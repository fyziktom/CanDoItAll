# Post-Codex Requirements

## R01 — Required finalizer mode must be enforceable in the process path

Critical process-step automation must be able to require exact-one finalizer tool calls. Shadow mode is allowed only as a transitional telemetry mode or for explicitly non-critical runs.

Acceptance:

- Process automation can set `agentFinalizerMode = "required"` through a typed invocation setting or metadata builder.
- Missing, duplicate, or invalid finalizer tool calls fail a required-mode run before completion.
- Required mode is covered by unit/integration tests on the actual process execution path.

## R02 — Persisted assistant transcript must match finalized machine output

When required finalizer mode replaces the runtime response text with finalizer JSON, the assistant chat message persisted for the run must contain the finalized text, not the pre-finalizer response.

Acceptance:

- Validation/finalization occurs before `ChatMessageRecord` is constructed, or the message content is updated after finalization.
- Tests cover both initial execution and approval-continuation execution.

## R03 — Bounded repair/retry must exist for invalid structured output

Invalid structured output should not immediately fail if a safe repair attempt is configured. Repair must be bounded and re-validated.

Acceptance:

- A concrete repair service exists.
- Default max repair attempts is 1 for governed process runs, configurable up to 2.
- Repaired output is deserialized, validated, and finalizer/policy checked.
- If repair fails, the run fails with validation details and raw output hashes.

## R04 — Provider capability matrix must match MAF semantics

Structured output and tool approval support must be modeled by actual provider/client/transport support, not oversimplified assumptions.

Acceptance:

- Compatible Chat Completion clients are not categorically rejected for structured outputs.
- Tool approval support is not treated as equal to function-tool support.
- Tests are updated to reflect documented support and known unsupported combinations.

## R05 — `RequireApproval` policy must prevent execution when no effective approval mechanism exists

If policy returns `RequireApproval`, the tool must either be wrapped/effectively supported or execution must be blocked/pended before the underlying tool runs.

Acceptance:

- A mutation tool with no wrapper or unsupported approval transport cannot execute.
- Middleware explicitly handles `RequireApproval`.
- Tests prove blocked execution before calling `next(...)`.

## R06 — Validators must be null-safe and exception-safe

Validation must produce structured validation errors, not unhandled exceptions, for missing/null fields.

Acceptance:

- All validators handle null collections/nested objects.
- `AgentOutputJson.DeserializeAndValidate(...)` catches validator exceptions and returns an `agent.output.validator_exception` error.
- Tests cover missing and explicit-null collections.

## R07 — Critical DTO finalizers and contract keys must be explicit

If a DTO is machine-critical, it needs either a required finalizer policy or an explicit documented reason why structured output alone is sufficient.

Acceptance:

- `ProcessStepOutcomeResult` has required-mode coverage.
- `CodeReviewResult`, `ArchitectureReviewResult`, `ImplementationPlanResult`, `TestPlanResult`, and `ToolExecutionDecisionResult` are either finalizer-enabled or documented as non-finalizer contracts.
- Known contract registry includes all contracts used across continuations.

## R08 — Build/test evidence is mandatory

Codex must produce real command output.

Acceptance:

- `dotnet --info`
- `dotnet restore`
- `dotnet build`
- `dotnet test`
- Any skipped command has a precise reason.

## R09 — Domain-specific recovery guidance must be pluggable

Calculator recovery guidance should not be hardcoded into generic process automation long-term.

Acceptance:

- Domain recovery guidance is moved behind a strategy/provider/template abstraction, or explicitly documented as a temporary test fixture.

## R10 — Observability must include repair/finalizer/tool-policy results

Acceptance:

- Logs/traces include finalizer mode/status, repair attempt count, raw output hashes, validation errors, tool policy decision, approval effective/unsupported flag, and final outcome.
