# Source Artifacts

| Id | Artifact | Type | Notes |
| --- | --- | --- | --- |
| SRC-001 | `repo://codex/bundles/process-run-first-step-artifact-binding-failure-inputs-v1/inputs/03-api-evidence-index.md` | Prior input bundle evidence | Captures earlier failed Blazor delivery run with the same first-step `StaleOrWrongRun` artifact failure. |
| SRC-002 | `repo://codex/bundles/process-run-first-step-artifact-binding-failure-inputs-v1/inputs/api-evidence/11-run-detail-full.json` | Prior raw API payload | Full failed run payload for run `9bbc0667-9d12-4506-ba81-654ef924cad6`. |
| SRC-003 | Live API run `0a96e6f9-4a89-4422-b931-e782f1b26c94` | Fresh runtime evidence | Current failed run observed through `GET /api/processes/runs/{runId}`. |
| SRC-004 | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Production source | Process-owned completion finalizer validates and transitions steps. |
| SRC-005 | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs` | Production source | Transition service revalidates required artifacts before completion. |
| SRC-006 | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs` | Production source | Runtime request models including `ProcessStepTransitionRequest`. |
| SRC-007 | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactValidator.cs` | Production source | Artifact validation entry point. |
| SRC-008 | `repo://Templates/Processes/processes/blazor-app-delivery/definition.json` | Template source | Generic Blazor delivery definition imported for the failed run. |
| SRC-009 | `repo://Templates/Processes/seed-catalog/live-run-profiles.json` | Template source | Generic Blazor WASM PWA live-run profile coverage. |
| SRC-010 | `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs` | Test source | Existing stale-lineage transition tests and target for regression coverage. |

## Fresh Failed Run Facts

- Run id: `0a96e6f9-4a89-4422-b931-e782f1b26c94`
- Definition: `Blazor app delivery`
- Run status: `Failed`
- Failed step: `Resolve Blazor delivery contract`
- Agent execution id: `5dce0d06-14b4-412c-a586-e262f0b7d9d1`
- Required artifact record: `d8dae283-ef03-4f6c-98ed-a4119428a3d5`
- Artifact path: `artifacts/scopes/organization/e5df9ad633dbc6974a0678a74976013c/process-runs/0a96e6f9-4a89-4422-b931-e782f1b26c94/01-blazor-delivery-contract.md`
- Failure summary: `StaleOrWrongRun (The candidate artifact is not bound to the current process run, step, execution run, or workflow run.)`

