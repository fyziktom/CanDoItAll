# 02-artifact-content-store-and-payload-retrieval

## Status

- Status: `Completed`

## Closure Notes

- Added `IWorkflowArtifactContentStore` and in-memory/file-backed content stores.
- `WorkflowPayloadPolicyService` now writes redacted artifact content before exposing artifact metadata.
- Added API retrieval for workflow artifact content by run id and artifact id.
- Proof manifest: `bundle://proof/SB02/manifest.md`
- Semantic invariants: `bundle://proof/SB02/semantic-invariants.md`

## Objective

Make workflow artifact references real and retrievable, or explicitly metadata-only where content is intentionally absent.

## Covered Inputs

- RN01: Runtime correctness must include artifact payload truth.
- RN02: New executors must not claim output artifacts without retrievable content.
- R2: Workflow artifact records must reference retrievable content or be clearly metadata-only.
- R6: Markdown/report output later depends on artifact integration.

## Prerequisites

- SB01 closure gate passed.
- Current payload policy and artifact storage paths are audited.
- Workspace and persistence boundaries are identified before adding storage.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowPayloadPolicyService.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowContracts.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowEventPayloads.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Artifacts/WorkspaceArtifactToolService.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Artifacts/WorkspaceArtifactToolContracts.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs`
- `repo://src/CanDoItAll.Web/Api/WorkflowsApi.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowFoundationTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/WorkflowApiIntegrationTests.cs`

## Scope

- Audit whether a writer already persists content for `WorkflowArtifactRecord.StoragePath`.
- Add a workflow artifact content store boundary if no writer exists.
- Persist redacted payload content when the policy creates a content-bearing artifact.
- Add a safe read path by artifact id or storage path through existing workspace or API boundaries.
- Mark metadata-only artifact records explicitly where full content is intentionally unavailable.

## Dependency Impact

- SB03, SB05, SB07, and SB10 depend on artifact content truth for file/report/download outputs.
- If this phase is wrong, later tests can pass metadata creation without proving users can retrieve outputs.

## Validation Depth

- Failing-first proof that a policy-created artifact cannot currently be retrieved, or source proof that the writer already exists.
- Passing tests for content write/read, redaction-before-storage, missing content failure, and workspace scope enforcement.
- Critical proof manifest because this phase introduces or validates production artifact records and content lifecycle.

## Implementation Steps

1. Trace every `WorkflowArtifactRecord.StoragePath` producer and consumer.
2. Add `IWorkflowArtifactContentStore` and a workspace-backed implementation only if no suitable store exists.
3. Update `WorkflowPayloadPolicyService` to write redacted content before exposing artifact metadata.
4. Add read APIs or service methods using existing authorization and workspace scoping.
5. Add tests for long output artifact retrieval, secret redaction, missing content, and allowed-kind policy.

## Do Not Do

- Do not persist raw secrets just because inline payloads are redacted.
- Do not create a second unscoped file store beside existing workspace policy.
- Do not let missing content be treated as an empty artifact.
- Do not start Markdown or HTTP output claims before this gate is proven.

## Acceptance Checklist

- A truncated inline event payload has a retrievable artifact body when content is claimed.
- Redaction occurs before artifact content is written.
- Missing artifact content fails clearly with actionable state.
- Artifact content cannot escape workspace or tenant scope.
- Metadata-only artifacts are labeled as such and are not presented as full content.

## Proof Required

- `bundle://proof/SB02/manifest.md`
- `bundle://proof/SB02/semantic-invariants.md`
- Failing-first or source-audit transcript for missing content writer behavior.
- Passing unit/integration transcripts for artifact write/read and redaction.
- Changed-file SHA-256 hashes, source assertions, and anti-stub audit.
- Production Behavior Artifact Matrix for artifact content record lifecycle if new production records or state are added.

## Browser Validation Logging

- N/A unless artifact retrieval becomes browser-visible in this phase; if UI links are changed, record route, viewport, actions, screenshot, and result in `bundle://reviews/01-execution-report.md`.

## Progression Gate

- Continue to SB03 only after artifact metadata either resolves to retrievable content or is explicitly marked metadata-only, with negative proof for missing content.

## Suggested Agent Prompt

Use SB02 to make workflow artifact references truthful. Prove the old gap or existing writer, add the smallest content-store boundary needed, and validate redacted retrieval through product-safe paths.
