# Release Readiness Gate

Do not accept the MAF stabilization work as complete until all gates pass.

## Gate A — Structured output

- [x] Every machine-critical run has a structured-output contract or typed finalizer.
- [x] No machine-critical run relies only on prompt text such as "return JSON".
- [x] No `ResponseFormat` contract uses top-level primitive/array output.
- [x] Approval continuations preserve structured-output contract.

## Gate B — Finalizers

- [x] Process-step automation can require exact-one finalizer.
- [x] Required finalizer mode is actually enabled for critical process runs or explicitly configured.
- [x] Missing finalizer fails required mode.
- [x] Duplicate finalizer fails required mode.
- [x] Invalid finalizer fails required mode.
- [x] Persisted assistant text matches finalized machine output.

## Gate C — Validation and repair

- [x] Invalid JSON fails validation or is repaired within bounded attempts.
- [x] Semantically invalid JSON fails validation or is repaired within bounded attempts.
- [x] Repair output is re-validated.
- [x] Validators are null-safe.
- [x] Validator exceptions become structured validation errors.

## Gate D — Tool governance

- [x] Read-only tools are allowed by policy.
- [x] Unknown tools are denied.
- [x] Mutation/destructive tools require effective approval.
- [x] `RequireApproval` never proceeds to underlying execution without an effective approval mechanism.
- [x] Provider capability matrix distinguishes ordinary function tools from approval support.

## Gate E — Provider matrix

- [x] Compatible chat clients may use structured output.
- [x] Unsupported clients are rejected for machine-critical structured output.
- [x] Tool approval support is narrower than generic tool support.
- [x] Tests reflect current MAF docs and actual installed package behavior.

## Gate F — Observability

- [x] Structured output contract key and raw output hash are logged/traced.
- [x] Finalizer mode/status/count/raw hash are logged/traced.
- [x] Repair attempt count and hashes are logged/traced.
- [x] Tool policy decision and approval effectiveness are logged/traced.

## Gate G — Build/test proof

- [x] `dotnet --info` captured.
- [x] `dotnet restore` completed.
- [x] `dotnet build` completed.
- [x] Focused `dotnet test` proof completed and passed.
- [x] Repo-wide `dotnet test` caveats have precise reasons.

Repo-wide acceptance caveat: full solution testing timed out after 10 minutes, and the full integration project currently reports 421 passed / 30 failed from unrelated existing environment and test-data failures. Bundle-surface tests pass.
