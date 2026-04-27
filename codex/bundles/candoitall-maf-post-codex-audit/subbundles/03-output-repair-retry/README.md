# Subbundle 03 — Structured Output Repair/Retry

## Goal

Implement bounded repair for invalid structured output while preserving validation and finalizer safety.

## Current problem

Invalid structured output fails immediately. Repair DTOs exist, but no concrete repair service is used.

## Implementation tasks

1. Introduce a concrete repair service.

Suggested interface:

```csharp
public interface IAgentOutputRepairService
{
    Task<AgentOutputRepairAttemptResult> TryRepairAsync(
        AgentOutputRepairRequest request,
        CancellationToken cancellationToken);
}
```

2. Repair prompt requirements.

The repair prompt must include:

- Contract key.
- Schema name/description.
- Raw invalid output, redacted if needed.
- Validation errors.
- Instruction to output only the target JSON object.

It must not include unrelated conversation history.

3. Integrate repair into completion validation.

Flow:

```text
validate raw output
if valid -> continue
if invalid and repairAttemptsRemaining > 0 -> repair
validate repaired output
if valid -> use repaired output and log attempt
if invalid -> fail
```

4. Interaction with finalizers.

For required finalizer mode, prefer finalizer output when valid. Repair should apply to the selected machine-output source. Do not let repair bypass finalizer exact-one policy.

Recommended policy:

- If required finalizer missing/duplicate: do not repair; fail.
- If required finalizer present but invalid shape: optional repair of finalizer JSON is allowed only if the finalizer call count is exactly one.
- If structured output invalid in shadow/no-finalizer mode: repair structured response text.

5. Observability.

Log:

- original raw output hash
- repaired raw output hash
- attempt count
- validation errors before/after
- repair model/provider if different

6. Tests.

Required tests:

- Malformed JSON triggers one repair and succeeds.
- Semantically invalid JSON triggers repair and succeeds.
- Repair output is re-validated.
- Repair output with invalid schema fails.
- Retry limit is enforced.
- Required finalizer missing is not silently repaired as a normal text response.

## Execution Result

Status: Complete. Added `IAgentOutputRepairService`, bounded repair metadata, default conservative JSON-object repair, revalidation, and repair telemetry. Required finalizer failures remain hard failures.

## Acceptance gate

No invalid structured output may be persisted as success, and repair must never run indefinitely.
