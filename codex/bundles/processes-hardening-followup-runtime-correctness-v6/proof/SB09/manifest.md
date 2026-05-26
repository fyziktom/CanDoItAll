# SB09 Proof Manifest

## Status

Completed.

## Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessDefinitionEntities.cs` persists `WorkflowOutputId`, `WorkflowOutputName`, `WorkflowOutputKind`, and `SubprocessChildArtifactExpectationId` on artifact expectations.
- `repo://src/CanDoItAll.Migrations.PostgreSql/Migrations/20260526013931_ProcessArtifactExplicitOutputMappings.cs` adds the PostgreSQL columns and subprocess-child mapping index.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkflowRunCoordinator.cs` resolves workflow artifacts from explicit output id/name/kind mapping before any legacy fallback.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkflowRunCoordinator.cs` blocks ambiguous same-kind workflow artifacts and emits warning diagnostics for legacy fallback.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` resolves subprocess projections from explicit child expectation ids before any legacy fallback.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` blocks ambiguous same-kind subprocess artifacts and emits warning diagnostics for legacy fallback.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` covers explicit workflow mapping, explicit subprocess mapping, ambiguity blocking, and warning-only legacy fallback.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs` proves explicit mapping fields persist through `SaveAsync` and `GetEditorAsync`.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB09 verified runtime behavior | repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessDefinitionEntities.cs | bundle://proof/SB09/manifest.md | bundle://proof/SB09/transcripts/passing.txt | bundle://proof/SB09/transcripts/failing-first.txt |
## Semantic Invariant Contract

- `bundle://proof/SB09/semantic-invariants.md`

## Failing-First or Red-Team Proof

Transcript: `bundle://proof/SB09/transcripts/failing-first.txt`

## Passing Proof

Transcript: `bundle://proof/SB09/transcripts/passing.txt`

## Anti-Stub Audit

Transcript: `bundle://proof/SB09/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

Transcript: `bundle://proof/SB09/transcripts/changed-file-hashes.txt`

- `95FF67640E7209C0DE05D0510D1185877775C0CA90EA30BFD2AEA14D15E81E51` `repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessDefinitionEntities.cs`
## Validation

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~WorkflowArtifactProjectionMapping_SB09_INV_001|FullyQualifiedName~SubprocessArtifactProjectionMapping_SB09_INV_001|FullyQualifiedName~SaveAsync_SB09_INV_001_persists_explicit_artifact_output_mappings"` passed: 7 tests.
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessWorkflowExecutorIntegrationTests|FullyQualifiedName~WorkflowArtifactProjectionMapping_SB09_INV_001|FullyQualifiedName~SubprocessArtifactProjectionMapping_SB09_INV_001|FullyQualifiedName~SaveAsync_SB09_INV_001"` passed: 12 tests.
- Existing EF Core relational version MSB3277 warnings remain unrelated to SB09.
- `dotnet ef migrations add ProcessArtifactExplicitOutputMappings --project src/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --startup-project src/CanDoItAll.Web/CanDoItAll.Web.csproj --context AppDbContext` succeeded; the EF tools/runtime version warning is unrelated to SB09 behavior.
- SQLite audit found only existing retired/legacy strings and bundle prohibition text; no SB09 SQLite runtime or migration dependency was added.

## Blockers

None.




