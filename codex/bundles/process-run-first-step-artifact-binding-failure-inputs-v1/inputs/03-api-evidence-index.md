# API Evidence Index

This file summarizes observed API facts only. It intentionally avoids implementation recommendations.

## Run Identity

- Base URL: `http://localhost:5032`
- API auth: disabled; see `api-evidence/00-access-status.json`
- Process run id: `9bbc0667-9d12-4506-ba81-654ef924cad6`
- Process run name: `Main app / Blazor app delivery`
- Process definition id: `9dd2f94e-b607-4f47-afb6-c51765db55bb`
- Process definition version id: `9e902a62-4f74-46c0-b974-7ebed8f66f52`
- Project id: `7330105d-8450-4c80-923b-5c27d8e63d6c`
- Run status: `5` = `Failed`
- Operating mode: `2` = `AssistedExecution`
- Progress: `0/8` completed steps
- Updated at: `2026-05-26T14:03:51.142717-04:00`

## Failure Summary

The first step failed after the agent execution itself completed successfully.

- Step run id: `0610f6d6-5d37-4313-b560-09cc9484f5b8`
- Step definition id: `70d60e3e-ded4-4d12-a4b0-4c271b47d844`
- Step title: `Resolve Blazor delivery contract`
- Step kind: `0` = `Start`
- Step status: `8` = `Failed`
- Step block reason code: `9` = `ArtifactContractUnsatisfied`
- Next recovery action: `2` = `RecoverArtifactsOnly`
- Recovery options: `2` = `RecoverArtifactsOnly`, `3` = `RetryAgent`, `6` = `HumanEscalation`
- Exception summary:

```text
AgentFramework execution failed: Step 'Resolve Blazor delivery contract' cannot be completed because required artifact contract validation failed: Blazor delivery contract: StaleOrWrongRun (The candidate artifact is not bound to the current process run, step, execution run, or workflow run.).
```

Primary raw files:

- `api-evidence/11-run-detail-full.json`
- `api-evidence/12-run-steps.json`
- `api-evidence/13-step-00-detail.json`

## Step Sequence Around Failure

| Seq | Step run id | Title | Status | Outputs | Allowed operations | Target scope |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | `0610f6d6-5d37-4313-b560-09cc9484f5b8` | Resolve Blazor delivery contract | `8` Failed | Blazor delivery contract | `0,1,3` | `3` ExternalProductTargetReadOnly |
| 1 | `2d7d0cb5-5566-4c60-8209-c36520df5163` | Build Blazor application | `0` Pending | Implementation self-review summary; Blazor implementation change set | `0,1,2,3,5,6` | `4` ExternalProductTargetMutable |
| 2 | `a44c1771-f139-4dc0-9c5e-7731161186de` | Validate Blazor runtime and browser evidence | `0` Pending | Validation self-review summary; Blazor runtime evidence pack | `0,1,2,3,6,7,8` | `3` ExternalProductTargetReadOnly |
| 3 | `713680d8-b416-4ce2-92b1-b2ef9d18bfe6` | Repair Blazor validation findings | `0` Pending | Blazor repair change set | `0,1,2,3,5,6` | `4` ExternalProductTargetMutable |
| 4 | `f5ad15a5-75d0-4eb1-9558-ae783a4f45ce` | Revalidate Blazor repair | `0` Pending | Repaired Blazor runtime evidence pack | `0,1,2,3,6,7,8` | `3` ExternalProductTargetReadOnly |
| 5 | `b873774c-7f68-4282-b097-7fa62113d0a6` | Record Blazor results and evidence index | `0` Pending | Project-structure result writeback summary; Run evidence index | `0,2,3,9` | `5` ExternalActionControlled |
| 6 | `f2754381-57eb-4cf0-9637-0b4dbffa0493` | Record repaired Blazor results and evidence index | `0` Pending | Repaired project-structure result writeback summary; Repaired run evidence index | `0,2,3,9` | `5` ExternalActionControlled |
| 7 | `3c461bd7-dce0-4fa5-aebb-e1917da8a2d1` | Escalate unresolved Blazor repair findings | `0` Pending | Blazor repair escalation record | `0,2,3,11` | `0` ManagedProcessArtifactsOnly |

Enum references are in `src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs` and `src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs`.

## Delivery Contract Artifact Record

- Artifact record id: `aa9a3e75-8d3e-4757-bafa-be00e8678b8d`
- Artifact expectation id: `47ae2838-41eb-4301-a57e-350a635d4a51`
- Artifact kind: `0` = `Brief`
- Title: `Blazor delivery contract`
- Trust status: `1` = `ReviewRequired`
- Sensitivity: `1` = `Internal`
- Managed storage path:

```text
artifacts/scopes/organization/e5df9ad633dbc6974a0678a74976013c/process-runs/9bbc0667-9d12-4506-ba81-654ef924cad6/01-blazor-delivery-contract.md
```

- External reference key:

```text
workspace-written-artifact|91e6a078-ac63-43e6-9901-6f8364539c42|47ae2838-41eb-4301-a57e-350a635d4a51|artifacts/process-runs/9bbc0667-9d12-4506-ba81-654ef924cad6/01-blazor-delivery-contract.md
```

- Projection lineage JSON:

```json
{"sourceKind":"WorkspaceWrite","sourceExecutionRunId":"91e6a078-ac63-43e6-9901-6f8364539c42","projectedExecutionRunId":"91e6a078-ac63-43e6-9901-6f8364539c42","sourceExternalReferenceKey":"workspace-written-artifact|91e6a078-ac63-43e6-9901-6f8364539c42|47ae2838-41eb-4301-a57e-350a635d4a51|artifacts/process-runs/9bbc0667-9d12-4506-ba81-654ef924cad6/01-blazor-delivery-contract.md","contentHash":"","projectionIdentityHash":"sha256:2c233800c82b0b865f3cd3d689e8deb7789ef876682fca5de7395d32548fe7ec"}
```

Observed artifact facts:

- The artifact expectation is shown as satisfied in the step view.
- The step still failed final contract validation with `StaleOrWrongRun`.
- `contentHash` in projection lineage is empty.
- The managed path is organization-scoped, while the external reference key path segment is `artifacts/process-runs/...`.
- A project-structure output node exists for this run and artifact: `process-run-output:9bbc0667-9d12-4506-ba81-654ef924cad6:29cef23cc26e`.

Primary raw files:

- `api-evidence/14-run-artifacts.json`
- `api-evidence/15-step-00-artifacts.json`
- `api-evidence/16-artifact-delivery-contract-detail.json`
- `api-evidence/43-project-structure-read-full-project.json`

## Agent Execution Runs

Two execution runs are tied to this process run.

### Step 0 Agent Execution

- Execution run id: `91e6a078-ac63-43e6-9901-6f8364539c42`
- Agent id: `2732772f-3a8a-4e1f-97d0-56c25eade538`
- Source kind: `process-step`
- Source id: `0610f6d6-5d37-4313-b560-09cc9484f5b8`
- State: `5` = `Completed`
- Outcome: `0` = `Succeeded`
- Requested by: `process-automation-dispatch`
- Model: `gpt-5-mini`
- Auto-approve pending tool calls: `true`
- Structured output contract: `process_step_outcome_result`
- The execution result reported `status: Completed`.
- The execution result cited evidence ref `artifacts/process-runs/9bbc0667-9d12-4506-ba81-654ef924cad6/01-blazor-delivery-contract.md`.
- Tool receipt: one `workspace_write_file` succeeded and created the organization-scoped managed file with `5660` characters.

Primary raw files:

- `api-evidence/31-agent-execution-run-91e6a078-ac63-43e6-9901-6f8364539c42-detail.json`
- `api-evidence/34-agent-execution-run-91e6a078-ac63-43e6-9901-6f8364539c42-tool-receipts.json`
- `api-evidence/35-agent-execution-run-91e6a078-ac63-43e6-9901-6f8364539c42-log.json`
- `api-evidence/36-agent-execution-run-91e6a078-ac63-43e6-9901-6f8364539c42-metrics.json`

### Later Manager-Chat Execution

- Execution run id: `d38da822-a980-44ce-952b-6e86c0b74bbb`
- Source kind: `process-manager-chat`
- Source id: `9dd2f94e-b607-4f47-afb6-c51765db55bb`
- State: `3` = `WaitingOnTool`
- Pending approval count: `1`
- Pending tool: `processes_artifact_record`
- Pending approval id: `ficc_call_M2nwtidqGQPrBjKYG1p4uUXT`
- The requested artifact title is `Operator decision: Approved - Resolve Blazor delivery contract`.
- The pending approval arguments say the operator approved accepting the contract, but agents still must rebind or re-create the contract artifact against this process run before the step can complete.

Primary raw files:

- `api-evidence/31-agent-execution-run-d38da822-a980-44ce-952b-6e86c0b74bbb-detail.json`
- `api-evidence/33-agent-execution-run-d38da822-a980-44ce-952b-6e86c0b74bbb-checkpoints.json`
- `api-evidence/35-agent-execution-run-d38da822-a980-44ce-952b-6e86c0b74bbb-log.json`
- `api-evidence/37-agent-execution-run-d38da822-a980-44ce-952b-6e86c0b74bbb-approvals.json`

## Runtime Health, Decisions, And Escalation

Run health from `api-evidence/11-run-detail-full.json`:

- Active execution count: `1`
- Latest attempt count: `2`
- Pending approval count: `1`
- Failed step count: `1`
- Missing artifact count: `0`
- Runtime invariant diagnostic count: `1`
- Actionable reason: `One or more runtime invariant diagnostics need review.`
- Recommended action: `2` = `RecoverArtifactsOnly`

Additional records:

- Decisions: `6`
- Outbox records: `2`, both completed
- Work briefs: `8`
- Conformance observations: `1`
- Direct message threads: `0`
- Workflow runs: `0`
- Escalations: `1`
- Operator approvals: `1`
- Attempt timeline entries: `6`

Escalation:

- Escalation id: `e408fdcf-bdf8-4988-87e3-43a60b920f7d`
- Step: `Resolve Blazor delivery contract`
- Title: `Failed step needs operator review`
- Kind: `1`
- Severity: `2`
- Status: `0`
- Open: `true`
- Due at: `2026-05-26T18:03:51.1427178-04:00`
- Reason repeats the `StaleOrWrongRun` artifact contract validation failure.

Primary raw files:

- `api-evidence/11-run-detail-full.json`
- `api-evidence/19-run-escalations.json`
- `api-evidence/26-process-analytics.json`

## Process Definition And Template Context

- Definition name: `Blazor app delivery`
- Definition status: `1` = `Published`
- Definition contract mode: `1` = `Strict`
- Criticality: `2`
- Autonomy level: `2`
- Roles: `4`
- Steps: `8`
- Lint issues: `7`
- Lint has errors: `false`
- Lint has warnings or errors: `true`
- Every lint issue in the captured detail is `processes.lint.step-boundary-ambiguous`.

Primary raw files:

- `api-evidence/20-process-definition-detail.json`
- `api-evidence/21-process-definition-export.json`
- `api-evidence/24-template-blazor-app-delivery-detail.json`
- `api-evidence/25-template-blazor-app-delivery-mermaid.mmd`

## Project Structure Context

Selected project-structure nodes referenced by the step output:

| Node id | Title | Status | Notes |
| --- | --- | --- | --- |
| `custom:7404d4fd10624f468c2524ba618d747b` | Main app | Draft | Selected work node. |
| `process-definition:9dd2f94e-b607-4f47-afb6-c51765db55bb` | Blazor app delivery | Published | Role-first contract: 4 roles, 8 steps. |
| `custom:a4eca310019c4c5c8ba638aa44218ff3` | Blazor runtime evidence pack | Draft | Notes point to old run id `0cca729a-e9bc-47e7-89aa-bef9b88dbf1c`, not the current run id. |
| `custom:8af5e3d023734a0e9cb7c42d6f59043b` | QA evidence placeholder | Draft | Notes say evidence will be written separately. |
| `custom:0e6475a1f98b484d90670671e73cbe76` | `C:\programovani\dotnet-demo\output` | Draft | Output path note. |

Full project read also includes:

- `process-run:9bbc0667-9d12-4506-ba81-654ef924cad6`, status `Failed`
- `process-run-output:9bbc0667-9d12-4506-ba81-654ef924cad6:29cef23cc26e`, status `Stored`, notes name the managed artifact path and `Blazor delivery contract`
- `custom:85bfefd7bd7f401ab2c964ea04337cec`, `Blazor WebAssembly PWA shell`
- `custom:191c3c1e156d4fd3a1a87b16d6762a15`, another `Main app`
- `custom:b49064a0983e4a2ea2dca56af1d237d8`, `Output folder`

Primary raw files:

- `api-evidence/42-project-structure-read-selected-nodes.json`
- `api-evidence/43-project-structure-read-full-project.json`

## Source Files Likely Needed For Later Diagnosis

These are source references only, not implementation guidance:

- `repo://src/CanDoItAll.Web/Api/ProcessesApi.cs`
- `repo://src/CanDoItAll.Web/Api/AgentsApi.cs`
- `repo://src/CanDoItAll.Web/ProjectStructureAgentApi.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs`
- `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs`
- `repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs`
- `repo://src/CanDoItAll.AgentFramework.Models/Common/Enums.cs`

## Evidence Gaps In The API Surface

- The run health reports `invariantDiagnosticCount: 1`, but the captured process API response does not expose the diagnostic list as a top-level array.
- Storage download by raw managed path failed because `/storage/objects/download` expects a storage reference token, not a raw `path` query parameter.
- `/managed-files/{path}` serves only the managed-files root and did not expose the organization-scoped artifact path.
- The delivery contract content is still available indirectly in the step agent execution `resultSummary.humanReadableSummaryMarkdown` and the workspace write tool receipt, both captured in agent execution run detail.
