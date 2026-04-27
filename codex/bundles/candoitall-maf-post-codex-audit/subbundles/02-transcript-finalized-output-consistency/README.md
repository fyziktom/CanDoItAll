# Subbundle 02 — Transcript Consistency After Finalizer Validation

## Goal

Ensure persisted assistant messages and execution details reflect the finalized machine output after required finalizer validation.

## Current problem

`ChatMessageRecord` is created before `ValidateMachineOutputBeforeCompletionAsync(...)`. Required finalizer validation can replace `runtimeResponse.ResponseText` later, creating a mismatch between persisted chat transcript and the final machine output.

## Implementation tasks

1. Move validation/finalization before assistant message creation on the initial run path.

Current pattern:

```csharp
var assistantMessage = new ChatMessageRecord(... Content: runtimeResponse.ResponseText ...);
runtimeResponse = await ValidateMachineOutputBeforeCompletionAsync(...);
```

Target pattern:

```csharp
runtimeResponse = await ValidateMachineOutputBeforeCompletionAsync(...);
var assistantMessage = new ChatMessageRecord(... Content: runtimeResponse.ResponseText ...);
```

2. Apply the same ordering on the approval-continuation path.

3. Verify metrics still use the correct token counts and tool counts. Validation should not reset runtime accounting.

4. Add regression tests.

Required tests:

- Required finalizer replaces response text and persisted assistant message uses finalizer JSON.
- Continuation path has the same behavior.
- Shadow mode does not replace response text.
- Invalid output still fails before persistence mutation.

## Acceptance gate

No persisted assistant message may contain stale pre-finalizer machine output in required finalizer mode.

## Execution Result

Status: Complete. Initial and approval-continuation execution paths now validate and finalize machine output before assistant transcript persistence.
