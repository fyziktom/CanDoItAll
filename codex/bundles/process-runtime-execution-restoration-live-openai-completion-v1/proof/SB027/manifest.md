# SB027 Proof Manifest

## Status
Completed.

## Objective
Gate I: prove trigger-origin process starts.

## Owned Requirements And Notes
- Requirement IDs: REQ-001, REQ-002, REQ-003, REQ-004, REQ-015 trigger-origin subset.
- Critical invariant contract: `bundle://proof/SB027/semantic-invariants.md`
- Downstream dependency: SB028-SB030 UI proof may start after scheduler/workflow-origin start provenance is source-backed.

## Changed File Hashes
| File | SHA-256 |
| --- | --- |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/reviews/01-execution-report.md` | `3a60030927876bea3b604d48e05d63efb376c39858c183d0a24ecd1d28326644` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/subbundles/SB027/README.md` | `fa0b5a2ed6caae7c5ebabbfd6aa9f8fa00ebcf5fa9ca31962d9032ab5fcb7bb4` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB027/transcripts/trigger-origin-process-starts-tests.txt` | `10949de04a8bc8456ccbf8f3ca2c67135aefec7e191d36194538119e5650f8a7` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB027/transcripts/trigger-origin-source-assertions.txt` | `80a094fdda022babfdccad864cc5762c255e6a255132efa86fda394a2a6bcf59` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB027/red-team/manual-start-not-trigger-origin-proof.txt` | `955790cc71f14a7aba70291d5ebca42ae6f27b26d9e1c45567290308003d2bbc` |
| `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.TriggerStart.cs` | `ec626dde0e91cd8ec7b5c6c633cd1d83b51420b4c23d7c426ab7835ddf2895c7` |
| `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs` | `acbdf998f5834e395c893aac70f9c09b7e9c1deaa13d6d92b95cb757b15eeca7` |
| `repo://tests/CanDoItAll.Tests.Integration/SchedulerPlannerIntegrationTests.cs` | `86f361a5bd18dce04d4436e8cdfd41c79a79699d41d495c43ab005f4026363d2` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs` | `599540f916a2499569e791cb1b1f1a93ad6de395ac1a1470b681e768614c9ab9` |

## Command Transcripts
- Integration: `bundle://proof/SB027/transcripts/trigger-origin-process-starts-tests.txt`
- Source assertions: `bundle://proof/SB027/transcripts/trigger-origin-source-assertions.txt`
- No transient bundle-path scan: `bundle://proof/SB027/transcripts/no-transient-bundle-path-scan.txt`
- Anti-stub/runtime-host drift scan: `bundle://proof/SB027/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Red-team manual-start rejection: `bundle://proof/SB027/red-team/manual-start-not-trigger-origin-proof.txt`

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Scheduler-origin process run | `SchedulerTargetLauncher.LaunchProcessAsync` | Process runtime and scheduler history | Starts through `StartRunFromTriggerAsync` with `SchedulerPlan` metadata | Manual-start red-team rejects plain `StartRunAsync` proof |
| Workflow-origin process run | `ProcessesService.StartRunFromTriggerAsync` | Process runtime/readback | Persists `WorkflowRun` source identity and requester in trigger reason | Missing source identity is rejected with typed errors |
| Scheduler-origin workflow run | `SchedulerTargetLauncher.LaunchWorkflowAsync` | Scheduler history and workflow runtime | Starts a real workflow run for workflow scheduler target | Gate distinguishes scheduler-to-workflow from workflow-origin process start |
| No driver hook | Trigger-start tests and scans | Gate I review | Trigger origins call process service runtime, not driver runtime hooks | Anti-stub/runtime-host scan rejects driver runtime host drift |

## Closure
- Shallow-pass trap: A fake pass could cite manual `StartRunAsync` coverage or scheduler plan persistence without actual trigger-origin run start.
- Adversarial negative proof: `bundle://proof/SB027/red-team/manual-start-not-trigger-origin-proof.txt`
- Semantic positive proof: `bundle://proof/SB027/transcripts/trigger-origin-process-starts-tests.txt` plus `bundle://proof/SB027/transcripts/trigger-origin-source-assertions.txt`
- Anti-stub audit: `bundle://proof/SB027/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Raw-note closure: Scheduler-origin and workflow-origin starts are source-backed.
