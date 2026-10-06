# Spiking Neural P Systems evolved with the use of Genetic Algorithms
#### Researched and Developed by Michael Stachowicz, 15068126 - MMU

This project targets .NET 10.

Thanks to Newtonsoft for the JSON parser libraries which are provided with this software

## How to build

From the `SNP_Evolution` folder:

```
dotnet run --project SNP_Evolution    # start the console menu
dotnet test                           # run the test suite
```

## Installing the command

To run the program from any folder as `snp-evolution`, install it as a .NET global tool. On macOS, from the repository root:

```
sh install-snp.sh
```

Run the script again after changing the code to reinstall the latest build. It adds `~/.dotnet/tools` to your `PATH` in `~/.zshrc` if needed. Elsewhere, from the `SNP_Evolution` folder:

```
dotnet pack SNP_Evolution -c Release
dotnet tool install --global --add-source ./nupkg SNP_Evolution
```

To remove the command, run `dotnet tool uninstall --global SNP_Evolution`. Evolved networks are saved under the folder you run the command from.

## Using the GUI

Move with the arrow keys and press Enter to select, or press an option's number. ESC, Backspace or the left arrow goes back a level. Every submenu stays open until you leave it. The top of each screen shows the current task, search settings and simulation settings.

- **Evolve a system**
  - *Match a target*: type the output you want and a maximum number of generations (1000 if left empty), then evolve a network for it from scratch. A target is one of three kinds:
    - **Set**: the numbers a run can produce, in any order, e.g. `2,4,6,8`. Each run gives one number: the steps between the output neuron's first two spikes.
    - **Sequence**: the gaps between successive output spikes, in order and with repeats, e.g. `1,1,2,3,5,8,13`.
    - **Binary word**: the output spike train step by step, 1 for a spike, e.g. `001001001001`.
  - *For the selected task*: evolve from scratch for the task chosen in Settings > Evolution: the target or a suite task.
  - *Starting from the natural numbers or even numbers network*: evolve a hand-designed network towards the target. To evolve only its rules, as in the original paper, first pick a "rule expressions only" algorithm in Settings > Evolution.
  - *Suggest settings*: show what the advisor recommends for the selected task, apply it if you agree, and optionally run a quick pilot that picks the algorithm. *Match a target* does this before every run.
  - *Compile a target, then shrink it*: type a sequence or set target, then build a network that is correct by construction and evolve it smaller (see [Compile, then shrink](#compile-then-shrink)). For a set you can give a register program file, or leave it empty to evolve one. Leave the shrink generations empty for 300, or enter 0 to only compile.
  - *Evolve the first library parts*: run `evolve-parts` (see [Evolving library parts](#evolving-library-parts)) with a seed you give, 1 if left empty.
  - *Redo a saved run*: after a run you can save it under a name. Saved runs can be run again as they were, have their settings loaded to change first, or be deleted.
- **Run a network**: run the natural numbers or even numbers network, or import one from a JSON file.
- **Benchmark**: run every algorithm on the task suite, or find the best algorithm for the selected task.
- **Settings**: grouped into:
  - **Evolution**: task, target, fitness function, algorithm, population, mutation rate, generations, maximum neurons and experimental rules.
  - **Search**: iterative evolution and its stages, stagnation recovery, genome limits (delay, spikes produced, initial spikes, duplicate neurons), lexicase parents, modules (build from modules, freeze, triggered, incubation, and module files that start the library), and composition search (part library folder, part copies, glue, proposing parts when stalled, and starting from the hand-built parts).
  - **Simulation**: engine, steps, runs per network, rule form and output timing.
  - **Benchmarks**.

  *Reset to defaults* restores the default configuration.

Sequence and binary targets have some limits:

- Only a network that gives the output on every sampled run solves the target. Spike trains are always sampled, even on the exhaustive engine.
- Runs are lengthened automatically to fit the whole target.
- Small SN P systems produce spike trains that eventually repeat. An evolved network matches the numbers you typed but need not continue the pattern after them. For example, one evolved network for `1,1,2,3,5,8,13` continued with `6,8`.
- The default algorithm, MAP-Elites, evolves structure as well as rules. If you pick a rule-only algorithm, the menu offers to switch back before evolving from scratch, since a random network's structure would never change. Long or irregular targets need a larger population than the default.

## Understanding the evolution outputs in the console
When evolving, the console shows the best network after every generation: its neurons, its rules, what it did on the task, and its fitness. For each kind of task, that line shows:

- **Generator**: the distinct numbers it produced.
- **Sequence**: `intervals [1,1,2,3] / [1,1,2,3,5]` (output / target).
- **Binary word**: `spikes 0010010 / 0010010`.
- **Function**: `f(3)={3}/3` (outputs / expected).
- **Acceptor**: the numbers it accepted.

Sequence and binary lines add "(varies between runs)" when the network is nondeterministic. With the MAP-Elites algorithm, the end of the run also lists the fittest network of each size.

Running a network shows the numbers it generated, one run's spike train with the intervals between its spikes, and how long the run took. For a network with input neurons, it shows its score on the selected task instead of the spike train. Evolved networks are saved to a timestamped folder inside `Test Data`, named in the message at the end of a run. To load one, choose *Run a network > Import* and give the path from the folder you run the program in, such as `Test Data/6234234242322/TargetNet.json`.

## Rule forms

Two rule forms are supported, and one network can mix them:

- **Legacy** (`aa -> a`, the original program's rules): when the expression matches the spike count, the neuron empties and sends one spike. A delayed rule sends its spike at once, then holds the neuron for d steps before emptying it.
- **Standard** (`E/a^c -> a^p;d`, as in the SN P literature): when the expression matches and the neuron holds at least c spikes, the rule consumes exactly c spikes and sends p along each synapse. A delayed rule closes the neuron for d steps: spikes sent to it in that time are lost, and it fires when it reopens. The notation leaves out `E/` when E only matches a^c, so `aaa -> aa` consumes 3 spikes and sends 2.

The *Rule Form* setting (Legacy, Standard or Mixed) picks the form of the rules evolution creates. The *Output Timing* setting picks how the output neuron's spikes become a number. *Legacy* counts every non-spiking step from the start, as the original program did. *Interval* counts the steps between the first two spikes, as the literature does. JSON files gain optional `Consume`, `Produce` and `IsInput` fields, and older files load as legacy rules.

## Tasks

A task says what a network should do (`Evolution/Tasks/ITask`):

- **Generator**: with no input, produce exactly a set of numbers, such as a set target from the settings.
- **Sequence**: with no input, space the output spikes by the target's gaps, in order (`SequenceTask`). Each gap that is wrong but close earns partial credit.
- **Binary word**: with no input, spike exactly on the target's 1s (`SpikeWordTask`). Scored by balanced accuracy over the 1s and 0s.

The last two read the whole output spike train (`Readout.SpikeTrain`), not just the first two spikes. `OutputTarget` turns a typed target into the matching task.
- **Function**: read numbers on the input neurons, each as two spikes n steps apart, and output f(n). Examples are `n + 1`, `2n` and `n1 + n2`. Wrong but close outputs earn partial credit.
- **Acceptor**: read a number on the input neuron and halt if and only if it belongs to the set. Scored by balanced accuracy.
- **Contract**: behave as a part with a start trigger, one or more done triggers and typed data ports (interval, count, trigger or binary), as a `Contract` in `Evolution/Contracts/` describes (`ContractTask`). Each case is checked against four rules: nothing is sent before start, done fires exactly once with the right outputs, every neuron ends with the spikes it started with, and done fires within the maximum latency. It reads the named port neurons (`Readout.Ports`), which the exhaustive engine follows over every computation. `ReferenceParts` holds a hand-built delay and register that meet their contracts.

`TaskSuite` holds a benchmark suite of these tasks, and the settings menu can select any of them. It includes eight arithmetic contracts (`ArithmeticParts`): n1 - n2 (with n1 >= n2), n1 x n2, n1 div n2 with remainder, and n1 < n2 with two done branches. Each comes in count encoding and in binary with 4-bit operands and an 8-bit product. Cases include zero operands and one larger pair.

## Engines

- **Auto: fastest available** is the default. It is the GPU engine below on a Mac, and CPU, all cores elsewhere.
- **CPU, all cores** and **CPU, single thread** sample a number of random runs of each network, as before.
- **Exhaustive (exact outputs)** follows every possible computation, merging those that reach the same configuration. Its outputs are therefore the exact set the network can produce within the step limit, with no luck in the score. An exact solution is accepted without retesting. If a network's computations branch too widely, the engine samples it instead. It always samples spike trains, because merging computations would lose the trains that led to them.
- **GPU (Metal), for large networks** is listed on Macs. It samples like the CPU engines, but runs every run of every network in the population at once on the GPU, with one threadgroup per run. On an M5 Pro it is about 6 to 12 times faster than all CPU cores for networks of a thousand neurons or more. Its rule choices come from a hash of the seed rather than `System.Random`, so individual runs differ from the CPU engines' while following the same distribution, and the same seed repeats them. Batches too small to repay the GPU round trip, and networks with more than 64 output neurons, delays above 65,535 or emissions above 32,767, run on the CPU instead.

## Algorithms

- **Generational, rule expressions only** (roulette wheel or tournament): the original algorithm. It changes only the rule expressions and keeps the starting network's structure.
- **Generational, structural**: tournament selection, a crossover that works across different topologies, and structural mutation.
- **(mu + lambda) evolution strategy**: mutated children compete with their parents, and the best survive. Children win ties, so the search can drift across equally fit networks.
- **MAP-Elites**: keeps the best network for every size, which maps out how fitness trades against size.
- **NEAT-style speciated**: groups similar structures into species that share fitness, so new structures get time to improve.

Structural mutation (`Evolution/Operators/StructuralMutations.cs`) is a weighted mix of small edits. It can:

- nudge or replace an expression;
- change a delay, a consumed or produced count, or whether a rule fires;
- add or remove rules, synapses and neurons;
- split a synapse with a relay neuron;
- nudge initial spikes.

Every algorithm ranks equally fit networks smaller first.

## Checks, lexicase selection and modules

Every task also scores a network on each separate thing it checks: each gap of a sequence, each step of a binary word, each example of a function or acceptor, each number of a set. Two options in *Settings > Search* use these checks, and both are off by default:

- **Lexicase parents** (`--lexicase on`): parents are picked by lexicase selection. Each pick goes through the checks in a random order and keeps only the networks that do best on each. A network that is the only one to get some part of the target right gets to breed even when its fitness is low.
- **Build from modules** (`--modules on`): the run keeps a library of parts (modules) and builds networks out of them. Modules come from three places, all found by the run itself:
  - a change that made a child get a check right that its parent did not, cut out with the neurons around it;
  - the network that solved each stage of an iterative run;
  - side runs. When the search stalls, the run finds the first check no network does yet, such as gap 7 of a sequence. It evolves a small network (at most 24 neurons) for just the gaps around it on the side. With *Triggered modules* on (`--triggered off` to stop it), every other side run instead builds a part that waits for a spike on an input before it makes the missing gaps, so it can follow on from what a host already does rather than run beside it from the first step. A part that a side run solved is reused the next time the same gap is missing, rather than evolved again.

  Copies of the new module go into the best networks. With *Module incubation* above 0 (`--incubate N`, 30 generations by default) those networks first evolve on their own, seeded only with each other, until one beats the main run or the generations run out; the best of them then join the main run, and the module is credited with whether it led to a better network. When a module takes over as the output, the old output neuron sends to it, so what the host already made can still pass through. Side runs and incubation cost evaluations the main run's generation count does not show; `-modules.txt` gives how many generations ran on the side.

  Mutation can insert a copy of a module, picked by how often it has helped, or free one so its neurons evolve like any other. Inserted modules are frozen unless *Freeze modules* (`--freeze off`) is off: other edits cannot change their rules or inner synapses, only their initial spikes and how they are wired in. `--module-files a.json,b.json` (*Module files* in the menu) starts the library with saved networks, such as the best network of an earlier run.

  The run saves the library, with how often each module was tried and helped, as `-modules.txt` next to the network.

For gap sequences that keep growing, such as Fibonacci, the advisor suggests turning both on. On 16 values of Fibonacci (100 neurons, 2000 generations, 15 seeds), runs with both solved 7.3 values on average. Runs without them solved 5.9, or 6.5 when given as many extra generations as the side runs added on average.

## Compile, then shrink

`compile` builds a network that is correct by construction instead of evolving one from scratch, then evolves it smaller while it stays correct:

```
snp-evolution compile --target "1,1,2,3,5,8,13,21,34,55,89,144,233,377,610,987" --shrink 2000
snp-evolution compile --kind set --target "2,3" --generations 400
snp-evolution compile --kind set --target "2,4,6" --program my-program.txt
```

- **Sequences** are fitted with a recurrence: a few starting values, then each value is a fixed sum of the ones before it, such as `g(n) = g(n-1) + g(n-2)` for Fibonacci. Up to three values back are tried, each counted up to four times. The network holds each value as spikes in pairs of neurons. Each gap is made by draining, one spike per step, the pairs that hold the values it is the sum of, while the drained spikes are written into fresh pairs for the gaps that come later. Gaps come out exact and never stop, so the network keeps going past the values given. Fibonacci compiles to 15 neurons and is right for every value checked, not only the 16 given. Shrinking it for 2000 generations reached 10 neurons on 4 of 5 seeds. After shrinking, the run checks the next 4 values of the recurrence, since shrinking only sees the values given; every shrunk Fibonacci network so far still got them right. A sequence that follows no recurrence of that kind cannot be compiled.
- **Sets** are generated by a register program, as in the SN P universality proofs: `ADD`, `SUB` and `HALT` instructions, where register 0 is the output and can only be added to. The program is compiled with the standard ADD and SUB modules (Ionescu, Păun & Yokomori 2006) using standard rules, and it outputs the interval between two spikes. Programs are evolved unless `--program FILE` gives one, one instruction per line:

  ```
  0: ADD r1 -> 1          # add one to r1, go to 1
  1: ADD r0 -> 2 | 3      # add one to r0, go to 2 or 3 at random
  2: SUB r1 -> 1 else 3   # take one from r1 and go to 1, or go to 3 if r1 is zero
  3: HALT                 # generates the value of r0
  ```

  The program search scores a program by following every choice, which is far cheaper than simulating its network. It learns the set a few numbers at a time, and its last stage looks beyond the largest number, so programs that overshoot are caught. Parents are picked by lexicase selection over the target numbers plus a "nothing extra" check (`--lexicase off` to pick the fittest instead). Without it, the search usually gets stuck on a program that generates too much, such as every even number. Over 10 seeds of 3000 generations each:

  | Target | Lexicase | Fittest only |
  |---|---|---|
  | {2,4,6} | 10 found | 3 found |
  | {1,2,3,5,8,13} | 2 found | 0 found |
  | Fibonacci up to 987 | 0 found; 9 seeds got as far as 34 | 0 found; every seed stuck before 5 |

  The programs it finds for larger sets fill all 16 instructions and list the numbers rather than compute them, so for sets such as Fibonacci's give the program yourself.
- **Shrinking** (`--shrink N` generations, 300 by default, 0 to skip) starts every network from the compiled one. Children compete on fitness first and size second, and a smaller network only counts once it passes the same retests that stop a run. It uses the usual edits apart from those that only add, plus two more: bypassing a neuron (whatever sent to it sends on to its targets) and merging two neurons with the same rules.

The run saves `Program.txt` (the recurrence or program), `Compiled.*` and `Shrunk.*` (network, notation, graph and page) and `Shrunk.csv` (fitness history).

## Evolving library parts

`evolve-parts` evolves small verified parts that later runs can build machines from. Each part has a start input, a done output and typed data ports, and a contract it must meet: nothing comes out before start, done fires exactly once with the right values on the outputs, every neuron ends where it began, and done fires in time. The goals are ten general arithmetic parts, none specific to any target (`FirstParts.Table()` prints them):

| Part | Ports besides start and done | Contract |
|---|---|---|
| Delay k | none | done fires k steps after start (k = 1..4, one contract each) |
| Fan-out | count in; count out x2 | both outputs carry n |
| Increment | count in; count out | output carries n + 1 |
| Double | count in; count out | output carries 2n |
| Add | count in x2; count out | output carries n1 + n2 (cases cover pairs up to 6 + 6) |
| Interval to count | interval in; count out | output carries n |
| Count to interval (timer) | count in; interval out | two output spikes n steps apart |
| Register | count in; count out | holds n until started again, then drains it |
| Zero test | count in; done-zero, done-nonzero | the right branch fires, the other never |
| Sequencer | done out xk | fires its outputs in order, each one step after the previous (k = 2, 3) |

Count cases run from 0 to 8 plus 12, to catch a part that only memorised small values.

```
snp-evolution evolve-parts --seed 1
snp-evolution evolve-parts --only "add,fan-out" --budget 200000 --redo on
```

For each contract the library has no part for, the command evolves one from scratch with MAP-Elites (lexicase parents, standard rules), verifies it on the exhaustive engine over every computation of every case, then shrinks it with MAP-Elites over hardware-cost cells and keeps the cheapest network that still verifies. Hardware cost orders by neurons, then synapses, then rules, then register width (the most spikes any neuron holds during the cases); distinct rules and lasso table size break any remaining tie.

- `--seed N` (1 by default): each contract gets its own seed from it, so the same seed writes the same library whichever contracts run together.
- `--budget N` (50000 by default): search evaluations per contract; shrinking spends a quarter as many again.
- `--only NAMES`: contracts whose names contain any of the comma-separated names.
- `--library DIR` (`parts/` at the repository root by default): the library folder.
- `--engine exact|sampled`: the engine the search scores on; verification is always exhaustive.
- `--redo on`: evolve contracts the library already has a part for; the new part replaces the old only if it is cheaper.

The library folder holds one JSON file per contract (`delay-1.json`, `zero-test.json`) with the network (in the usual network file format), contract, port binding, hardware cost, latency, seed, run and evaluations. Loading verifies every part again and refuses a file whose part fails its contract or whose contract differs from the catalogue, naming the file. Two parts that read the same on every case of a contract count as one, and the cheaper is kept under the first one's id.

The run ends with a table of each contract, whether it was solved, the evaluations it used, and the kept part's neurons, synapses and latency. It exits with 2 when a contract is left without a part.

With seed 1 and the default budget (47 minutes on a 15-core machine), the run solved delay 2, 3 and 4 (2 neurons, 1 synapse each) and sequencer 2 (6 neurons, 8 synapses). Delay 1 was solved by other seeds but not this one, and sequencer 3 and the zero test came close (best fitness 0.97). None of the parts with count ports was solved. Their best networks score about 0.8, getting every rule right except putting the right values out and firing done once. A part that holds a count until start needs the parity trick of the hand-built register: each input spike is stored as two, and start makes the total odd so that a rule matching odd counts drains it. Larger networks, lexicase off, and partial credit for right values when done misfires all left the best near 0.8 within 50000 evaluations. The `parts/` folder in the repository holds the parts this run found.

## Composing machines from parts

Composition search (the "Composition search" algorithms) builds each network from copies of library parts, glue neurons and the synapses between them, and never changes a part's inside. Parts are wired port to port by type. For a contract task the task's own ports count as typed ports too: a part's count in-port can be fed straight from the task's count input, and its done port can drive the task's done neuron. The glue then starts as quiet relays with no synapses of their own, since the parts and the ports give the structure. Without a contract, glue starts random, because something has to fire.

```
snp-evolution compose --task "Contract multiply" --hand-built on --library parts-hand-built
snp-evolution benchmark --task "Contract multiply" --algorithm Composition --lexicase on --engine sampled --hand-built on
```

`compose` runs composition search for one suite task, with lexicase parents, 30000 evaluations and tournament selection unless told otherwise (`--algorithm`, `--evaluations`, `--generations`, `--population`, `--seed`, `--max-parts`, `--glue`). It exits with 2 when the task is not solved.

**Promotion.** When a composition solves a contract, it is promoted to a part whose contract is the target's (`Promotion`). The run scored it on the task, which may test less than the contract, so it is first verified on the exhaustive engine. A contract counts as solved only at fitness 1, since a neuron left holding a spike means the part cannot be started again. The promoted part's file holds a recipe rather than a network: its children by contract name, its glue, and its wires, with ports named (`"2.sum"`) so a cheaper child that later replaces one is wired the same way. Loading builds promoted parts after the parts they are built from and verifies them again. A promoted part's size is its children's, so the 24-neuron cap applies only to modules evolved or harvested as one network.

**Proposals.** When a composition run stalls, it asks for the parts it lacks (`Proposals/`). For a sequence target it first fits the shape of the target, once: a small linear recurrence asks for one register per term, an add per sum and a double or fan-out per coefficient above one, and gaps with a constant first or second difference ask for registers and adds. Fibonacci gaps give two registers and an add, and 2^k gives a register and a double. When nothing fits exactly it proposes nothing, since a wrong part costs a whole part evolution. Then it reads the checks nobody passes: a missing gap becomes a delay contract, and a contract's failing cases become a sub-contract. Each proposal is evolved and verified like a first part (`--proposal-budget N`, 20000 evaluations by default), and a solved one joins the library, with copies put into the best networks. `--propose off` turns this off. The run log and `-parts.txt` list every proposal and its outcome, and the evaluations count as "proposed parts".

**Reuse.** Every composition run saves `-parts.txt`: each library part's copies in the best network, with copies nested inside promoted parts in brackets, and its mean copies per network in the final population. It ends by saying whether the best network reuses a promoted part. Counting reads the module tags of the flattened network, so it works for any algorithm. Benchmarks of composition search add a column of the parts the best networks hold, as runs out of all. This is not the module library's uses and wins, which count whether inserting a copy made a child fitter during the search.

**Hand-built parts.** evolve-parts has not yet found a part with count ports, so `--hand-built on` (*Composition: start from hand-built parts* in the menu) adds hand-built ones: register, add, increment, fan-out, zero test, decrement and a gate that passes a count on or swallows it. It also adds an *add loop*, a + n x b, built from seven of them and ten glue neurons and promoted like any composition (49 neurons, latency 223). Every hand-built part is verified on its contract. They are off by default, since the library should be one the runs found, and a run with them never saves to the default `parts/` folder. `--hand-built leaves` gives the parts without the add loop, as a control.

On n1 x n2 in count encoding (10 seeds per algorithm, 6000 evaluations each, lexicase parents, 5 sampled runs per network, the `parts/` library plus the hand-built parts), composition search solved 14 of 20 runs with the add loop in the library: 7 of 10 for each algorithm, with medians of 1365 (tournament) and 2045 (MAP-Elites) evaluations. The add loop was in the best network of 18 of 20 runs, and every solution was promoted. With `--hand-built leaves`, the same parts without the add loop, no run solved it (best fitness 0.875 on average; Fisher's exact test p = 3e-6). A network that only relays start to done scores 0.85 on multiplication, because every case with a zero product passes, so without a loop part the search stalls there. The promoted multiplier has 55 neurons and 66 synapses and takes 225 steps for 6 x 5.

```
snp-evolution benchmark --task "Contract multiply" --algorithm Composition --seeds 10 --budget 6000 --lexicase on --repetitions 5 --engine sampled --hand-built on
snp-evolution benchmark --task "Contract multiply" --algorithm Composition --seeds 10 --budget 6000 --lexicase on --repetitions 5 --engine sampled --hand-built leaves
```

## Benchmarking and choosing an algorithm

*Benchmark > Every algorithm* runs every algorithm on every suite task over several seeds. Each run has a budget of network evaluations, so the results compare fairly across machines and engines. For each algorithm and task it reports how many runs solved the task, the median evaluations to solve, the mean best fitness and the mean size of the solutions. It saves the results as `benchmark.txt` and `benchmark.csv`.

*Benchmark > Find the best algorithm* picks the best algorithm for the selected task by successive halving. Every algorithm gets a small budget, the better half goes on with double the budget, and this repeats until one is left. You can then make the winner your algorithm.

Both also run without the menu:

```
dotnet run --project SNP_Evolution -c Release -- benchmark --budget 3000 --seeds 3 --task "Compute" --algorithm "MAP"
dotnet run --project SNP_Evolution -c Release -- select --task "Accept even"
dotnet run --project SNP_Evolution -c Release -- evolve --target "1,1,2,3,5,8,13" --generations 4000 --population 100
dotnet run --project SNP_Evolution -c Release -- evolve --kind binary --target 001001001001
dotnet run --project SNP_Evolution -c Release -- tasks        # list the suite
dotnet run --project SNP_Evolution -c Release -- algorithms   # list the algorithms
```

`--task` and `--algorithm` match any name containing the text. These runs use the exhaustive engine unless given `--engine sampled`. `--lexicase on` picks parents by lexicase selection, and `--repetitions N` sets the sampled runs per network. A task or algorithm named exactly wins over those that only contain the name. Composition search builds from `--library DIR`, plus the hand-built parts with `--hand-built on`; a contract it solves is promoted into that run's library.

`evolve` builds a network from scratch for a target. `--kind` is `set`, `sequence` (the default) or `binary`. `evolve` uses the default algorithm, MAP-Elites, unless given `--algorithm`, and `--seed N` makes a run repeatable. It saves the result like the menu does, and exits with status 2 if no network solved the target.

File Structure (under `SNP_Evolution/SNP_Evolution`):

- `Program.cs` starts the console menu, or runs a command when given arguments.
- `Networks/`: the SN P system.
  - `Rule`, `Neuron` and `Network` describe a system.
  - `SpikeCondition` compiles a rule expression into the exact set of spike counts it matches.
  - `NetworkNotation` renders a network as a readable text table.
  - `ReferenceNetworks` holds the hand-built natural and even numbers systems.
- `Simulation/`: runs networks.
  - `CompiledNetwork` flattens a network into arrays.
  - `NetworkSimulation` steps one computation of it.
  - `InputSpikes` describes what the environment feeds the input neurons.
  - `NetworkRunner` samples runs.
  - The `ISimulationEngine` implementations run whole batches of trials.
- `Evolution/`: the search.
  - The algorithms, with their swappable `Operators/`.
  - `NetworkFactory` and `GenomeSpace` for random networks.
  - `Ranking`, the fitness functions and the evaluator.
  - `Tasks/` holds the tasks and the suite.
  - `Contracts/` holds contracts, the first parts, the arithmetic contracts and the hand-built parts.
  - `Modules/` holds the module library and the modular loop: cutting modules out of networks, inserting and freezing them, harvesting the changes that pay off, and side runs on what is missing. It also holds composition search, promotion, reuse counting and the hand-built add loop.
  - `Proposals/` proposes parts when a composition run stalls, from its failing checks or the shape of its target.
  - `Benchmarking/` holds the benchmark harness and the algorithm selector.
- `Storage/` saves and loads networks as JSON and fitness history as CSV.
- `Cli/` holds the console menus, settings and command-line mode. `Catalog` lists the engines, fitness functions, tasks and algorithms the settings menu offers. `EvolutionSession` runs and saves one evolution, for both the menu and the `evolve` command.

## How spikes are stored

Spikes are stored as counts (up to `long.MaxValue`) rather than strings of `a`. Each rule expression is a regex over `a`, and the counts it matches always settle into a repeating pattern. Each rule is therefore compiled once into a short lookup table plus a repeating cycle. Matching a rule costs the same whether a neuron holds 2 spikes or 2 trillion. The supported syntax is literals, `.`, groups, `|` and the `?`, `*`, `+`, `{n}`, `{n,}` and `{n,m}` quantifiers. Saved networks write `SpikeCount` as a number; older files that wrote it as `"aa"` still load.

When an evolution finishes, the best network is printed and saved as a `.txt` table next to its `.json`, with spike counts written back out as runs of `a` (`a^N` for long runs).

## Extending

Each swappable part is an interface plus one line in a catalog, after which it appears in the settings menu:

- **Simulation engine** (`ISimulationEngine`, listed in `Cli/Catalog.cs`): receives a whole batch of trials (a network, its input and what to read back) so it can spread the runs out. Read each network through `CompiledNetwork.Of(network)`, whose flat arrays are ready to copy to a GPU, and seed any per-run generators from the `Random` passed in, as `ParallelCpuEngine` does.
- **Fitness function** (`IFitnessFunction`, `Cli/Catalog.cs`): scores a generator's sorted outputs from 0 to 1.
- **Task** (`ITask`, `Evolution/Tasks/TaskSuite.cs` or `Cli/Catalog.cs`): lists its cases (input spikes and readout), then scores and describes the results.
- **Algorithm** (`IGeneticAlgorithm`, `Evolution/EvolutionContext.cs`): build it from the `EvolutionContext`, which provides the evaluator, a network factory and structural mutation. Or combine new `IParentSelection`, `ICrossover` or `IMutation` operators with an existing algorithm. A new algorithm is automatically included in the benchmark and the selector.
