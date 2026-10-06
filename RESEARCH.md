# Follow-up paper research notes

Literature check done 2026-10-05. Most papers were read as abstracts only (marked *abstract*).

## Baseline paper

Dong, Stachowicz, Zhang, Cavaliere, Rong, Paul, "Automatic Design of Spiking Neural P Systems Based on Genetic Algorithms", Int. J. Unconventional Computing (accepted June 2020). [MMU repository](https://repository.mmu.ac.uk/articles/journal_contribution/Automatic_Design_of_Spiking_Neural_P_Systems_Based_on_Genetic_Algorithms/32459331)

- Evolved only the rule regexes and delays. Neuron count, synapses, number of rules and initial spikes were fixed to the known hand-designed systems.
- Tasks: generate {1..9} and evens {2..16}. Population 4, 50 steps, 50 repetitions, 200 generations, 10 runs. Fitness curves only, no statistical tests.
- Fitness: `2tp / (2tp + fp + fn) × sf`. The paper never defines `sf`; `SetCoverageFitness` reproduces the fitness used in the runs.
- Future work stated in the paper: evolve neuron count and synapses; asynchronous and time-free SN P systems.
- The original code differs from standard SN P semantics. Both differences are kept as legacy modes:
  - `RuleForm.Legacy`: `a -> a` rules that empty the neuron.
  - `OutputTiming.Legacy`: counts steps from the start, not between the first and second output spikes.

  Disclose both and use them as the baseline.

## Planned contributions and novelty

| # | Contribution | Verdict |
|---|---|---|
| 1 | Evolve neurons, synapses, regex rules, delays and initial spikes together, from scratch | Partly done: see Leporati & Rovida, Custode, Casauay, Dong 2023 |
| 2 | Function and acceptor tasks | Partly done: adders and arithmetic are already evolved. No evolved acceptors or n+1/2n found |
| 3 | GA vs μ+λ vs NEAT-style vs MAP-Elites, same evaluation budget | Looks new: no controlled comparison and no MAP-Elites for any P system found |
| 4 | Exact fitness by exploring every computation (`ExhaustiveCpuEngine`) | New as a fitness method; exploration exists only for simulation and verification |
| 5 | Regexes compiled into eventually-periodic lookup tables | Looks new as engineering; the theory (unary regular languages are semilinear) is known |

| 6 | Modules discovered during evolution, kept in a library and composed (no hand-built modules by default) | Looks new: no modules, ADFs or libraries in any SN P evolution found; nearest is Cao et al. 2010 (hand-built library, stochastic P systems) |
| 7 | Compile a register-machine program into SN P with the standard modules, then evolve it smaller | Looks new as an automated method; neuron reduction so far is by hand |
| 8 | Modules with contracts (what they compute, on typed ports), composed by wiring port to port, with solved compositions kept as new modules | Looks new for SN P; nearest are ECGP and functional modularity in GP. See "Composing modules into machines" |
| 9 | Compile a linear recurrence on output gaps into an SN P system whose gaps are exact and never stop (Fibonacci in 15 neurons, 2 or 3 rules each) | Looks new: no SN P system generating Fibonacci as output gaps found (see searches below); check before claiming |

Nothing found on automatic design of asynchronous or time-free SN P systems, so that is still open.

Suggested framing: lead with 3 and 4 (exact-fitness comparison of algorithms, plus MAP-Elites fitness-vs-size maps). Use acceptors and the step up from the baseline as supporting results. Don't headline n1+n2.

## Related work

- **Dong, Luo, Zhang (2023)**, "Automatic design of arithmetic operation SN P systems", *Natural Computing* 22:55-67. https://doi.org/10.1007/s11047-022-09902-5 — Direct follow-up by co-authors: new encoding and evolutionary strategy for arithmetic. *Abstract; paywalled.* **TODO: read the full text to check whether it evolves synapses and neuron count.**
- **Leporati, Rovida (2025)**, "An evolutionary approach to the design of spiking neural P circuits", *J. Membrane Computing* 7:221-235. https://doi.org/10.1007/s41965-024-00180-x ([CMC 2024 preprint](https://webusers.i3s.unice.fr/CMC2024/resources/regular/CMC_2024_paper_10.pdf)) — Closest prior work, read in full. A GA evolves structure and rules for layered, acyclic Boolean circuits. Rules are `a^i -> a` only, with no delays and deterministic computation.
- **Casauay, Cabarle, Macababayao, de la Cruz, Adorna, Zeng, Martínez-del-Amor (2021)**, "A Framework for Evolving Spiking Neural P Systems", IJUC 16(2-3):83-119 (same issue as the baseline). https://www.oldcitypublishing.com/journals/ijuc-home/ijuc-issue-contents/ijuc-volume-16-number-2-3-2021/ijuc-16-2-3-p-83-119/ — Evolves neurons and synapses of a hand-designed system with the rules fixed. *Abstract.*
- **Gungon et al. (2022)**, "GPU implementation of evolving spiking neural P systems", *Neurocomputing*. https://doi.org/10.1016/j.neucom.2022.06.094 — Casauay's framework on CuSNP. Adders and subtractors, up to 9x faster than CPU. *Abstract.*
- **Custode, Mo, Iacca (2022)**, "Neuroevolution of Spiking Neural P Systems", EvoApplications, LNCS. https://doi.org/10.1007/978-3-031-02462-7_28 — NEAT evolving SN P systems as policies for Gym control tasks. *Abstract.*
- **Custode, Mo, Ferigo, Iacca (2022)**, "Evolutionary Optimization of SN P Systems for Remaining Useful Life Prediction", *Algorithms* 15(3):98. https://doi.org/10.3390/a15030098 — Customised NEAT for regression. *Abstract.*
- **Luo, Zhang, Xiao, Dong (2022)**, "An Evolutionary Design Method for an Adder with Spiking Neural P Systems", PRAI 2022. https://doi.org/10.1109/PRAI55851.2022.9904063 — Evolves initial spikes and rules for an adder. *Abstract.*
- **Hernández-Tello, Martínez-del-Amor, Orellana-Martín, Cabarle (2024)**, "Sparse Spiking Neural-like Membrane Systems on GPUs", IJNS 34(07). https://arxiv.org/abs/2408.04343 — Read in full. Supports only the regexes `a*`, `a+` and `a^n`, and simulates one computation path only.
- **Carandang, Cabarle, Adorna et al. (2019)**, "Handling Non-determinism in SN P Systems: Algorithms and Simulations", *Fundamenta Informaticae* 164. https://doi.org/10.3233/FI-2019-1759 — Explores nondeterministic choices for simulation, not for fitness. *Abstract.*
- **Gheorghe, Lefticaru, Konur, Niculescu, Adorna (2021)**, "SN P systems: matrix representation and formal verification", JMC. https://doi.org/10.1007/s41965-021-00075-1 — Model-checks SN P systems via kP systems. *Abstract.*
- Related work on non-spiking (cell-like) P systems:
  - Leporati et al. (2023), "Inferring P systems from their computing steps", *Swarm & Evol. Comp.*: a (μ+λ) EA. https://www.sciencedirect.com/science/article/abs/pii/S2210650222001894
  - Nadizar, Pietropolli (2023), grammatical evolution for inferring P systems, JMC. https://doi.org/10.1007/s41965-023-00125-w

### Modules, compilation and finding what is missing

Second literature check, 2026-10-05, for contributions 6 and 7. Citations confirmed by web search, mostly at title or abstract level.

SN P constructions and their size:
- **Ionescu, Păun, Yokomori (2006)**, "Spiking neural P systems", *Fundamenta Informaticae* 71(2-3):279-308. https://doi.org/10.3233/FUN-2006-712-308 — The standard ADD, SUB and FIN modules for simulating register machines (a register holding n is 2n spikes). The compiler for contribution 7.
- **Păun, Păun (2007)**, "Small universal spiking neural P systems", *BioSystems* 90(1):48-60. https://www.sciencedirect.com/science/article/abs/pii/S0303264706001158 — 84 neurons, reduced by merging consecutive instructions into compound modules (ADD-ADD, SUB-ADD). Reduction by hand.
- **Zhang, Zeng, Pan (2008)**, *Fundamenta Informaticae* 87(1):117-136, and **Neary (2015)**, "Three small universal SN P systems", *TCS* 567:2-20 — Smaller universal systems (Neary: 17 neurons with standard rules). Size references.
- **Ibarra, Păun, Păun, Rodríguez-Patón, Sosík, Woodworth (2007)**, "Normal forms for spiking neural P systems", *TCS* 372:196-217. https://doi.org/10.1016/j.tcs.2006.11.025 and **Macababayao, Cabarle, de la Cruz, Zeng (2022)**, *Information Sciences* 595:344-363 — Normal forms; could restrict the genome.
- **Ibarra, Pérez-Jiménez, Yokomori (2010)**, "On spiking neural P systems", *Natural Computing*. https://link.springer.com/article/10.1007/s11047-009-9159-3 — SN P modules with input and output neurons; a formal interface for composing modules.
- P-Lingua/MeCoSim, CuSNP and WebSnapse specify and simulate systems; none compiles programs or assembles systems from a module library.

Evolving P systems with modules:
- **Cao, Romero-Campero, Heeb, Cámara, Krasnogor (2010)**, "Evolving cell models for systems and synthetic biology", *Systems and Synthetic Biology* 4(1):55-84. https://pmc.ncbi.nlm.nih.gov/articles/PMC2816226/ — Nested EA: structure from a hand-built module library, parameters by an inner GA. Closest precedent for 6; our modules are discovered instead.
- **Huang, Zhang, Rong, Ipate (2011)**, "Evolutionary design of a simple membrane system", CMC 2011, LNCS 7184. https://link.springer.com/chapter/10.1007/978-3-642-28024-5_14 — Fixed rule pool, no modules.
- **Paul, Sosík, Ciencialová (2024)**, survey of learning in SN P systems, *Natural Computing*. https://arxiv.org/abs/2403.18609 — Up-to-date survey to cite. *Abstract.*

Modularity and library learning:
- **Koza (1994)**, *Genetic Programming II*, MIT Press — Automatically defined functions.
- **Angeline, Pollack (1993)**, "Evolutionary module acquisition", Proc. 2nd Conf. on Evolutionary Programming, 154-163 — Compress and expand mutations that freeze subtrees into a library.
- **Rosca, Ballard (1996)**, "Discovery of subroutines in genetic programming" (adaptive representation through learning), *Advances in GP 2*. https://cdn.aaai.org/Symposia/Fall/1995/FS-95-01/FS95-01-011.pdf — New subroutines from the parts of a child that beat its parents; unused ones are dropped. The model for harvesting modules on fitness jumps.
- **Ellis et al. (2021)**, "DreamCoder", PLDI. https://dspace.mit.edu/bitstream/handle/1721.1/145949/3453483.3454080.pdf; **Bowers et al. (2023)**, "Top-down synthesis for library learning" (Stitch), POPL. https://arxiv.org/abs/2211.16605; **Grand et al. (2024)**, "LILO", ICLR. https://arxiv.org/abs/2310.19791 — Solve, then compress solutions into a library.
- **Berlot-Attwell, Rudzicz, Si (2024)**, "Library learning doesn't", NeurIPS MATH-AI workshop. https://arxiv.org/abs/2410.20274 — Learned libraries were rarely reused. Measure reuse, not only fitness.
- **Reisinger, Stanley, Miikkulainen (2004)**, "Evolving reusable neural modules", GECCO, LNCS 3103. https://link.springer.com/chapter/10.1007/978-3-540-24855-2_7; **Miikkulainen et al. (2017)**, CoDeepNEAT. https://arxiv.org/abs/1703.00548 — Modules and blueprints co-evolved.
- **Kashtan, Alon (2005)**, "Spontaneous evolution of modularity and network motifs", *PNAS* 102(39):13773. https://www.pnas.org/doi/10.1073/pnas.0503610102; **Clune, Mouret, Lipson (2013)**, "The evolutionary origins of modularity", *Proc. R. Soc. B*. https://arxiv.org/abs/1207.2743 — Changing goals and a connection cost make modularity emerge.

Giving new parts time to be wired in, and parts that start on a signal:
- **Stanley, Miikkulainen (2002)**, "Evolving neural networks through augmenting topologies" (NEAT), *Evolutionary Computation* 10(2):99-127. https://doi.org/10.1162/106365602320169811 — Speciation protects new structure until its weights are tuned. Incubation does the same for a network given a module copy: it competes only with other such networks for a while.
- **Hornby (2006)**, "ALPS: the age-layered population structure for reducing the problem of premature convergence", GECCO, 815-822. https://doi.org/10.1145/1143997.1144142 — Newcomers compete only with individuals of similar age. Another way to keep new material alive; a baseline for incubation.
- The ADD and SUB modules of Ionescu, Păun, Yokomori (2006) start when a spike reaches their instruction neuron and end by sending one to the next. Triggered side runs evolve parts with the same kind of interface: start on a spike from the host, then make the missing gaps.

Finding what is missing:
- **Krawiec, Liskowski (2015)**, "Automatic derivation of search objectives for test-based GP" (DOC), EuroGP, LNCS 9025:53-65. https://doi.org/10.1007/978-3-319-16501-1_5; **Krawiec (2016)**, *Behavioral Program Synthesis with Genetic Programming*, Springer. https://link.springer.com/book/10.1007/978-3-319-27565-9 — Per-test outcomes as a matrix; cases nothing passes show what is missing.
- **Helmuth, Spector, Matheson (2015)**, "Solving uncompromising problems with lexicase selection", *IEEE TEVC* 19(5):630-643. https://doi.org/10.1109/TEVC.2014.2362729 — Keeps specialists on different cases alive.
- **Li, Miikkulainen**, "Evolving multimodal behavior through subtask and switch neural networks" (ModNEAT), ALIFE 2014 *(venue unconfirmed)* — Adds a module when fitness stalls.
- **Moraglio, Krawiec, Johnson (2012)**, geometric semantic GP, PPSN XII; **Solar-Lezama et al. (2006)**, sketching, ASPLOS. https://doi.org/10.1145/1168857.1168907
- **Lehman et al. (2022)**, "Evolution through large models". https://arxiv.org/abs/2206.08896; **Romera-Paredes et al. (2024)**, FunSearch, *Nature* 625:468; **Novikov et al. (2025)**, AlphaEvolve. https://arxiv.org/abs/2506.13131 — An LLM as an optional mutation proposer.

Correct first, then smaller:
- **Schkufza, Sharma, Aiken (2013)**, "Stochastic superoptimization" (STOKE), ASPLOS. https://arxiv.org/abs/1211.0557 — Search from compiled code under a correctness-plus-cost objective.
- **Vasicek, Sekanina (2011)**, "Formal verification of candidate solutions for post-synthesis evolutionary optimization in evolvable hardware", *GPEM* 12:305-327. https://link.springer.com/article/10.1007/s10710-011-9132-7 — Start from a working circuit, keep only edits that stay correct, let size fall. The template for 7; the exhaustive engine can play the SAT checker.
- **Luke, Panait (2006)**, "A comparison of bloat control methods for GP", *Evolutionary Computation* 14(3):309-344 — Lexicographic parsimony as a baseline.

What was built for 7 and 9 (`compile`):
- Register programs are compiled with the Ionescu ADD/SUB modules written as standard rules, and checked against an interpreter that follows every choice. Evolving the program is the weak step. A (μ+λ) search over instruction lists gets stuck on programs that generate too much. Lexicase selection over the target numbers, plus a "nothing extra" check, fixes much of that (10 seeds each, lexicase against fittest-only parents): {2,4,6} found 10/10 against 3/10, {1,2,3,5,8,13} found 2/10 against 0/10. On the Fibonacci set up to 987, 9 of 10 seeds reached numbers up to 34, while without lexicase none got past 5. The programs found for larger sets hit the 16-instruction cap and list numbers rather than compute them. A hand-written 13-instruction Fibonacci program compiles and matches its interpreter.
- Textbook modules cannot give exact gap sequences: each instruction takes a fixed 2 to 4 steps, so a gap of 1 is impossible and larger gaps come out as a multiple of the value plus overhead. Gap sequences are compiled from a fitted recurrence instead. Each value is a pair of neurons holding 2v spikes. One neuron of the pair drains one unit per step into the pairs that store the next value; the other stays silent until the last unit, then starts the next pair, so the drains run back to back. A gap lasts exactly as long as the sum of the values drained in it.
- Shrinking compiled Fibonacci (15 neurons, 29 rules, size 1834) for 2000 generations reached 10 neurons and 18 rules (size 1208 to 1210) on 4 of 5 seeds, and 12 neurons on the fifth. Every shrunk network still gave the next 4 values of the recurrence, past the 16 it was checked on, so shrinking did not overfit here. For comparison, evolving from scratch with modules and lexicase (100 neurons, 2000 generations) solved at most 10 of the 16 values.

### Composing modules into machines

Design notes, 2026-10-05. Large Fibonacci gaps were out of reach because the search is scored only on the output gaps. To make gaps that keep growing, a network has to store two numbers, add them, wait that many steps and repeat, so the flat search has to find storage, addition, timing and control all at once. The aim here is to evolve those parts separately, verify each one, and then search over ways of wiring them together.

Where the current module system falls short of this:
- A module has no record of what it computes. `Module.Origin` is a string, so nothing can say that module 7 computes n+1.
- `ModuleEdits.Insert` wires each port to a random neuron, so chaining parts on purpose cannot be expressed.
- Side runs (`SequenceTask.Focus` and `Triggered`) evolve parts for a few output gaps. That is still matching the output, only a smaller piece of it.
- `ModuleLibrary.Add` removes duplicates by structure. Two networks that compute the same thing are kept as two modules.
- `MaxModuleNeurons = 24` caps every module, so a solved composition can never become a module itself.
- The readouts are `Output` (the gap between two spikes), `Halting` and `SpikeTrain`. Nothing reads the number of spikes a neuron holds, and no part says when its result is ready.

Ways to encode a number at a port:
- **Interval**: two spikes n steps apart. `FunctionTask` already uses this, and so do the hand-built adder, subtracter, multiplier and divider of Zeng et al. (2012).
- **Count**: n (or 2n) spikes held in a neuron. This is how the registers of Ionescu, Păun, Yokomori (2006) hold numbers.
- **Trigger**: one spike that means start or done, as in the ADD and SUB instruction neurons.

With the count encoding, addition costs nothing: two synapses into one neuron add their spikes. The hard parts are control (what runs when) and converting between counts and intervals. Turning a count into an interval is exactly what a gap of the output needs.

Fibonacci in these terms (a hand derivation, used as a test and not as a seed): keep registers A = F(k-1) and B = F(k). Each round, B is drained one unit per step. Each unit sends one spike to the output, one to a new A' and one to a new B'. A is drained into B' at the same time, and finishes first because A ≤ B. The output gap is then B, A' = B and B' = A + B, and the next round swaps the old and new registers. This is a short register-machine program, so contributions 7 and 8 meet here. Building it by hand from verified parts would show whether the composition genome can express the answer at all, and its size gives MAP-Elites a target to beat.

**Round timing (answered 2026-10-06).** A round cannot last exactly B steps. Between the done that starts a round and the done that ends it, the trigger passes through the part's start neuron, the step the store empties, and its done neuron. So a round that drains y spikes from the hand-built register or add part lasts y + 3 steps. The swap cannot overlap the drain, because no port says the drain is 3 steps from its end. The output timer cannot absorb the lag either, because it grows by 3 every round. What works is option 2, storing values lower: the banks hold A − 2 and B − 3. The done that starts a round also reaches, through one relay, the three count ports of the bank being loaded, which puts back the 1 spike A' lacks and the 2 that B' lacks. Then x' = y + 1 and y' = x + y + 2 hold A − 2 and B − 3 again for the next pair, and the round of y = B − 3 lasts exactly B steps. The first gaps 1, 1, 2 are shorter than any round, so a glue neuron preloaded like a register store fires them and then starts the first round with both banks empty. That round's gap is 3.

Trace of the round with gap 3 (steps 4 to 8) and the round with gap 5 (steps 8 to 13), from `FibonacciCompositionTests.TraceTheRoundsOfGapThreeAndFive`. Bank 1 is register X1 and add part Y1, bank 2 is X2 and Y2. G starts the first round, O is the output, and R1 and R2 are the relays that put the missing spikes into banks 1 and 2. Cells show the spikes a neuron holds at the start of the step, and * marks a neuron that fires. A store holds each unit as two spikes.

| step | G | O | Y1.start | Y1.store | Y1.done | R2 | X2.store | Y2.store | Y2.sum | Y2.done | R1 | X1.n | X1.store |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 2* | 2 |  |  |  |  |  |  |  |  |  |  |  |
| 5 |  | 1* | 1* |  |  | 1* |  |  |  |  |  |  |  |
| 6 |  |  |  | 1* |  |  |  |  |  |  |  |  | 1* |
| 7 |  |  |  |  | 2* |  | 2 | 4 |  |  |  |  |  |
| 8 |  | 1* |  |  |  |  | 2 | 4 |  |  | 1* |  |  |
| 9 |  |  |  |  |  |  | 3* | 5* |  |  |  | 1* |  |
| 10 |  |  |  | 4 |  |  | 1* | 3* | 1* | 1 |  |  | 2 |
| 11 |  |  |  | 4 |  |  |  | 1* | 1* | 1 |  | 1* | 2 |
| 12 |  |  |  | 8 |  |  |  |  | 2 | 2* |  | 1* | 4 |
| 13 |  | 1* | 1* | 10 |  | 1* |  |  |  |  |  |  | 6 |

O fires on steps 5, 8 and 13, so the gaps are 3 and 5. G fires on step 4, which starts bank 1 with both parts empty. Y1's done (step 7) ends that round in 3 steps. Through R2, G also put one spike into each of X2.n, Y2.a and Y2.b, so on step 8 bank 2 holds 1 (2 spikes) and 2 (4 spikes): A − 2 and B − 3 for A = 3, B = 5. Y2 then drains 2 units on steps 9 and 10, X2 drains 1, and Y2's done on step 12 starts bank 1 again on step 13. In that time bank 1 is loaded to 3 and 5 (6 and 10 spikes), which is A − 2 and B − 3 for the next pair (5, 8).

**Fibonacci by hand from library parts (done 2026-10-06).** `FibonacciCompositionTests` builds the network from 4 part copies, 2 registers and 2 add parts, joined by 10 typed wires (done to start, count out to count in), plus 5 glue neurons: the output, the preamble, the first-round trigger and the two relays. `PartWiring.Copies` and `PartWiring.Wires` recognise every copy and wire, so the build is in the composition genome's terms. The register is the hand-built one checked against the catalogue's register contract, and the add part is a hand-built register with a second count in-port (`ReferenceParts.Add`). Both pass their first-part contracts on the exhaustive engine. The evolved library has neither: no part with count ports was solved by `evolve-parts`. The network gives the 16-value Fibonacci `SequenceTask` exactly on the exhaustive engine (fitness 1) and keeps going to 2584, which is 17 gaps.

Size target for composition search and MAP-Elites: **27 neurons, 44 synapses, 42 rules** (6 distinct), from 22 neurons in parts and 5 glue neurons. For comparison, the recurrence compiler makes 15 neurons and shrinking reaches 10. The 12-neuron difference from the compiler is the price of start and done: each part spends a start neuron and a done neuron, and the register's out neuron filters the store's last pair.

A generic set of first parts, which are arithmetic and not specific to Fibonacci, so the default of automatic discovery still holds:
- delay and identity, and fan-out (copy a number to two ports);
- n+1, 2n and n1+n2, in both the interval and the count encoding;
- interval → count, and count → interval (a timer);
- zero test or compare (SUB-like, with two trigger outputs);
- a sequencer: a trigger in, then triggers out in order.

Each part is an ordinary task with typed ports. Evolution finds them, the exhaustive engine verifies them, and MAP-Elites makes them smaller. The paper should say which parts were given as goals.

The loop that keeps itself going:
1. **Solve**: evolve each open contract. The exhaustive engine verifies the result and MAP-Elites shrinks it.
2. **Store**: a module keeps its body, its contract, and its ports with their encodings. Duplicates are found by behaviour (the same results on the contract's inputs), so a smaller network that does the same thing replaces the larger one.
3. **Compose**: the genome is a graph of module instances, glue neurons, and wires between type-compatible ports, flattened into an SN P network to be scored. This is the blueprint level of ECGP and CoDeepNEAT. Its mutations add an instance, move a wire to another compatible port, add a glue neuron, or swap an instance for a smaller module with the same behaviour. Raw neuron-level mutation is still allowed on the glue.
4. **Promote**: a solved composition becomes a module with the target as its contract. It refers to its parts rather than copying them, so the size cap no longer applies, and modules can be built from modules.
5. **Propose**: when composition stalls, propose new contracts. They can come from the failing checks, as now, or from the shape of the target: finite differences or a fitted linear recurrence (gap_k = gap_{k-1} + gap_{k-2}) suggest which operations are missing. That works for any recurrent sequence, not only Fibonacci. An LLM proposer (FunSearch, AlphaEvolve) can be added as an option.

Timing: SN P systems are synchronous, so parts wired together go wrong if a part takes a step longer than expected. Two ways to handle it are a fixed, recorded latency for every part, or a start and done trigger on every part, as in the Ionescu modules. The trigger approach is the safer one, since a smaller replacement part with a different latency still fits. It costs a few neurons per part.

Toward arithmetic: n1+n2, then n1−n2, then n1·n2 (a counter loop of additions), then division and comparison. The hand-built systems of Zeng et al. (2012) and the evolved ones of Dong et al. (2023) give neuron counts to compare against. Measure reuse as well as success (Berlot-Attwell et al. 2024): how often promoted modules show up in later solutions.

Related work for this section:
- **Zeng, Song, Zhang, Pan (2012)**, "Performing four basic arithmetic operations with spiking neural P systems", *IEEE Trans. NanoBioscience* 11(4):366-374. https://www.researchgate.net/publication/230671821_Performing_Four_Basic_Arithmetic_Operations_With_Spiking_Neural_P_Systems — Hand-built adder, subtracter, multiplier and divider. Numbers are intervals between input spikes and the result is the interval between output spikes, the same as `FunctionTask`. *Abstract; the full text could not be opened (IEEE and ResearchGate refused), so its sizes in the comparison table are taken from Chen and Guo 2023.*
- **Gutiérrez-Naranjo, Leporati (2009)**, "First steps towards a CPU made of spiking neural P systems", *IJCCC* 4(3):244-252 — Arithmetic circuits built by hand from SN P parts. *Not read.*
- **Walker, Miller (2008)**, "The automatic acquisition, evolution and reuse of modules in Cartesian genetic programming" (ECGP), *IEEE TEVC* 12(4):397-417 — Modules acquired and reused during the run inside a graph genome. The closest model for step 3.
- **Krawiec, Wieloch (2009)**, "Functional modularity for genetic programming", GECCO. https://dl.acm.org/doi/10.1145/1569901.1570037 — Modules identified by what they compute, not by their structure. The model for removing duplicates by behaviour. *Title only.*

Searched with no relevant hits: modules, ADFs or library learning in SN P evolution; automated compilers from programs to SN P systems; evolutionary minimisation of SN P systems; an SN P system generating Fibonacci as output gaps. Chinese-language venues and CMC/BWMC proceedings are poorly indexed, so check again before claiming novelty.

Searched with no relevant hits: MAP-Elites or quality-diversity for P systems; SAT/SMT synthesis of SN P systems; automatic design of asynchronous or time-free SN P systems; simulators that compile regexes into periodic tables.

### Use cases and a practical path

Notes from 2026-10-05. The published applications fall into two groups:
- Some variants keep the SN P name but have real-valued neurons trained by gradient or tuned by experts. These are practical, but they are not the model we evolve.
- The rest are exact, integer, deterministic circuits like ours, and every one of them was designed by hand.

Nobody evolves the exact circuits, so that is the niche. The realistic practical claim is automatic design of small, verified spiking circuits for neuromorphic chips or FPGAs. General computing is out: no published work shows SN P beating conventional hardware at it.

| Area | What is published | Fit with this project |
|---|---|---|
| Fault diagnosis (power grids, locomotives, motors) | Fuzzy-reasoning SN P (FRSN P), built by hand from expert cause-and-effect rules and evaluated as matrix operations. | Poor. Spikes are fuzzy truth values, not counts. |
| Forecasting and NLP | Nonlinear SN P (NSNP) and LSTM-SNP are real-valued recurrent networks trained by gradient. | Poor. They share the name but not the model. |
| Image processing (skeletons, edges, segmentation) | Classic parallel algorithms, recoded by hand as SN P systems with weights and run on GPUs. | Partial. These are per-pixel local rules; a per-pixel module could be evolved. |
| Arithmetic | Adders, subtracters, multipliers and dividers, including time-free versions. A 2024 paper puts 64-bit circuits using communication on request onto a low-area FPGA. | Strong. These are exact targets with published sizes to beat. |
| Cryptography and security | ElGamal encryption built on SN P arithmetic (2025), and a 2024 review of SN P in cybersecurity. | Downstream: this needs arithmetic first. |
| Robot control | Enzymatic numerical SN P controllers for wall following, built test-first in Webots. | Partial. It needs streaming input ports and real-valued variables. |
| NP-hard problems | SAT and Subset Sum solved in polynomial time using neuron division, budding or plasticity. | Poor. The extra workspace grows exponentially, which no physical device provides. |

The closest neighbours lie outside membrane computing, in neuromorphic algorithm work:
- **Fugu** composes spiking-algorithm "bricks". Each brick declares how many input and output neurons it has, how long its input and output last, and its depth. It also has a control neuron that fires on completion, and Fugu pads parallel branches to equal depth or flushes buffers with control neurons. This is our start/done contract, already in use, but its bricks are hand-written scripts. So the claim in the table above narrows from "composition is new" to "evolving parts with contracts and composing them automatically is new". Fugu has to be cited in the composition section.
- **Adders on Loihi 2** (2025) take all n bits of each operand at once, one input neuron per bit; they do not stream one bit per step. Their "sequential" adder takes n + 1 steps, and the parallel ones take 2 or 3 steps but need about n² or n√n synapses. The bit-serial adder they cite from Aimone et al. (4 neurons, 9 synapses, one bit per step, least significant first) is the baseline for our `Binary` port kind. Their neuron and synapse counts are a baseline for evolved adders.
- **Shortest paths with spikes** (SPAA 2020): the time until the first spike encodes the distance, the same idea as our interval encoding. They prove a polynomial speed-up over conventional algorithms for the k-hop version. This is the one place where spike timing is the answer and also an advantage.
- **NIR** (Nature Communications 2024) is a shared intermediate representation that runs on 7 simulators and 4 digital hardware platforms. Its primitives are leaky integrate-and-fire style neurons, so our regex rules do not map onto it directly.
- **Loihi 2 microcode** allows nearly any discrete-time neuron model, and its graded spikes carry an integer payload of up to 32 bits. Matching a `SpikeCondition` is a modulo plus a table lookup (a tail followed by a repeating period), and `Produce` > 1 could ride on a graded spike. That makes running our neurons on Loihi 2 plausible, but it is not demonstrated.

#### Evolved against hand-designed arithmetic

Composition search solved n1 x n2 in count encoding by reusing an add loop promoted from hand-built parts: 10 of 20 runs within 6000 evaluations, against 0 of 20 with the same parts but no add loop (Fisher's exact p = 4e-4). That is with the add loop fixed after the bounded check found it wrong past a = 13 (see Proving contracts). The broken loop solved 14 of 20, and the difference is all in tournament selection, 3 of 10 against 7 (p = 0.18). The add loop was in 18 of the 20 best networks, which is the reuse Berlot-Attwell et al. (2024) found missing from learned libraries. See the README, Composing machines from parts. Nothing evolved has solved a binary contract yet, and no published circuit has been rebuilt as a network to run through our contracts, since none of the SN P papers could be opened. Rows from the same paper compare like with like only within an encoding: a unary circuit costs time in the value, a binary one in the width.

| Circuit | Encoding, range | Neurons | Synapses | Rules | Steps | Source |
|---|---|---|---|---|---|---|
| Zeng et al. 2012 adder / subtracter / multiplier / divider | interval (unary), several inputs, naturals | 10 / 12 / 21 / 25 | not given | 4 / 4 / 12 / 15 rule types | not given | Second-hand: Chen and Guo 2023, Table 5 (p. 28). Their Section 2.2 (p. 5) gives 22 and 24 for the multiplier and divider instead. Zeng's paper was not opened. |
| Liu et al. 2015, time-free adder / subtracter / multiplier / divider | time-free, several inputs | 2 / 2 / 11 / 10 | not given | 2 / 6 / 15 / 16 rule types | none (time-free) | Second-hand: Chen and Guo 2023, Table 5 (p. 28). Not opened. |
| Chen and Guo 2023 adder / subtracter / multiplier / divider | binary spike train, one input, k bits | k+8 / k+13 / 3k+8 / 5k+12 (12 / 17 / 20 / 32 at k = 4) | not given | 6 / 11 / 9 / 29 rule types | 2k+4 / 2k+3 / 3k+5 / 4k+q+4 | Chen and Guo 2023, Table 5 (p. 28); the contributions list (p. 3) gives 2k+q+4 for division. |
| von Seeler et al. 2025, sequential adder | binary, all n bits at once, n <= 62 | 2n | 7n-2 | LIF threshold gates | n+1 | von Seeler et al. 2025, Table I (p. 5). Theoretical counts. |
| von Seeler et al. 2025, DCTA2 / DCTA3 parallel adders | binary, all bits at once, n <= 16 / 42 | 2n / 4n | n²+5n-1 / 3n√n+7n-1 | LIF threshold gates | 2 / 3 | Same, Table I and footnote b. |
| Aimone et al. streaming adder, as cited by von Seeler | binary, one bit per step | 4 | 9 | LIF threshold gates | n+1 | von Seeler et al. 2025, Table I. |
| Dong, Luo, Zhang 2023, evolved | – | – | – | – | – | Not opened (paywalled), so no numbers. |
| Ours: hand-built add | count (unary), cases up to 12 + 5 | 6 | 5 | 9 (6 distinct) | n1 + n2 + 2 (19 for 12 + 5) | `ReferenceParts.Add`, `HardwareCost`. |
| Ours: add loop a + n x b, hand-built from 7 parts and 10 glue neurons | count, cases up to 6 x 5, proven up to 30 | 49 | 62 | 79 (15 distinct) | 223 for 2 + 6 x 5 | `HandBuiltMachines.AddLoop`. |
| Ours: n1 x n2 found by composition search, one add loop and glue | count, cases up to 6 x 5, proven up to 12 | 56 | 68 | 86 (16 distinct) | 261 for 6 x 5 | `compose --task "Contract multiply" --hand-built on --seed 1`. |

Notes:
- "Rule types" in Chen and Guo count a rule such as a -> a once however many neurons use it, which is close to our distinct rules.
- None of the SN P papers give synapse counts; they would have to be counted from each paper's figures.
- Our multiplier is about twice Zeng's 21 neurons, because it is built from general parts with start and done triggers rather than designed as one circuit. Shrinking it, by swapping in smaller parts or evolving the glue, is the next comparison to make.

What to build, in order of payoff:
1. **Binary port encoding**: one bit per step, a spike for 1 and silence for 0. Interval and count are both unary, so a k-bit number costs up to 2^k steps or spikes. The hardware and SN P arithmetic papers all use bits, and any practical arithmetic needs them. Add it as a fourth `PortKind` in build PR 1.
2. **Published circuits as benchmark tasks**: the adder, subtracter, multiplier and divider of Zeng 2012, the time-free versions, and the Loihi 2 serial adder. Report neurons, synapses, rules and steps, evolved against hand-designed. This gives the paper a clean comparison table. Fits build PR 6.
3. **Hardware cost as an objective**: neurons, synapses, distinct rules, the most spikes any neuron holds (register width) and lasso table size. Add these as MAP-Elites dimensions or tie-breakers.
4. **Hardware profile**: an option that limits rules to threshold-and-reset forms (`a^{≥k}`, consuming everything, sending one spike). Networks evolved under it are integrate-and-fire networks and can be exported to NIR or Lava. It costs expressiveness, so measure how much harder tasks get with it on.
5. **Exporters**, checked by co-simulation: replay each export against our engine and require the spike traces to match bit for bit.
   - Verilog for any network: one module per neuron, built from a spike counter, the lasso table, a delay counter and a closed flag.
   - NIR or Lava for networks built under the hardware profile.
6. **Formal check of contracts**: SN P systems have been translated to Uppaal timed automata, PRISM, Petri nets and kernel P systems. First, a bounded check in the exhaustive engine: done fires exactly once and the system returns to its initial state, for every input up to a bound. A model-checker export can come later.
7. **Streaming tasks**: sensor spikes in and actuator spikes out, without start or done. Examples are a debouncer, a rate detector, or a small wall-follower like the ENSNP robot controllers. This is a different task family from the arithmetic one.
8. **Timing robustness**: score parts under random extra delays, following time-free SN P. Asynchronous hardware jitters, and a part that only works under lock-step timing breaks there. The start/done triggers already go some way towards this.

Items 1 and 3 belong in the existing build PRs; items 4 to 6 would be new PRs after build PR 6.

#### Hardware profile and exporters

Items 4 and 5 are built (`HardwareProfile`, `Export/`, README "Exporting to hardware"). Notes from 2026-10-06:

- **Delays.** Neither of our delay semantics is an axonal delay. A delayed standard rule closes its neuron, so spikes sent to it while it waits are lost. A delayed legacy rule sends at once and then holds the neuron. Integrate-and-fire hardware resets and keeps integrating while the spike is in flight. So rules gained a third, axonal kind of delay (`Rule.Axonal`): consume now, stay open, deliver d steps later. The profile's delays are all axonal, and they map onto NIR `Delay` nodes and Loihi's synaptic delays one to one.
- **The profile.** One rule per neuron, `a^{>=k} / a^*` firing or forgetting, no initial spikes (NIR has no initial state), axonal delays only. One threshold per neuron is forced anyway: two consume-everything rules both apply from the larger threshold up, so they would be a nondeterministic choice.
- **Time step.** NIR is defined in continuous time and leaves the step to the backend, so the step is part of the claim. We fix one SN P step as one time step, dt = 1. A spike sent on step t is integrated on step t + 1, which is the one-step latency a discrete NIR backend gives a recurrent connection, so every synapse is the recurrent edge `neurons -> [Delay] -> w_rec -> neurons`. The environment's spike on step t is presented at time step t + 1, through `Input -> w_in`. NIR's IF fires when v > v_threshold, so threshold k is written as k - 0.5, and reset is to 0. Under this mapping the IF neuron spikes on step t exactly when the SN P neuron applies its rule on step t. Its potential after the step equals what the SN P neuron holds after applying its rule, and the co-simulation checks both on every step. An axonal delay d is a `Delay` of d time steps, arriving on t + 1 + d.
- **Simulators.** snnTorch's NIR import maps `IF` to a leaky neuron with beta 0.9 and supports only one threshold per layer, so it cannot reproduce integer IF exactly. norse's `IAFCell` integrates without leak and fires on v > v_th, and nirtorch's executor feeds a recurrent edge the previous step's output. norse has no `Delay`, so `tools/snp_nir.py` supplies a shift register for it.
- **Verilog.** Every deterministic network exports, in either rule form and with every kind of delay. Co-simulation under Icarus Verilog matches `NetworkSimulation` on the library's evolved delay 2, the hand-built register, add and increment (every contract case), and 25 random deterministic networks with holding, closing and axonal delays. It compares what every neuron sends and holds on every step.
- **What the profile costs.** `evolve-parts` on the 14 first-part contracts, seeds 1 to 5, default budget of 50000 evaluations per contract, with and without `--profile hardware`:

| Contract | Unrestricted | Profile |
|---|---|---|
| delay 1 / 2 / 3 / 4 | 4 / 5 / 5 / 5 of 5 | 5 / 5 / 5 / 5 of 5 |
| sequencer 2 / 3 | 4 / 0 of 5 | 3 / 1 of 5 |
| fan-out, increment, double, add, interval to count, count to interval, register, zero test | 0 of 5 each | 0 of 5 each |

  On timing parts the profile costs nothing measurable and is if anything easier. It solved 24 of the 30 timing-part runs against 23, and the delays took a median of 180 evaluations against 360. Its search space is far smaller, and a profile seed ran in about 2 minutes against roughly an hour unrestricted. No count-port part was solved either way. The best fitness was about 0.8 in both, and 0.975 on the zero test, so on these contracts the profile's cost cannot yet be told apart from the search's. It may still be structural: a neuron that resets to zero cannot hold a count and release it one spike at a time, two spikes arriving together merge into one, and SN P has no inhibition to gate a stored count. So unary count parts may be out of reach of profile networks altogether. If so, the NIR route suits timing parts and, later, binary or interval encodings. The NIR done-when therefore uses the evolved profile delay and sequencer (`parts-profile/`); there is no profile add.

Sources for this section:
- **Aimone, Severa, Vineyard (2019)**, "Composing neural algorithms with Fugu", ICONS. https://arxiv.org/abs/1905.12130 — Bricks with declared sizes and timing, a control neuron that fires on completion, and a NetworkX graph as output. *HTML read.*
- **von Seeler, Offenberg, Michaelis, Luboeinski, Lehr, Tetzlaff (2025)**, "Adding numbers with spiking neural circuits on neuromorphic hardware", *Neuromorph. Comput. Eng.* https://arxiv.org/abs/2503.10387 — Sequential and parallel adders in Lava on Loihi 2, with all input bits at once; Table I gives theoretical neuron and synapse counts, and Figure 5 the resources measured on the chip. *Full text read (arXiv v2).*
- **Chen, Guo (2023)**, "Spiking Neural P Systems for Basic Arithmetic Operations", *Appl. Sci.* 13(14):8556. https://doi.org/10.3390/app13148556 — Binary adder, subtracter, multiplier and divider with one input neuron, and Table 5 comparing neurons, time and rule types with Zeng 2012, Liu 2015 and others. *Full text read.*
- **Liu, Li, Liu, Liu, Zeng (2015)**, "Implementation of Arithmetic Operations with Time-Free Spiking Neural P Systems", *IEEE Trans. NanoBioscience* 14:617-624 — *Not opened; sizes taken from Chen and Guo 2023, Table 5.*
- **Aimone et al. (2020)**, "Provable neuromorphic advantages for computing shortest paths", SPAA. https://www.osti.gov/servlets/purl/1808434 — Distance encoded as the time to first spike. *Title and summary.*
- **Pedersen et al. (2024)**, "Neuromorphic intermediate representation", *Nat. Commun.* 15:8122. https://arxiv.org/abs/2311.14641 — *Abstract.*
- **Peng, Wang, Pérez-Jiménez et al. (2013)**, "Fuzzy reasoning spiking neural P system for fault diagnosis", *Information Sciences*. https://www.sciencedirect.com/science/article/abs/pii/S0020025512004793 — *Title only.*
- **Liu, Long, Peng et al. (2021)**, "LSTM-SNP", and **Long, Liu, Peng et al. (2022)**, NSNP multivariate forecasting, *Knowledge-Based Systems* and *Neural Networks*. https://www.sciencedirect.com/science/article/abs/pii/S0950705121009187 — *Abstract.*
- "New high-speed arithmetic circuits based on SN P systems with communication on request implemented in a low-area FPGA", *Mathematics* 12(22):3472 (2024). https://doi.org/10.3390/math12223472 — *Title and search summary; the page returned 403.*
- "First ElGamal encryption/decryption scheme based on SN P systems…", *Mathematics* 13(9):1366 (2025). https://doi.org/10.3390/math13091366, and "Applications of spiking neural P systems in cybersecurity", *J. Membrane Computing* (2024). https://link.springer.com/article/10.1007/s41965-024-00166-9 — *Title only.*
- "Enzymatic numerical spiking neural membrane systems and their application in designing membrane controllers", *IJNS* (2022). https://pubmed.ncbi.nlm.nih.gov/36254796 — *Title only.*
- "Towards a general methodology for formal verification on spiking neural P systems", *TCS* (2024), and "Modelling and verification of weighted spiking neural systems" (Uppaal), *TCS* (2016) — see the sources under Proving contracts.
- Hernández-Tello, Martínez-del-Amor, Orellana-Martín, Cabarle (2024), "Sparse spiking neural-like membrane systems on GPUs". https://arxiv.org/abs/2408.04343 — For comparison with our Metal engine. *Abstract.*
- Two books from 2024 to read for wider coverage: *Spiking Neural P Systems: Theory, Applications and Implementations* and *Advanced Spiking Neural P Systems: Models and Applications* (Springer). Also the 2025 narrative review in *J. Membrane Computing*: https://link.springer.com/article/10.1007/s41965-025-00206-y (paywalled).

#### Proving contracts

Item 6, first half, is built (`BoundedCheck`, `Specifications`, the `verify` command; README "Proving parts past their cases"). Notes from 2026-10-06:

- **What is proven.** For each N, every input whose values are all at most N meets the contract's four rules, with the latency the specification allows at N, on every computation the exhaustive engine follows. A case it cannot follow exactly ends the check unproven. The rules watch a window after done, so "done fires once" means once within that window. A proof for all time would need the run to end in the initial configuration with nothing in flight and no rule applicable, which the configuration graph below could show.
- **Specifications.** Contracts are stored as cases, so the function behind each known contract is written out once more in `Specifications` and checked against the contract's cases before use. The latency allowed at N follows the formula the contract's own latency came from (3N + 4 for most count parts), because a unary part's time grows with its value and a fixed latency would fail every part at some N.
- **Results.** Every part in `parts/` and `parts-profile/` is proven for every input, as none has data in-ports. In 20 seconds each, the hand-built register reached N = 1662, add 193, fan-out 1424, gate 801, decrement 1602, increment 1659 and zero test 2181. The hand-built add loop failed at a = 14, b = 0, n = 0: the zero test sent done while the accumulator was still draining a into the sum, so the last spike of sum came a step after done. Its cases only use a = 0 and 2. This is the failure the epic warned about. A part passes its cases but fails at a value nobody tested, and the n1 x n2 composition is built on it. The multiply cases always have a = 0, so the multiplier was not wrong, but the add loop's contract was wrong past a = 13. Done now joins the loop's finish with the accumulator's own done (one glue rule `aa -> a` and one more synapse), and the fixed loop is proven up to 30 in 120 seconds.
- **Admission.** Parts from evolve-parts, promotion and proposals are checked up to twice the largest value in their cases before they join the library. That would not have caught the add loop: its cases go up to 6, so it is checked to 12 and fails at 14.

**Model checking (item 6, second half).** The ticket asks to follow the published translation of SN P systems into Uppaal timed automata, and to state first what it covers.

- *Pérez-Jiménez, Valencia-Cabrera, Orellana-Martín, Ramírez-de-Arellano (2024)* is not a model-checker translation. It is a method for proving SN P systems correct by hand. Build the computation tree, merge nodes with equal configurations into an oriented graph, read each computation as a path through that graph's loops, and prove invariants about the loops by induction. It works on two generators from Păun, Pérez-Jiménez and Rozenberg (2006), with standard rules `E/a^c -> a; d`, closing delays and forgetting rules `a^s -> λ`. Our exhaustive engine already merges equal configurations, so the graph is close to what it builds. The method is still by hand, and for a part it would prove one input at a time unless the loop structure is parameterised in n.
- *Aman and Ciobanu (2016)* translates "SN P systems with weighted synapses" into timed safety automata for Uppaal, with a proof that the translation is correct. Its full text could not be opened: ScienceDirect and the Elsevier API return 403, and no preprint was found. The abstract does not say which rule forms it covers. If its variant is that of Pan et al. (2012), which keeps standard rules with consumption, forgetting and delays and adds synapse weights, it would cover our standard rules with weights of 1. That is unconfirmed. It also says nothing about our legacy delay (fire, then hold) or the axonal delay.
- **Gaps known so far.** Our rules have `Produce` > 1, which a synapse weight models only when every synapse from the neuron has the same weight. Our legacy and axonal delays are not standard SN P semantics. A part is a system with an environment, the encoded inputs and the start trigger, which the 2024 paper's systems, both generators, do not have.
- **Status.** Since the 2016 paper could not be read, we wrote our own translation of our engine's step (`Export/UppaalExporter.cs`, `export-uppaal`). A Clock automaton picks a contract case, then steps time in two phases. On release it broadcasts, and each neuron automaton takes one edge: one per applicable rule (a nondeterministic choice when several apply), a busy edge while a closing delay runs, or an idle edge. Each edge changes only that neuron's variables. deliver() then sends spikes and input and records the ports as PortRecorder does. The run ends where the exhaustive engine's does. Queries cover quiet before start, done once with the right outputs, back to start and on time. For a deterministic part, one more query checks every neuron's spikes on every step against our engine. Legacy rules with a delay and binary out-ports are refused. **It has not been run under Uppaal yet** (no licence installed), so the model may not parse, and the done-when for #44 is open. The tests that need verifyta skip.

Sources for this section:
- **Pérez-Jiménez, Valencia-Cabrera, Orellana-Martín, Ramírez-de-Arellano (2024)**, "Towards a general methodology for formal verification on spiking neural P systems", *TCS* 1011:114705. https://doi.org/10.1016/j.tcs.2024.114705 (open access, https://hdl.handle.net/11441/173302) — *Full text read.*
- **Aman, Ciobanu (2016)**, "Modelling and verification of weighted spiking neural systems", *TCS*. https://doi.org/10.1016/j.tcs.2015.11.005 — *Abstract only; paywalled, 403.*
- **Pan et al. (2012)**, "Spiking neural P systems with weighted synapses" — *Not opened or checked; named only as the likely variant.*

### Toward general synthesis

Notes from 2026-10-06, after M1 and M2. Every complex result so far rests on three things a general method cannot depend on:

- **No count-port part has been evolved.** n1 x n2 was solved only with the hand-built parts and the hand-built add loop; with `--hand-built leaves` no run solved it. Automatic design stops at delays and sequencers.
- **Control is found one rewired port at a time.** Composition search finds a loop only when a loop part is already in the library, and Fibonacci from parts was wired by hand, never found.
- **Everything is unary.** A k-bit number costs up to 2^k steps or spikes, and no binary contract has been solved.

What would address them, in order:

1. **Control primitives as first parts.** Join (done after both starts), fork, merge and select (route start by a branch). The add loop's fix was a hand-made join (`aa -> a`). They are timing parts, which `evolve-parts` does solve, and they fit the hardware profile. They give composition and the lowering in item 3 the words for control.
2. **Count parts compiled, not hand-built.** Search for a register program that computes a contract's function on its cases, compile it with the Ionescu ADD and SUB modules (which already start on a spike and end by sending one), drain the 2n-spike result onto the count out-port, verify, then shrink with MAP-Elites. This is compile-then-shrink (contribution 7) aimed at parts, and it removes the hand-built seed. The paper should say the textbook modules come from the compiler.
3. **Programs of parts.** A genome that is a short program whose instructions call library parts by contract on named registers, with branches on the zero test. It is scored by an interpreter that runs each part's `Specification`, as `ProgramSearch` scores register programs without simulating them, and only the winner is lowered to a `Composition`, verified and promoted. This generalises the register-machine compiler from ADD and SUB to any verified part. Multiply, divide and Fibonacci become short programs rather than wiring puzzles. Flat composition search stays for shrinking and glue.
4. **Counterexamples back into the search.** A counterexample from `BoundedCheck` becomes a case the search must pass (CEGIS, Solar-Lezama et al. 2006). Admission up to twice the largest case would not have caught the add loop.
5. **Proofs from the parts' contracts.** Prove a composition from its children's proven bounds and a check that every part is used as its contract assumes (started only when idle, inputs within its bound, read only through ports), rather than flattening it onto the exhaustive engine. The add loop flattened reached N = 30 in 120 seconds, so deeper hierarchies cannot be proven the current way.
6. **A bit-serial adder.** Aimone et al.'s streaming adder (4 neurons, 9 synapses) is the target. The `Binary` port kind is already serial, least significant bit first.
7. **Parameterised part families and library compression.** One contract per delay k or add k does not scale; and promoted recipes may share sub-compositions worth promoting (Stitch, DreamCoder).
8. **Targets that show depth.** Fibonacci from parts with nothing hand-wired, subtraction, division and comparison in count encoding, and a third level of promotion such as n^k on multiply on the add loop, with reuse reported at each level.

These are milestone M3 in `.github/tickets/m3/`.

## Next steps

- Read the full text of Dong 2023 and Zeng 2012, which were paywalled when the comparison table was made, and check Zeng's multiplier and divider sizes, which Chen and Guo give two ways.
- Contact co-authors (Zhang, Dong, Paul, Cavaliere) about the follow-up.
- Run the full benchmark: ~30 seeds per algorithm and task, Mann–Whitney tests with effect sizes, and the legacy GA as baseline.
- Likely venues: *J. Membrane Computing* or CMC; GECCO or EvoStar if the paper leans on the algorithm comparison.
