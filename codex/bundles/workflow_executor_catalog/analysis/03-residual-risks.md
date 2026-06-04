# Residual Risks

## R1 Validator without executor catalog

Risk: invalid executor IDs, planned executors, or unavailable plugin executors can pass catalog save validation and fail later at runtime.

Mitigation:
- Register `WorkflowDefinitionValidator` with `IWorkflowExecutorCatalog` where possible.
- If circular dependency appears, split validation into graph validation and executor catalog validation service.
- Add failing-first tests for unknown executor id, planned executor id, unavailable plugin executor, invalid settings schema, and unavailable backend.

## R2 Artifact records without content

Risk: users see artifact references for truncated payloads but cannot open/download the content.

Mitigation:
- Introduce `IWorkflowArtifactContentStore`.
- Write redacted payload content or intentionally store raw payload only if policy allows.
- Add API/UI retrieval path and tests that load artifact content by `WorkflowArtifactId`.

## R3 Workspace versus absolute local path confusion

Risk: source ingestion allows absolute input paths only when enabled, while file executor is workspace-bound. Users may not understand which node can access local folders and which cannot.

Mitigation:
- Create explicit workspace folder/file executor UX.
- Add an optional guarded “external local path import” executor with approval and allowlist, not as default behavior.

## R4 Helper node pass-through ambiguity

Risk: node kinds appear available in UI/templates but do nothing at runtime.

Mitigation:
- Add validator checks: active node kinds must have known semantics.
- Convert helper nodes to executor-backed behavior.
- Mark visual-only nodes as non-executable and block publish/run until resolved.

## R5 HTTP and command safety

Risk: HTTP fetch and command process can become SSRF/host-command surfaces.

Mitigation:
- Use `IHttpClientFactory`, policy/allowlist, max size/content-type constraints.
- Keep command execution behind workspace boundary, allowlisted recipes, and approval.
