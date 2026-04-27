# Codex Master Prompt — Post-Codex MAF Hardening Completion

You are a senior C#/.NET architect and Microsoft Agent Framework implementation engineer.

You are working in the CanDoItAll repository after an earlier Codex implementation pass. Do not start over. Preserve the good work already implemented: structured-output DTOs, `ResponseFormat` application, continuation structured-output propagation, output validators, finalizer policy, function-call middleware, tool policy, and tests.

Your task is to close the remaining correctness gaps identified in this audit bundle.

## Mandatory reading

Read these files from this bundle first:

1. `audit/post-codex-maf-stabilization-audit.md`
2. `audit/evidence-map.md`
3. `requirements/requirements.md`
4. All `subbundles/*/README.md`

## Main goals

Implement the following, in order:

1. Required finalizer mode for critical process-step runs.
2. Transcript consistency after required-finalizer output replacement.
3. Bounded structured-output repair/retry.
4. Provider capability matrix alignment with Microsoft Agent Framework semantics.
5. Effective enforcement of `RequireApproval` tool-policy decisions.
6. Null-safe validators and exception-safe validation pipeline.
7. Finalizers or explicit documented exceptions for all critical DTOs.
8. Observability fields and build/test proof documentation.
9. Optional: domain-specific recovery guidance provider if time permits.

## Hard rules

- Do not remove structured-output enforcement.
- Do not downgrade process automation back to prompt-only JSON.
- Do not parse workflow decisions from markdown.
- Do not persist unvalidated machine output as success.
- Do not allow mutation/write/destructive tools to execute if approval is required but not effective.
- Do not silently swallow invalid finalizer output.
- Do not use top-level arrays/primitives with `ResponseFormat`; use wrapper DTOs.
- Keep source-code comments in English.
- Do not introduce broad unrelated refactors.
- Do not claim tests passed unless you ran them.

## Required implementation details

### Required finalizer mode

Process automation must be able to set finalizer mode required through a typed policy or safe metadata builder. The current `MetadataJson: "{}"` in process automation is insufficient for critical runs.

### Assistant message ordering

Move `ValidateMachineOutputBeforeCompletionAsync(...)` before assistant message creation, or update the assistant message after finalization. Apply to initial and continuation paths.

### Repair/retry

Implement a bounded repair service. Default max attempts should be 1 for governed process runs. Repaired output must be re-validated and must not bypass finalizer, policy, or security checks.

### Provider capability matrix

Do not equate ordinary tool support with approval support. Do not categorically reject compatible Chat Completion structured outputs. Align tests with documented MAF behavior and actual client capabilities in this repo.

### Tool approval enforcement

If policy returns `RequireApproval`, execution must not proceed unless an effective approval path exists. Effective approval depends on both wrapper availability and provider/client support.

### Validators

Fix null handling in all validators. Add safety net in `AgentOutputJson.DeserializeAndValidate(...)` for validator exceptions.

## Required tests

Add/update tests for:

- Required finalizer mode on process automation path.
- Missing/duplicate/invalid finalizer failure in required mode.
- Persisted assistant content equals finalizer JSON in required mode.
- Structured output repair success and retry limit failure.
- Provider matrix: structured output and approval support separated.
- `RequireApproval` without effective approval path blocks execution.
- Null/missing collection fields produce validation errors, not exceptions.
- All known critical contracts resolve and have validators.

## Required command proof

Run and document:

```bash
dotnet --info
dotnet restore CanDoItAll.sln
dotnet build CanDoItAll.sln --configuration Release --no-restore
dotnet test CanDoItAll.sln --configuration Release --no-build
```

If the solution file has a different name, use the actual solution and record it.

Create/update:

```text
docs/agent-runtime-hardening-verification.md
```

Include command outputs or accurate summaries with timestamps, environment info, and skipped-command reasons.

## Final response format

When finished, provide:

1. Implementation summary.
2. Files changed.
3. Tests added/updated.
4. Exact command outputs or failure reasons.
5. Remaining risks.
6. Confirmation that no machine-critical prompt-only JSON paths remain, or a list of exceptions.
