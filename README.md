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
- **Run a network**: run the natural numbers or even numbers network, or import one from a JSON file.
- **Benchmark**: run every algorithm on the task suite, or find the best algorithm for the selected task.
- **Settings**: grouped into Evolution (task, target, fitness function, algorithm, population, mutation rate, generations, maximum neurons, experimental rules), Simulation (engine, steps, runs per network, rule form, output timing) and Benchmarks. *Reset to defaults* restores the default configuration.

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

Running a network shows the numbers it generated, one run's spike train with the intervals between its spikes, and how long the run took. For a network with input neurons, it shows its score on the selected task instead of the spike train. Evolved networks are saved to a timestamped folder, named in the message at the end of a run. To load one, choose *Run a network > Import* and give the path from the folder you run the program in, such as `6234234242322/TargetNet.json`.

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

`TaskSuite` holds a benchmark suite of these tasks, and the settings menu can select any of them.

## Engines

- **CPU, all cores** and **CPU, single thread** sample a number of random runs of each network, as before.
- **Exhaustive (exact outputs)** follows every possible computation, merging those that reach the same configuration. Its outputs are therefore the exact set the network can produce within the step limit, with no luck in the score. An exact solution is accepted without retesting. If a network's computations branch too widely, the engine samples it instead. It always samples spike trains, because merging computations would lose the trains that led to them.

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

`--task` and `--algorithm` match any name containing the text. These runs use the exhaustive engine unless given `--engine sampled`.

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
