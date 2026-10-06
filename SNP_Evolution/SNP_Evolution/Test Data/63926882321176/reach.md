Target 1,1,2,3,5,8,13,21,34,55,89,144,233,377,610,987,..., 1 seeds per setup, 5,000 evaluations per run. The part library cost 74,433 evaluations to evolve, not charged to any run.

| Setup | Median reach | Mean reach | Min-max | Median neurons | Median evaluations | Median wall time (s) |
|---|---|---|---|---|---|---|
| flat (MAP-Elites over network size, no modules) | 3 | 3.0 | 3-3 | 22 | 5028 | 6 |
| modules (MAP-Elites with the modular loop and lexicase parents) | 5 | 5.0 | 5-5 | 45 | 5464 | 8 |
| composition (composition search with MAP-Elites, from the part library) | 2 | 2.0 | 2-2 | 16 | 5000 | 4 |

| Comparison of reach | U | p (two-sided) | A12 |
|---|---|---|---|
| modules vs flat | 1 | 1 | 1.00 |
| composition vs flat | 0 | 1 | 0.00 |
| composition vs modules | 0 | 1 | 0.00 |

A12 is the chance a run of the first setup reaches further than one of the second; 0.5 is no difference.

Command: `dotnet run -- reach --target "1,1,2,3,5,8,13,21,34,55,89,144,233,377,610,987" --evaluations 5000 --seeds 1 --neurons 100 --charge-parts off`
