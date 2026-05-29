# SB02 Semantic Invariants

## Invariant SB02-ARTIFACT-CONTENT-TRUTH

- Invariant ID: `SB02-ARTIFACT-CONTENT-TRUTH`
- Source raw note: RN01 and R2 require workflow artifact references to be truthful.
- Expected behavior: when payload policy emits content-bearing artifact metadata, redacted content is written first and can be read through the artifact content store/API.
- Disallowed shallow implementation: creating `WorkflowArtifactRecord` metadata that points at a storage path without writing retrievable bytes/text.
- Failing-first test: N/A - process/non-production exemption because the gap was proven by source audit of missing writer/reader boundary, then closed with new store/API tests.
- Passing test: `WorkflowPayloadPolicyService_writes_retrievable_redacted_artifact_content` in `bundle://proof/SB10/transcripts/dotnet-test-unit-workflow-executor-catalog.txt`.
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowContracts.cs`; `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowArtifactContentStores.cs`; `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowPayloadPolicyService.cs`; `repo://src/CanDoItAll.Web/Api/WorkflowsApi.cs`.
- Production assertions: `bundle://proof/SB10/transcripts/source-assertions-artifact-content.txt`.
- Red-team negative case: missing artifact content returns null/not-found, verified by `InMemoryWorkflowArtifactContentStore_returns_null_for_missing_content`.
- Downstream dependency check: SB05 and SB07 file/report/download outputs can now claim artifacts only when the referenced content path exists.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| `WorkflowArtifactContent` | `WorkflowPayloadPolicyService` writes through `IWorkflowArtifactContentStore`; proof `bundle://proof/SB10/transcripts/source-assertions-artifact-content.txt` | Workflow artifact content API and store readers; proof `bundle://proof/SB10/transcripts/dotnet-test-integration-workflow-api.txt` | Written after redaction and before metadata is exposed; verified by `WorkflowPayloadPolicyService_writes_retrievable_redacted_artifact_content` | Missing content returns null/not-found, verified by `InMemoryWorkflowArtifactContentStore_returns_null_for_missing_content` |
