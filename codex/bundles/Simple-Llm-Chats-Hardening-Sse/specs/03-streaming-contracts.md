# Streaming contracts

## Invocation updates

Suggested semantic contract:

```text
LlmStreamingAttemptStarted
    AttemptOrdinal
    ProviderId/Kind
    Model
    StartedAtUtc

LlmStreamingTextDelta
    AttemptOrdinal
    Delta
    ProviderSequence? (optional diagnostic, not client cursor)

LlmStreamingCompleted
    AttemptOrdinal
    Model
    FinishReason
    Usage
    CompletedAtUtc
```

Failures remain typed exceptions or a terminal update, but one convention must be selected and applied
consistently. Raw provider exceptions stay inside logs.

## Content rules

- Delta must be non-null; empty deltas are ignored.
- Aggregate assistant content is bounded by the same or stricter limit as canonical assistant messages.
- UTF-8 code points must not be corrupted at chunk boundaries.
- No chain-of-thought or hidden provider reasoning is exposed unless a separately governed future
  contract explicitly permits a safe summary.
- Structured JSON mode either streams text with a final validation result or is explicitly declared
  completed-only; do not present invalid partial JSON as a finished object.

## Usage

Usage may be unavailable until completion. Final completed update contains authoritative usage. If a
failed attempt reports partial usage, persist it in the attempt audit.

## Fallback

A completed-only provider adapter may yield:

1. one attempt-started update;
2. one text-delta containing the completed response;
3. one completed update.

The capability response must label this as `CompletedFallback`, not `Incremental`.
