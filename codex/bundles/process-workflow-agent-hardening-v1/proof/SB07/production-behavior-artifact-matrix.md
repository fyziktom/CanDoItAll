# SB07 Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Proof citation |
| --- | --- | --- | --- | --- |
| Workflow executor display badge | `WorkflowExecutorDisplayAdapter` consuming workflow executor descriptors and retry policy | Workflow canvas catalog, node chips, node setup panel, node modal | Built at render time from canonical descriptor metadata; retry safety delegates to `WorkflowExecutorSideEffectPolicy` | `source-assertions.txt`; `workflow-canvas-regression-tests.txt`; workflow screenshots |
| Capability proof display badge | `CapabilityProofDisplayAdapter` consuming `CapabilityProofStatus` | Agent capability panel and agent details dialog | Built at render time from canonical proof status enum | `component-status-display-tests.txt`; `source-assertions.txt` |
| Provider profile display status | `ProviderProfileDisplayAdapter` consuming provider profile DTO state | Provider profile panel and tree node builder | Built at render time from enabled/health state without duplicating component mappings | `component-status-display-tests.txt`; `source-assertions.txt` |
| Provider usage summary on live stats | `ProcessObservationService` consuming execution usage observations, legacy runtime metrics, and provider pricing profiles | Live process dashboard and observation graphs | Rebuilt with each live snapshot; marks unknown usage when legacy activity lacks usage observations or known cost | `process-workspace-regression-tests.txt`; `source-assertions.txt` |
| Process usage cost display | `ProcessUsageDisplayAdapter` consuming `ProcessLiveStats` | Live dashboard cost stat, run cost badge, and process graphs | Shows precise actual cost only when provider usage is complete; otherwise reports incomplete usage | `component-status-display-tests.txt`; `process-workspace-regression-tests.txt`; process screenshots |
| Browser proof artifacts | Playwright browser validation run on isolated in-memory host | SB07/SB08/SB09 reviewers | Captured per route and viewport with screenshots, snapshots, console logs, host logs, and cleanup receipt | `browser-validation-analytics.md` |

## Dependency Smoke Proof

- SB08 can use the workflow editor proof to confirm executor availability and side-effect metadata are visible before running multidomain scenarios.
- SB08 can use the live-process proof to confirm cost UI distinguishes known zero usage from incomplete usage.
- SB09 can red-team UI drift by checking display adapters and the screenshot-backed browser proof instead of relying on component compilation alone.
