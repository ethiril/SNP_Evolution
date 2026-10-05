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

Searched with no relevant hits: MAP-Elites or quality-diversity for P systems; SAT/SMT synthesis of SN P systems; automatic design of asynchronous or time-free SN P systems; simulators that compile regexes into periodic tables.

## Next steps

- Read the full text of Dong 2023.
- Contact co-authors (Zhang, Dong, Paul, Cavaliere) about the follow-up.
- Run the full benchmark: ~30 seeds per algorithm and task, Mann–Whitney tests with effect sizes, and the legacy GA as baseline.
- Likely venues: *J. Membrane Computing* or CMC; GECCO or EvoStar if the paper leans on the algorithm comparison.
