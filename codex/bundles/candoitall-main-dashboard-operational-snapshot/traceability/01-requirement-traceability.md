# Requirement Traceability

| Input / requirements | Bundle destination | Owner | Planned proof | Closure path |
| --- | --- | --- | --- | --- |
| `N001`, `R001`, `R011`, `R012` | SB02 | SB02 | QuickActionCard/Home bUnit + route/keyboard Playwright | Execution report SB02 and raw-note rows |
| `N002`, `R002` | SB01 project query | SB01 | >5/equal-time relational test; source no-tracking/bound assertion | SB01 Behavioral evidence |
| `N003`, `O001`, `R003` | Workflow dedicated query/store | SB01 | Mixed active/terminal, no-active fallback, max-five, aggregate store untouched | SB01 Behavioral evidence |
| `N004`, `R004` | Process dashboard query and shared tab | SB01 data; SB02 UI | Active/fallback/budget/no-enrichment test plus tab DOM/browser | SB01/SB02 evidence |
| `N005`, `O002`, `R005`, `R016` | Agent totals query and header stats | SB01 data; SB02 UI | Exact projection-copy/exception test; labels/values DOM | SB01/SB02 evidence |
| `N006`, `N008`, `N010`, `R006`–`R008` | Snapshot loader/cache/key | SB01 | Fake time/key/concurrency/force/fault/cancel tests | SB01 evidence and AC01 |
| `N007`, `R010` | Home timer/countdown/force | SB02 | Fake-time expiry, reset, disposal component tests; browser countdown | SB02/SB03 evidence |
| `N009`, `R014` | Architecture artifacts/gates | SB01–SB03 | `.csproj` negative diff, source assertions, direct tests, build | AC01–AC03 |
| `N011`, `R009`, `R017` | Explicit load/refresh errors | SB01 service; SB02 states | Throwing fake + prior snapshot proves visible stale/error, not empty | SB01/SB02 evidence |
| `N013`, `R015` | Bounds/cadence/no enrichment | SB01; independent audit SB03 | Counting fakes/interceptors, fake time, source/query inspection | SB01/SB03 evidence |
| `N014` | Exactly three Behavioral subbundles | Root/plan/subbundles | Prepared/completed validator | Root validation summary |
| `N015`, `R019` | C# architecture overlay | architecture/plan/review | AC00–AC03 and architecture gate | SB03 final gate |
| `N016` | Literal input and portable references | inputs/entire bundle | Prepared validator + manual literal comparison | Preparation review |
| `N017`, `R013` | 1440x900/page-scroll/no overlay | SB02/SB03 | DOM bounds/scroll metrics + four screenshots | Browser analytics |
| `N018`, `R018` | Semantic proof/traceability/reopen/status | reviews/subbundles | Completed report fields + completed validator | Final closure |
| `N019` | CodeAnalytics unavailable | analysis/architecture/review | Retry log or manual dependency proof | AC03; no false clean claim |

All normalized requirements R001–R019 have an owner and proof above. Detailed acceptance remains authoritative in `bundle://requirements/01-normalized-requirements.md`.

## Current Progression

- SB01 closed the bounded data, cache, coalescing, failure, and lifetime requirements; AC01 is approved.
- SB02 closed quick actions, tabs, totals, explicit states, countdown, force refresh, and disposal through 7/7 deterministic component tests; AC02 is approved.
- SB03 closed the independent performance, manual dependency, Release solution build, migration, and real-browser proof; AC03 is approved. CodeAnalytics itself remained unavailable, with its manual replacement evidence recorded under `N019`.
