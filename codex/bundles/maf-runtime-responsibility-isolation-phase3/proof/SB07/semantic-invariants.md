# SB07 Semantic Invariants

| ID | Invariant | Proof |
| --- | --- | --- |
| SB07-INV-01 | Extracted services are production-wired through DI. | DI smoke |
| SB07-INV-02 | Core behavior does not service-locate dependencies. | Source assertion |
| SB07-INV-03 | Project references are acyclic. | Dependency graph proof |
| SB07-INV-04 | New classes do not create broad helper/manager dumping grounds. | Source assertion |

## Production Behavior Artifact Matrix

| Signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| DI graph | Composition root | Runtime callers | App startup/test composition | Scope validation test. |
