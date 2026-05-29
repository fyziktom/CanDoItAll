# SB05 proof manifest

Status: Completed

## Summary

- Added `IWorkflowPayloadPolicyService` with typed scopes for run input, event payloads, executor outputs/errors, external requests, plugin logs, tool receipts, and preview simulation output.
- Runtime input, node output, failure, request, and event payload paths now redact and bound inline payloads before persistence and attach artifact metadata when policy requires capture.
- Plugin log messages/details and workspace tool receipts are redacted and bounded before storage.
- Default artifact policy now includes `ToolReceipt` and `PreviewSimulation` kinds.
- No UI surface changed; browser proof is not required for this subbundle.

## Source Changes

- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowPayloadPolicyService.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowEventPayloads.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`
- `repo://src/CanDoItAll.Modules.Plugins/Catalog/PluginLogServices.cs`
- `repo://src/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceExecutionSliceStore.cs`
- `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowCatalogModels.cs`
- `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorPolicyObservabilityTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowFoundationTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/WorkflowApiIntegrationTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/PluginCatalogIntegrationTests.cs`

Hash sample: `b4ae8571fda1f236407311cbfb7193ce5ed81612b61d523dd7949386c25d2f88`.

## Proof

- `bundle://proof/SB05/transcripts/failing-first-payload-policy-tests.txt`
  - Command: `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~WorkflowExecutorPolicyObservabilityTests|FullyQualifiedName~WorkflowFoundationTests"`
  - Result: failed before implementation with missing `WorkflowPayloadPolicyService`, missing `WorkflowPayloadPolicyRequest`, missing `WorkflowPayloadPolicyScope`, missing `PreviewSimulation`, and missing backend `payloadPolicyService` constructor support.
- `bundle://proof/SB05/transcripts/unit-payload-policy-after-implementation.txt`
  - Command: `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~WorkflowExecutorPolicyObservabilityTests|FullyQualifiedName~WorkflowFoundationTests" --logger "console;verbosity=minimal"`
  - Result: 33 passed, 0 failed, 0 skipped.
- `bundle://proof/SB05/transcripts/integration-payload-policy-after-implementation.txt`
  - Command: `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~WorkflowApiIntegrationTests.Workflow_api_test_run_applies_payload_policy_to_large_runtime_payloads|FullyQualifiedName~PluginCatalogIntegrationTests.Plugin_logs_persist_installation_runtime_and_redact_sensitive_values" --logger "console;verbosity=minimal"`
  - Result: 2 passed, 0 failed, 0 skipped.
- `bundle://proof/SB05/transcripts/source-assertions-payload-policy.txt`
  - Command: `rg -n "IWorkflowPayloadPolicyService|WorkflowPayloadPolicyScope|PluginLogMessage|PluginLogDetails|PreviewSimulation|ToolReceipt|BoundPayload|MaxInlinePayloadCharacters" ...`
  - Result: source assertions found policy service, runtime call sites, plugin log call sites, tool receipt bounding, artifact kinds, and regression tests.
- `bundle://proof/SB05/transcripts/build-after-sb05.txt`
  - Command: `dotnet build CanDoItAll.slnx --no-restore`
  - Result: build passed with 0 errors and 26 existing EF Core Relational assembly-version warnings.
- `bundle://proof/SB05/transcripts/git-diff-check-after-sb05.txt`
  - Command: `git diff --check`
  - Result: passed with line-ending normalization warnings only.
- `bundle://proof/SB05/transcripts/bundle-validator-prepared-after-sb05.txt`
  - Command: `python bundle-preparation validate_bundle.py codex\bundles\workflow-maf-hardening-followup --stage prepared`
  - Result: bundle is valid for stage `prepared`.
- Passing transcript: `bundle://proof/SB05/transcripts/unit-payload-policy-after-implementation.txt`
- Anti-stub transcript: `bundle://proof/SB05/transcripts/source-assertions-payload-policy.txt`
- `bundle://proof/SB05/transcripts/semantic-invariant-evidence.txt`
  - Command: semantic invariant transcript index.
  - Result: invariant ids are indexed for completed-stage validation.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Bounded payload metadata | `WorkflowPayloadPolicyService` | runtime events, artifacts, plugin logs, tool receipts | Redacts before bounding and creates safe artifact metadata when policy requests capture. | `bundle://proof/SB05/transcripts/failing-first-payload-policy-tests.txt`; `bundle://proof/SB05/transcripts/unit-payload-policy-after-implementation.txt` |
| Plugin/tool receipt bounded records | plugin log services and workspace execution store | plugin catalog and workflow detail consumers | Stored as redacted bounded inline summaries or safe metadata references. | `bundle://proof/SB05/transcripts/integration-payload-policy-after-implementation.txt` |

## Skipped

- Live Gmail, Office365, Docker, and host-command workflow proof was not run per bundle boundary.
- Browser proof was not run because SB05 did not change artifact display UI.
