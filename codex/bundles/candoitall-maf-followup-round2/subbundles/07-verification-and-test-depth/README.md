# Subbundle 07 — Verification and test depth

## Goal

Replace “looks implemented” with behavior-level proof for the new hardening invariants.

## Required implementation

Add or update tests in the appropriate unit/integration test projects.

## Required test cases

### Finalizer runtime mode behavior

- Disabled structured-output run does not attach finalizer tool or append finalizer instructions.
- Required structured-output run attaches exactly one matching finalizer tool.
- Required structured-output run appends required JSON-compatible finalizer instructions.
- Shadow run does not append exact-once required instructions.
- Approval continuation preserves the finalizer mode.
- Temperature retry preserves the finalizer mode.

### Tool policy exception boundary

- Denied policy decision throws `AgentToolPolicyBlockedException`.
- Missing effective approval path throws `AgentToolPolicyBlockedException`.
- Allowed tool throwing `InvalidOperationException` is not reclassified as policy-blocked.
- Allowed tool throwing `NotSupportedException` is not reclassified as policy-blocked.

### Provider capability truth

- Core feature matrix and UI defaults agree for OpenAI Responses.
- Core feature matrix and UI defaults agree for OpenAI Chat Completions.
- Core feature matrix and UI defaults agree for Ollama local/remote.
- Workspace-backed registry does not contradict the core feature matrix.
- Managed SQLite provider capability display is truthful or explicitly marked as non-authoritative legacy metadata.

### Verification document truthfulness

- `docs/agent-runtime-hardening-verification.md` lists exact commands and whether they passed.
- The document must not claim tests passed unless test output exists or Codex actually ran them.

## Commands Codex must run

```bash
dotnet --info
dotnet restore CanDoItAll.slnx
dotnet build CanDoItAll.slnx --configuration Release --no-restore
dotnet test CanDoItAll.slnx --configuration Release --no-build
```

If the repository still uses `.sln` in the target environment, use the real solution file available there. Do not silently skip tests.

## Acceptance criteria

- New behavior tests fail against the current bug state and pass after implementation.
- Static tests remain supplementary, not the only evidence.
- Verification docs match reality.
