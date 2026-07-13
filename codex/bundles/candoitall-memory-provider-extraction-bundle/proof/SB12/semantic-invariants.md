# SB12 Semantic Invariants

## Invariant SB12_PROC001

- Invariant ID: `SB12_PROC001`
- Source raw note: process source adapters must expose process, step, agent-session, artifact, and completion context through the generic Source Gateway without leaking EF entities or native memory types.
- Expected behavior: `ProcessRuntimeEvidenceSourceProvider` returns MAF `MemorySourceSnapshot` items for process definitions, runs, steps, assignments, agent sessions, decisions, artifacts, journals, conformance observations, and completion outcomes.
- Disallowed shallow implementation: returning only process ids/counts, hand-built DTOs outside the MAF snapshot family, exposing DbContext entities, or copying artifact payload bytes into snapshots.
- Passing test: `Process_runtime_source_provider_exposes_run_step_agent_artifact_and_completion_context` in `bundle://proof/SB12/transcripts/passing-runtime-source-unit-tests.txt`.
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeEvidenceSourceProvider.cs`, `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeMemorySourceGatewayAdapter.cs`, and `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Sources/MemorySourceSnapshotContracts.cs`.
- Production assertions: `bundle://proof/SB12/transcripts/source-audit-source-snapshot-contract-family.txt` and `bundle://proof/SB12/transcripts/source-audit-provider-driver-boundary.txt`.
- Red-team negative case: denied process scope returns `DeniedSourceScope` and leaves the process provider unread.
- Downstream dependency check: SB14 and SB15-SB18 can request process runtime evidence through the generic source gateway and same MAF snapshot family.

## Invariant SB12_SCOPE001

- Invariant ID: `SB12_SCOPE001`
- Source raw note: provider source requests cannot query process data directly or bypass policy.
- Expected behavior: the generic source gateway rejects a process runtime request with an unauthorized requested scope before adapter/provider dispatch.
- Disallowed shallow implementation: relying on the process adapter to catch policy mistakes after it has access to process persistence, or treating mismatched scope as an empty snapshot.
- Passing test: `Process_gateway_rejects_denied_process_scope_before_provider_dispatch` in `bundle://proof/SB12/transcripts/passing-runtime-source-unit-tests.txt`.
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeMemorySourceGatewayAdapter.cs` and `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeSourceGatewayAdapterTests.cs`.
- Production assertions: `bundle://proof/SB12/transcripts/source-audit-adapter-registration.txt`.
- Red-team negative case: requesting `Agent` scope while policy allows only `Process` returns `DeniedSourceScope` and `ReadCount` remains zero.
- Downstream dependency check: provider-initiated ingestion in SB14-SB18 can rely on source policy enforcement before module-specific adapters run.

## Invariant SB12_WF001

- Invariant ID: `SB12_WF001`
- Source raw note: existing workflow runtime evidence source behavior must be migrated through the generic Source Gateway while preserving diagnostics and snapshot semantics.
- Expected behavior: `WorkflowRuntimeMemorySourceGatewayAdapter` translates generic workflow source requests into `WorkflowRuntimeEvidenceSourceRequest` and delegates to `IWorkflowRuntimeEvidenceSourceProvider`.
- Disallowed shallow implementation: replacing the existing workflow provider with a new partial snapshot implementation, duplicating cursor/hash behavior, or silently accepting wrong source kinds/scopes.
- Passing test: `Workflow_gateway_adapter_translates_scope_id_into_workflow_run_request` in `bundle://proof/SB12/transcripts/passing-runtime-source-unit-tests.txt`.
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/WorkflowRuntimeMemorySourceGatewayAdapter.cs` and `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`.
- Production assertions: `bundle://proof/SB12/transcripts/source-audit-source-snapshot-contract-family.txt`.
- Red-team negative case: wrong source kind or requested scope throws an explicit adapter error instead of returning misleading workflow evidence.
- Downstream dependency check: SB15-SB18 can route workflow memory source requests through a generic adapter without reworking workflow evidence provider internals.

## Invariant SB12_DI001

- Invariant ID: `SB12_DI001`
- Source raw note: the unavailable process source provider must retain useful diagnostics but not shadow the real process source provider when Processes is installed.
- Expected behavior: Processes registers `ProcessRuntimeEvidenceSourceProvider` and `ProcessRuntimeMemorySourceGatewayAdapter`; AgentFramework keeps `UnavailableProcessRuntimeEvidenceSourceProvider` as a `TryAdd` fallback and registers the workflow adapter.
- Disallowed shallow implementation: deleting unavailable-provider diagnostics, registering a fallback after the real process provider, or making AgentFramework depend directly on Processes.
- Passing test: `Process_and_agent_framework_modules_register_runtime_source_gateway_adapters` in `bundle://proof/SB12/transcripts/passing-runtime-source-unit-tests.txt`.
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` and `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`.
- Production assertions: `bundle://proof/SB12/transcripts/source-audit-adapter-registration.txt`.
- Red-team negative case: removing the real Processes registration or changing the fallback from `TryAdd` would fail the DI registration test.
- Downstream dependency check: composed hosts get real process snapshots, while AgentFramework-only hosts still fail predictably with existing diagnostics.

## Raw Note Closure

| Raw note | Status | Proof |
| --- | --- | --- |
| Process/workflow/agent source adapters | Solved | `bundle://proof/SB12/manifest.md` and focused runtime source unit tests |
| Process completion feedback hook candidate | Solved | `SB12_PROC001` test asserts `feedbackHook=process-runtime-completion` |
| Artifact references without payload copying | Solved | `SB12_PROC001` test asserts `artifact-id` storage references and content hash only |
| Denied process scope before provider dispatch | Solved | `SB12_SCOPE001` |
| Workflow runtime evidence compatibility | Solved | `SB12_WF001` |
| Unavailable process provider diagnostics preserved | Solved | `SB12_DI001` |

## Shallow-Pass Trap

- A DTO-only implementation would satisfy compile checks but break the contract family; the source snapshot contract audit proves MAF snapshot reuse.
- A provider that returned only process run rows would miss step, assignment, agent session, artifact, journal, and completion evidence; the unit test asserts representative items across those domains.
- A gateway adapter that allowed scope mismatch would leak process evidence to provider requests; the denied-scope test proves rejection before provider dispatch.
- A workflow rewrite would risk cursor/hash drift; the workflow adapter test proves delegation to the existing provider contract.

## Downstream Dependency Check

- SB13 can add CRM/resource/manual adapters using the same gateway descriptor and policy model.
- SB14 can checkpoint source gateway hardening with process/workflow adapters included in the boundary and anti-stub audits.
- SB15-SB18 can request process and workflow source context through generic gateway adapters instead of direct module reads.
- SB20-SB22 can display process-source ingestion state without inventing a second source snapshot shape.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Process runtime source provider | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeEvidenceSourceProvider.cs` | `Process_runtime_source_provider_exposes_run_step_agent_artifact_and_completion_context` in `bundle://proof/SB12/transcripts/passing-runtime-source-unit-tests.txt` | persistence and execution observations are mapped into MAF source snapshots | denied process scope test proves provider dispatch is blocked before source access |
| Process runtime gateway adapter | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeMemorySourceGatewayAdapter.cs` | generic gateway denied-scope test and adapter registration proof | maps generic source requests to process runtime evidence requests | scope mismatch returns `DeniedSourceScope` before provider dispatch |
| Workflow runtime gateway adapter | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/WorkflowRuntimeMemorySourceGatewayAdapter.cs` | workflow translation test | delegates to existing workflow evidence source provider | mismatched source kind/scope throws explicit adapter errors |
| Process completion feedback hook | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeEvidenceSourceProvider.cs` | completion metadata assertion in runtime source unit test | terminal process state emits a completion outcome item with hook metadata | snapshot does not force feedback submission or expose sensitive payloads |
| Artifact source reference | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeEvidenceSourceProvider.cs` | artifact reference assertion in runtime source unit test | artifact ledger rows become source references and storage locators | artifact bytes are not copied; only ids and hashes are exposed |
