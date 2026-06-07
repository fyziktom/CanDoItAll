# Core Extraction Scorecard

## Scope
- This scorecard refreshes SB031 after execution, finalizer, diagnostics, and projection descriptor work.
- Scores are readiness signals, not a request for broad runtime extraction.

## Scorecard
| Family | Current state | Readiness | Remaining blocker |
| --- | --- | --- | --- |
| Execution evidence descriptors | Core descriptors plus module adapter are in place. | Stable descriptor surface. | AgentFramework execution and retry orchestration stay module-owned. |
| Finalizer evidence descriptors | Intent/result descriptors plus module adapter are in place. | Stable descriptor surface. | Finalizer invocation and transition application stay module-owned. |
| Retry/provider diagnostics | Retry, no-progress, and provider repair descriptors are in place. | Stable descriptor surface. | Provider calls, repair, recovery packets, and retry persistence stay module-owned. |
| Projection/validation descriptors | Projection order, lineage, provider-browser facts, validation policy, and adapters are in place. | Stable descriptor surface. | Storage, filesystem, lineage JSON persistence, browser output probing, and projection orchestration stay module-owned. |
| Adapter ownership | Exact Core consumer map is guarded. | Stable boundary. | New consumers require explicit map/test updates. |
| Public API stability | API snapshot and owner classification are guarded. | Stable for driver-contract proposal work. | Any new public Core surface requires owner classification and generated API proof. |

## Decision
- No broad runtime extraction is approved.
- Core is stable enough for driver-contract proposal work.
- The next production implementation should still wait for permission/audit/sandbox prerequisites.
