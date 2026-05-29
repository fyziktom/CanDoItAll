# 02-artifact-content-store-and-payload-retrieval

## Objective

Make workflow artifact references real and retrievable.

## Required work

1. Audit whether any existing service writes payload content for `WorkflowArtifactRecord.StoragePath`.
2. If no content writer exists, add:
   - `IWorkflowArtifactContentStore`
   - workspace-backed implementation
   - persistent-safe metadata/content relationship
   - read API endpoint
   - UI/action path if already available in workflow page.
3. Update `WorkflowPayloadPolicyService` so when it creates an artifact it writes redacted payload content.
4. Decide whether raw payload is ever persisted. Default should be redacted content only.
5. Add hash/length metadata if useful without expanding the record too much.
6. Tests:
   - long executor output creates artifact record and retrievable content.
   - redaction applies before storage.
   - missing content fails clearly.
   - artifact allowed-kind policy is respected.

## Acceptance checklist

- A truncated inline event payload has a retrievable artifact body.
- Artifact content cannot escape workspace/tenant scope.
- UI/API can retrieve by artifact id or safe storage path.
