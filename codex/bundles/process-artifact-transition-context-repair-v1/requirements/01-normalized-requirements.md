# Normalized Requirements

| Id | Requirement | Verification |
| --- | --- | --- |
| R-001 | Process-owned direct-agent completion must carry execution lineage into the transition service's required-artifact validation pass. | Focused integration test completing a step with matching workspace-write lineage through `TransitionStepAsync`. |
| R-002 | Workflow, subprocess, and manager recovery completions must have a typed path to carry their relevant lineage ids into transition validation. | Source assertions over finalizer result/request mapping. |
| R-003 | Manual transitions must still reject stale or unbound execution lineage for required artifacts. | Existing stale-lineage integration test remains passing. |
| R-004 | Managed artifact content validation must remain active. | Existing artifact validation tests remain passing. |
| R-005 | Blazor app delivery must remain generic for Blazor SSR, WASM, and WASM PWA, and the generic Blazor WASM PWA live-run profile must be present. | Template governance tests and source assertions. |
| R-006 | The user-visible web app process must remain available for testing. | Host liveness check against `http://127.0.0.1:5032`. |

