# 06. Checklists

## Architecture checklist

- [ ] Detached backend remains the source of truth for runtime state.
- [ ] Stdio bridge is treated as a repairable client of the backend, not the runtime owner.
- [ ] Runtime lanes are explicit and named in code and contracts.
- [ ] Atomic update semantics are defined in terms of logical active runtime, not hand-wavy "blue-green" language.
- [ ] Published runtime support is modeled explicitly, not hidden inside `RunOnce`.
- [ ] Slot-based runtime artifacts replace single-folder publish assumptions for managed atomic flows.
- [ ] Resource coordination is scoped beyond one global workspace lock.
- [ ] Revision identity is explicit for watch, published, and run-once sessions.
- [ ] Rollback is part of the design, not a future TODO.
- [ ] Candidate endpoint allocation is explicit and collision-safe.
- [ ] Shadow build retention and cleanup rules are documented.
- [ ] Workflow steering is a first-class control-plane concern, not ad hoc text spread across tools.
- [ ] Guidance stays out of raw log and event payloads.

## Contract checklist

- [ ] Existing tool names remain backward compatible.
- [ ] New fields are additive where possible.
- [ ] Non-idempotent calls have idempotency keys.
- [ ] Bridge failure codes are typed and actionable.
- [ ] `workspace_info` exposes bridge and lane status.
- [ ] `app_status` exposes logical app id, lane kind, revision, and slot/transaction info where relevant.
- [ ] `app_wait` can wait on revision or transaction milestones, not only health.
- [ ] `app_events` exists or an equivalent structured event stream is provided.
- [ ] `app_update_atomic` is defined as the primary Codex-safe update tool.
- [ ] `app_rollback` is defined.
- [ ] Selected status/control tools can emit compact `workflowGuidance`.
- [ ] Guidance payload shape is budgeted and additive.
- [ ] Static tool descriptions include one short small-iteration hint where it materially improves agent behavior.

## Implementation checklist

- [ ] Introduce new runtime identity models.
- [ ] Refactor backend tool invocation to support repair and safe retry.
- [ ] Add launch specs for project, published DLL, and executable.
- [ ] Replace or wrap the current `AppStartTemplate`.
- [ ] Introduce resource-scope planning.
- [ ] Add runtime slot registry and transaction persistence.
- [ ] Add candidate endpoint allocation and lease persistence.
- [ ] Implement candidate prepare, commit, and rollback orchestration.
- [ ] Surface bridge and slot state in manager models/UI.
- [ ] Add shadow build retention/cleanup logic.
- [ ] Deprecate managed dependence on `.artifacts\bundle-validation\webapp` as a single hot folder.
- [ ] Preserve self-host MCP build/test validation while the live backend is running.
- [ ] Add a centralized workflow-guidance policy instead of hand-written hint strings per tool.
- [ ] Enforce guidance suppression on high-volume responses.

## Testing checklist

- [ ] Unit tests cover bridge repair classification and retry rules.
- [ ] Unit tests cover launch-spec compatibility shims.
- [ ] Unit tests cover resource-scope conflict behavior.
- [ ] Unit tests cover slot manifest and transaction persistence.
- [ ] Integration tests cover wrapper launch plus backend repair.
- [ ] Integration tests cover published candidate prepare without mutating the active runtime.
- [ ] Integration tests cover commit and rollback.
- [ ] Integration tests cover existing watch flows to prevent regression.
- [ ] Integration tests cover self-host build/test isolation for `CanDoItAll.Mcp.DotNetWatch`.
- [ ] Failure-injection tests cover backend disappearance mid-call.
- [ ] Failure-injection tests cover candidate health failure and commit failure.
- [ ] Unit tests cover guidance selection and suppression rules.
- [ ] Integration tests prove healthy watch responses recommend small-step browser-checked iteration.
- [ ] Integration tests prove risky/pressure states recommend focused build/test or atomic-lane work.
- [ ] Integration tests prove raw logs and event streams remain guidance-free.

## Migration checklist

- [ ] Existing `WatchRun` clients continue to function.
- [ ] Existing manager UI still works for source-watch sessions.
- [ ] Existing settings file remains valid with sensible defaults for new sections.
- [ ] New settings for slots and atomic runtime use conservative defaults.
- [ ] Old manual publish validation path is documented as non-authoritative for managed atomic updates.

## Codex usage checklist

- [ ] Codex can tell whether it is on the fast lane or atomic lane from tool responses alone.
- [ ] Codex can observe revision confirmation without parsing raw logs.
- [ ] Codex can ask for an atomic update in one high-level tool call.
- [ ] Codex can roll back after a bad commit.
- [ ] Codex does not need to manually stop a running published host to prepare the next candidate.
- [ ] Codex does not see generic bridge failures for known repairable conditions.
- [ ] Codex is explicitly nudged toward one nearby UI change followed by validation before widening scope.
- [ ] Codex is explicitly nudged away from broad edit batches when watch pressure or unresolved failures are present.

## QA signoff checklist

- [ ] Bundle explicitly distinguishes current verified facts from proposed redesign.
- [ ] Atomicity semantics are defined and non-goals are clear.
- [ ] Rollback behavior is fully described.
- [ ] Validation criteria include measurable thresholds.
- [ ] Risks and mitigations are documented.
- [ ] Prompts are executable by a follow-on implementation agent.
- [ ] Final approval is conditioned on evidence, not optimistic prose.
- [ ] Guidance payload budget and emission scope are explicit and testable.
