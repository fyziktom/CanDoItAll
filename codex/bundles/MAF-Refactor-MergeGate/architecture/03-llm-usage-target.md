# Lightweight LLM usage target

## Accounting rule

Every provider attempt that returns token usage contributes to the invocation's total usage, even when
its terminal response text is empty and the adapter retries.

Example:

```text
attempt 1: empty, input 100, output 3, cached 20
attempt 2: success, input 101, output 10, cached 20

returned usage:
  input 201
  output 13
  cached 40
```

## Failure rule

`LlmInvocationException` must carry accumulated numeric usage when known. Its public message remains
sanitized. Raw provider text and exception messages must not be exposed.

Workflow failure analytics must project accumulated usage from typed invocation failures instead of
recording fabricated zero values.

## Validation

- Token counters must be non-negative.
- Addition must not silently overflow.
- Provider failures with no reported usage remain zero/unknown according to the existing projection
  contract.
- Cancellation requested by the caller remains cancellation, not a provider failure.
- Deadline failure after a prior empty attempt retains the prior attempt's usage.
