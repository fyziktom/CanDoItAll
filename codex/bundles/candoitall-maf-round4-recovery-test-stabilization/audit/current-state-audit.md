# Current State Audit

## Scope

This audit inspected the attached repository snapshot after the user's reported round 3 Codex implementation. The audit focused on:

- MAF structured outputs, finalizers, and tool policy.
- Process automation failure/retry/recovery behavior.
- QA return and rework efficiency.
- Secret handling.
- Broad test suite instability reported by Codex.

## Executive verdict

The repository is improved compared with earlier rounds in some areas, especially finalizer mode wiring and policy exception boundaries. However, the attached snapshot does **not** contain several round 3 deliverables claimed by Codex. The most important missing pieces are secret removal/scanning, process mutation tool classification, typed rework packets, proof fingerprints, recovery ledger/backoff, and test suite stabilization.

## Positive findings

### Finalizer mode-aware runtime composition looks substantially improved

`MafAgentRuntime.AgentFactory.cs` now creates the finalizer capture from both `StructuredOutput` and `FinalizerMode`, and `CreateFinalizerCapture(...)` returns null for disabled finalizer mode. This addresses the earlier split-brain risk where the runtime could instruct exact-once finalizer behavior while the execution layer treated the run as shadow or disabled.

### Dedicated policy block exception exists

`AgentToolPolicyBlockedException` exists and `AgentToolPolicyBlockGuard.ThrowIfBlocked(...)` is used. This is the correct direction because policy-block errors should not be confused with real tool runtime failures.

### Provider feature matrix is better aligned than before

`WorkspaceBackedAgentProviderProfileRegistry` now uses the resolved provider feature matrix for `SupportsStructuredOutput`, and OpenAI/Azure OpenAI chat-completions structured-output support is represented more accurately than in earlier snapshots.

## Critical gaps

### P0 — committed real-looking OpenAI API key remains

`src/CanDoItAll.Web/appsettings.json` line 33 still contains a value matching an OpenAI API key pattern. The exact value is intentionally redacted from this bundle. Source removal is not sufficient; the key must be revoked/rotated outside the repository.

### Codex report mismatch

The round 3 report claims that secret scanning and recovery models were implemented, but the snapshot does not contain:

- `tests/CanDoItAll.Tests.Unit/SecretScanningTests.cs`
- `AgentRecoveryModels.cs`
- `AgentRecoveryModelsTests.cs`
- concrete typed `AgentReworkPacket` implementation
- proof fingerprint implementation
- recovery ledger/backoff implementation

This must be treated as a snapshot-integrity problem, not merely an implementation backlog.

### Process mutation tools are not classified as mutation tools

`AgentToolInvocationPolicyMetadata.IsMutationTool(...)` classifies workspace mutation tools but not process tools. Process tools such as `processes_definition_save`, `processes_definition_publish`, `processes_definition_delete`, `processes_definition_import`, `processes_run_start`, `processes_step_transition`, `processes_assignment_resolve`, `processes_artifact_record`, and `processes_template_import` are state-changing and must not default to `Read`.

### Process tools are attached without approval wrapping

`AttachInternalProcessToolsAsync(...)` adds all process tools directly to `composition.State.Tools`. If process tools are available to agents, process mutations need explicit policy, classification, approval behavior, and sequence significance.

### Recovery remains text-first

The dispatcher retries the current process step with a text recovery directive and typically resets the chat session. That is safe, but not optimal for partial implementation and QA repair flows. There is no typed `AgentRecoveryDecision`, no typed `AgentReworkPacket`, no durable retry ledger, no proof fingerprint reuse, and no strong separation between format repair, fresh step retry, and rework continuation.

### Broad test suite remains structurally unstable

The attached snapshot contains test harness issues consistent with Codex's own report:

- Playwright fixtures call `dotnet run --no-build` without `--configuration Release`.
- MCP stdio tests hardcode `C:epositories\CanDoItAll` and Debug assembly paths.
- Live-process integration tests lack a clear default-suite gate.
- Component tests appear brittle and likely assert transient UI/canvas details.

## Required next step

Run Codex against the included master prompt and subbundles. Require a final execution report that lists files actually created/changed, targeted tests, broad tests, skipped/quarantined tests, and explicit unresolved risks.
