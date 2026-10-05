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

Searched with no relevant hits: modules, ADFs or library learning in SN P evolution; automated compilers from programs to SN P systems; evolutionary minimisation of SN P systems; an SN P system generating Fibonacci as output gaps. Chinese-language venues and CMC/BWMC proceedings are poorly indexed, so check again before claiming novelty.

Searched with no relevant hits: MAP-Elites or quality-diversity for P systems; SAT/SMT synthesis of SN P systems; automatic design of asynchronous or time-free SN P systems; simulators that compile regexes into periodic tables.

## Next steps

- Read the full text of Dong 2023.
- Contact co-authors (Zhang, Dong, Paul, Cavaliere) about the follow-up.
- Run the full benchmark: ~30 seeds per algorithm and task, Mann–Whitney tests with effect sizes, and the legacy GA as baseline.
- Likely venues: *J. Membrane Computing* or CMC; GECCO or EvoStar if the paper leans on the algorithm comparison.
