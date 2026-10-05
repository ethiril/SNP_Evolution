// The Metal side of MetalEngine: one threadgroup runs one sampled computation, its threads sharing out the neurons.
//
// Step t mirrors NetworkSimulation.Step, rearranged so that it needs a single barrier. Each neuron first gathers what
// its senders emitted on step t - 1, plus that step's input, and then chooses and applies its rule for step t. What
// it emits goes into one of two buffers by the parity of t, so neighbours still reading step t - 1 are not disturbed.
// A closed neuron emits nothing, so its emission slot holds -1 instead, which is also how it turns away spikes.
//
// Every thread keeps its own copy of the run's progress and updates it identically after the barrier. Values that
// threads must agree on travel through threadgroup slots rotated over three steps: a slot written on step t was last
// read on step t - 3, which every thread finished before the barrier of step t - 1.
//
// A dispatch runs a chunk of steps and leaves everything in device memory, so the host can keep each command buffer
// short and resume where the last one stopped.
#include <metal_stdlib>
using namespace metal;

constant uchar None = 0;
constant uchar Fired = 1;
constant uchar Forgot = 2;

constant uint ReadoutOutput = 0;
constant uint ReadoutSpikeTrain = 2;

constant long ConsumesAll = -1;

// Networks with more output neurons than this run on the CPU.
constant uint MaxOutputs = 64;

constant int Closed = -1;

struct Params
{
    uint simCount;
    int maxSteps;
    uint steps;
    uint legacyTiming;
};

// Indexes in Neuron and Rule are local to their network; these bases make them global.
struct NetworkDesc
{
    uint neuronBase;
    uint neuronCount;
    uint ruleBase;
    uint acceptBase;
    uint incomingBase;
    uint outputCount;
    uint pad0;
    uint pad1;
};

struct Neuron
{
    long initialSpikes;
    uint ruleBegin;
    uint ruleEnd;
    uint incomingBegin;
    uint incomingEnd;
    int inputOrdinal;
    int outputOrdinal;
};

struct Rule
{
    long consume;
    // Lemire's fastmod constant for period, as the GPU has no integer divider.
    ulong periodMagic;
    uint acceptStart;
    int tail;
    int period;
    int produce;
    int delay;
    uint fires;
};

struct Sim
{
    uint network;
    uint stateBase;
    uint seed;
    uint inputBase;
    uint inputCount;
    int lastInputStep;
    uint trainBase;
    uint trainCapacity;
    uint readout;
    uint pad0;
    uint pad1;
    uint pad2;
};

struct Progress
{
    int step;
    int outputCounter;
    int outputEngaged;
    int output;
    int halted;
    int finished;
    int started;
    uint trainCount;
};

// Delays and emissions fit 16 bits (signed, for emissions); the host runs networks that need more on the CPU.
struct NeuronState
{
    long spikes;
    ushort legacyDelay;
    ushort closedFor;
    ushort pendingEmission;
    uchar legacyPending;
    uchar pad;
};

static inline uint Mix(uint x)
{
    x ^= x >> 16;
    x *= 0x7feb352du;
    x ^= x >> 15;
    x *= 0x846ca68bu;
    x ^= x >> 16;
    return x;
}

static inline bool RuleApplies(device const Rule& rule, device const uchar* accepts, long spikes)
{
    if (spikes < rule.consume)
    {
        return false;
    }
    ulong offset;
    if (spikes < rule.tail)
    {
        offset = ulong(spikes);
    }
    else
    {
        // Real division is kept for counts beyond 32 bits, which fastmod does not cover.
        ulong beyond = ulong(spikes - rule.tail);
        ulong cycle = beyond <= 0xFFFFFFFFul ? mulhi(rule.periodMagic * beyond, ulong(rule.period)) : beyond % ulong(rule.period);
        offset = ulong(rule.tail) + cycle;
    }
    return accepts[rule.acceptStart + uint(offset)] != 0;
}

kernel void Simulate(
    constant Params& params [[buffer(0)]],
    device const NetworkDesc* networks [[buffer(1)]],
    device const Sim* sims [[buffer(2)]],
    device Progress* allProgress [[buffer(3)]],
    device const Neuron* allNeurons [[buffer(4)]],
    device const Rule* allRules [[buffer(5)]],
    device const uchar* allAccepts [[buffer(6)]],
    device const uint* allIncoming [[buffer(7)]],
    device const uint* inputOffsets [[buffer(8)]],
    device const int* inputSteps [[buffer(9)]],
    device NeuronState* allStates [[buffer(10)]],
    device short* allEmitting [[buffer(11)]],
    device int* trains [[buffer(12)]],
    uint simIndex [[threadgroup_position_in_grid]],
    uint worker [[thread_index_in_threadgroup]],
    uint workers [[threads_per_threadgroup]])
{
    threadgroup atomic_uint anyActive[3];
    threadgroup uchar outputRelease[3][MaxOutputs];

    if (simIndex >= params.simCount || allProgress[simIndex].finished)
    {
        return;
    }
    Sim sim = sims[simIndex];
    NetworkDesc network = networks[sim.network];
    uint count = network.neuronCount;
    device const Neuron* neurons = allNeurons + network.neuronBase;
    device const Rule* rules = allRules + network.ruleBase;
    device const uchar* accepts = allAccepts + network.acceptBase;
    device const uint* incoming = allIncoming + network.incomingBase;
    device NeuronState* states = allStates + sim.stateBase;
    // Two emissions per neuron, side by side so a gather touches one cache line per sender, used by step parity.
    device short* emitting = allEmitting + 2 * sim.stateBase;
    Progress progress = allProgress[simIndex];

    if (!progress.started)
    {
        for (uint neuron = worker; neuron < count; neuron += workers)
        {
            states[neuron] = NeuronState { neurons[neuron].initialSpikes, 0, 0, 0, None, 0 };
            emitting[2 * neuron] = 0;
            emitting[2 * neuron + 1] = 0;
        }
        progress.started = 1;
    }
    if (worker < 3)
    {
        atomic_store_explicit(&anyActive[worker], 0, memory_order_relaxed);
    }
    threadgroup_barrier(mem_flags::mem_device | mem_flags::mem_threadgroup);

    for (uint chunkStep = 0; chunkStep < params.steps && !progress.finished; chunkStep++)
    {
        int step = progress.step;
        uint slot = uint(step) % 3;
        device const short* previous = emitting + ((uint(step) + 1) & 1);
        device short* current = emitting + (uint(step) & 1);
        // Once the run is over this pass only settles whether it ended halted, as NetworkSimulation.IsHalted would.
        bool over = step >= params.maxSteps || (sim.readout == ReadoutOutput && progress.output >= 0);

        bool active = false;
        for (uint neuron = worker; neuron < count; neuron += workers)
        {
            device const Neuron& data = neurons[neuron];
            NeuronState state = states[neuron];
            long held = state.spikes;

            // What arrived on the previous step, unless this neuron was closed to it.
            if (step > 0 && previous[2 * neuron] != Closed)
            {
                long arriving = 0;
                for (uint synapse = data.incomingBegin; synapse < data.incomingEnd; synapse++)
                {
                    arriving += max(previous[2 * incoming[synapse]], short(0));
                }
                if (data.inputOrdinal >= 0 && uint(data.inputOrdinal) < sim.inputCount)
                {
                    uint input = sim.inputBase + uint(data.inputOrdinal);
                    for (uint index = inputOffsets[input]; index < inputOffsets[input + 1]; index++)
                    {
                        arriving += inputSteps[index] == step - 1 ? 1 : 0;
                    }
                }
                held += arriving;
            }

            uint begin = data.ruleBegin;
            uint end = data.ruleEnd;
            bool busy = state.legacyDelay > 0 || state.closedFor > 0;
            // Applicable rules are noted in a bit mask, so a neuron with up to 32 rules checks each only once.
            uint applicable = 0;
            uint mask = 0;
            if (!busy)
            {
                for (uint rule = begin; rule < end; rule++)
                {
                    if (RuleApplies(rules[rule], accepts, held))
                    {
                        applicable++;
                        mask |= rule - begin < 32 ? 1u << (rule - begin) : 0;
                    }
                }
            }
            active |= busy || state.legacyPending != None || applicable > 0;
            if (over)
            {
                state.spikes = held;
                states[neuron] = state;
                continue;
            }

            int emit = 0;
            uchar release = None;
            bool closed = false;
            if (state.legacyDelay > 0)
            {
                state.legacyDelay--;
            }
            else if (state.closedFor > 0)
            {
                if (--state.closedFor > 0)
                {
                    closed = true;
                }
                else
                {
                    emit = state.pendingEmission;
                    state.pendingEmission = 0;
                    release = emit > 0 ? Fired : Forgot;
                }
            }
            else
            {
                int chosen = -1;
                if (applicable > 0)
                {
                    // Only a real choice costs a random draw.
                    uint pick = applicable == 1
                        ? 0
                        : uint((ulong(Mix(Mix(sim.seed ^ (neuron * 0x85EBCA6Bu)) ^ (uint(step) * 0x9E3779B9u))) * applicable) >> 32);
                    if (end - begin <= 32)
                    {
                        for (; pick > 0; pick--)
                        {
                            mask &= mask - 1;
                        }
                        chosen = int(begin + ctz(mask));
                    }
                    else
                    {
                        for (uint rule = begin; rule < end; rule++)
                        {
                            if (RuleApplies(rules[rule], accepts, held))
                            {
                                if (pick == 0)
                                {
                                    chosen = int(rule);
                                    break;
                                }
                                pick--;
                            }
                        }
                    }
                }
                if (state.legacyPending != None)
                {
                    // The original program let a rule chosen on this step emit, while the delayed rule emptied the neuron.
                    emit = chosen < 0 ? 0 : rules[chosen].produce;
                    release = state.legacyPending;
                    state.legacyPending = None;
                    held = 0;
                }
                else if (chosen >= 0)
                {
                    Rule rule = rules[chosen];
                    uchar outcome = rule.fires ? Fired : Forgot;
                    if (rule.consume == ConsumesAll)
                    {
                        emit = rule.produce;
                        if (rule.delay > 0)
                        {
                            state.legacyDelay = ushort(rule.delay);
                            state.legacyPending = outcome;
                        }
                        else
                        {
                            held = 0;
                            release = outcome;
                        }
                    }
                    else
                    {
                        held -= rule.consume;
                        if (rule.delay > 0)
                        {
                            state.closedFor = ushort(rule.delay);
                            state.pendingEmission = ushort(rule.produce);
                            closed = true;
                        }
                        else
                        {
                            emit = rule.produce;
                            release = outcome;
                        }
                    }
                }
            }
            state.spikes = held;
            states[neuron] = state;
            current[2 * neuron] = closed ? short(Closed) : short(emit);
            if (data.outputOrdinal >= 0)
            {
                outputRelease[slot][data.outputOrdinal] = release;
            }
        }

        // One write per SIMD group rather than per thread keeps the threads from queueing on the flag.
        if (simd_any(active) && simd_is_first())
        {
            atomic_store_explicit(&anyActive[slot], 1, memory_order_relaxed);
        }
        if (worker == 0)
        {
            atomic_store_explicit(&anyActive[(slot + 1) % 3], 0, memory_order_relaxed);
        }
        threadgroup_barrier(mem_flags::mem_device | mem_flags::mem_threadgroup);

        bool wasActive = atomic_load_explicit(&anyActive[slot], memory_order_relaxed) != 0;
        if (over || (!wasActive && step > sim.lastInputStep))
        {
            // Either the run is over, or the network was already halted so this step changed nothing and the CPU
            // would not have taken it.
            progress.halted = step > sim.lastInputStep && !wasActive;
            progress.finished = 1;
            break;
        }

        // Mirrors NetworkSimulation.RecordOutputNeuron, output neurons in network order.
        for (uint index = 0; index < network.outputCount; index++)
        {
            uchar release = outputRelease[slot][index];
            if (release == Fired && sim.readout == ReadoutSpikeTrain)
            {
                if (worker == 0 && progress.trainCount < sim.trainCapacity)
                {
                    trains[sim.trainBase + progress.trainCount] = step;
                }
                progress.trainCount++;
            }
            if (progress.output >= 0)
            {
                continue;
            }
            if (release != Fired)
            {
                if (progress.outputEngaged || params.legacyTiming)
                {
                    progress.outputCounter++;
                }
                continue;
            }
            if (progress.outputEngaged)
            {
                progress.output = ++progress.outputCounter;
                continue;
            }
            progress.outputEngaged = 1;
        }
        progress.step = step + 1;
    }

    if (worker == 0)
    {
        allProgress[simIndex] = progress;
    }
}
