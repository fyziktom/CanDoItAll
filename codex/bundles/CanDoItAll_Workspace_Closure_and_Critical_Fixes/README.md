# Workspace closure and critical fixes

Execute [prompt.md](prompt.md) on the current checkout. This is a closure assignment, not a new module extraction.

The reviewed application is `d9273a88973d28d73c4d29f8686f2b3d68ef8f3a` on `components-decoupling`.
The remaining Workspace renderers were extracted. Shared-shell failures, live proof gaps and
an additional exact-profile mutation race prevent declaring application readiness.

| Read | Purpose |
|---|---|
| [Review](REVIEW.md) and [test conclusions](TEST_RESULTS_REVIEW.md) | Separate verified source paths, implementer reports, missing evidence and new findings |
| [Data Sources correction](FIX_DATA_SOURCES.md) | WCL-R1: exact edit must not resurrect a concurrently deleted profile |
| [Shared lifetime corrections](FIX_SHARED_LIFETIMES.md) | WC-C1, WC-C2 and WC-C3, with controlled ordering and negative tests |
| [Dependency delivery](DEPENDENCY_DELIVERY.md) | Scoped BaseLib change and reproducible application/Components pair |
| [Harness and live verification](LIVE_VALIDATION.md) | WC-L1/WC-L2, exact approval targets, retained evidence and exhausted budget |
| [Validation matrix](VALIDATION_MATRIX.md) | Targeted, full Stable, whole browser sequence and application integration |
| [Safe execution](SAFE_EXECUTION.md) | Isolated state, secrets, costs, signing and cleanup |
| [Closure rules](CLOSURE.md) | What may be called complete and when the next module is permitted |
| [Workspace census](WORKSPACE_STATUS.md) | Keep the new leaf boundaries; do not repeat the extraction |
| [Sources](SOURCES.md) | Pinned source paths and precise review coverage |

The original 29 campaign groups remain obligations, with six closure-specific groups.
[closure-plan.json](closure-plan.json) contains their inventory; it does not claim execution.
Use an external writable copy of [the result template](templates/closure-results.json).
Keep the delivered handoff and its shared foundation sealed.

The previous forty live reservations are exhausted. This file and its main prompt do not
renew that budget. Complete non-live work; start new live calls only after explicit operator
authorization recorded as described in LIVE_VALIDATION. Missing authorization is not a code
failure, but it cannot pass required live proof.

Shared foundation: [shared/README.md](shared/README.md), copied byte-for-byte from the supplied
previous package. Current repository instructions remain authoritative. A historical module
map in shared v3 is provenance, not today's completion census.
