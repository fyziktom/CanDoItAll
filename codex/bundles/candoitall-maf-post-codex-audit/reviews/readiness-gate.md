# Release Readiness Gate

Do not accept the MAF stabilization work as complete until all gates pass.

## Gate A — Structured output

- [ ] Every machine-critical run has a structured-output contract or typed finalizer.
- [ ] No machine-critical run relies only on prompt text such as "return JSON".
- [ ] No `ResponseFormat` contract uses top-level primitive/array output.
- [ ] Approval continuations preserve structured-output contract.

## Gate B — Finalizers

- [ ] Process-step automation can require exact-one finalizer.
- [ ] Required finalizer mode is actually enabled for critical process runs or explicitly configured.
- [ ] Missing finalizer fails required mode.
- [ ] Duplicate finalizer fails required mode.
- [ ] Invalid finalizer fails required mode.
- [ ] Persisted assistant text matches finalized machine output.

## Gate C — Validation and repair

- [ ] Invalid JSON fails validation or is repaired within bounded attempts.
- [ ] Semantically invalid JSON fails validation or is repaired within bounded attempts.
- [ ] Repair output is re-validated.
- [ ] Validators are null-safe.
- [ ] Validator exceptions become structured validation errors.

## Gate D — Tool governance

- [ ] Read-only tools are allowed by policy.
- [ ] Unknown tools are denied.
- [ ] Mutation/destructive tools require effective approval.
- [ ] `RequireApproval` never proceeds to underlying execution without an effective approval mechanism.
- [ ] Provider capability matrix distinguishes ordinary function tools from approval support.

## Gate E — Provider matrix

- [ ] Compatible chat clients may use structured output.
- [ ] Unsupported clients are rejected for machine-critical structured output.
- [ ] Tool approval support is narrower than generic tool support.
- [ ] Tests reflect current MAF docs and actual installed package behavior.

## Gate F — Observability

- [ ] Structured output contract key and raw output hash are logged/traced.
- [ ] Finalizer mode/status/count/raw hash are logged/traced.
- [ ] Repair attempt count and hashes are logged/traced.
- [ ] Tool policy decision and approval effectiveness are logged/traced.

## Gate G — Build/test proof

- [ ] `dotnet --info` captured.
- [ ] `dotnet restore` completed.
- [ ] `dotnet build` completed.
- [ ] `dotnet test` completed.
- [ ] Skipped commands have precise reasons.
