# Session handoff — SB07

State: **Locked**

## Entry checklist

- [ ] Root bundle status read
- [ ] Dependencies complete and proof trusted
- [ ] Actual repository/branch/head recorded
- [ ] Current source and nearby tests inspected
- [ ] Test budget understood
- [ ] Database/dependency mode recorded

## Work performed

Pending.

## Files changed

Pending.

## Commands and results

Pending. Include exact command, exit code, passed/failed/skipped counts and evidence path.

## Bugs discovered and resolved

Pending.

## Deviations

Pending. `None` is acceptable only after review.

## Acceptance result

- [ ] Existing ILlmInvocationPort callers remain source- and behavior-compatible.
- [ ] OpenAI, Azure OpenAI, and Ollama produce incremental text through one provider-neutral contract.
- [ ] A non-incremental supported driver uses a deterministic single-delta fallback or typed unsupported result.
- [ ] No automatic retry occurs after the first emitted delta.
- [ ] Every actual provider dispatch attempt receives a distinct monotonic audit ordinal and deterministic outcome.
- [ ] Streaming failures expose no credentials, raw frames, or raw provider errors.

## Architecture result

- [ ] Owner moved or strengthened as planned
- [ ] Old shallow path removed/unreachable
- [ ] Direct tests target the new owner
- [ ] No forbidden reference/cycle/partial expansion
- [ ] Architecture record updated if design changed

## Progression

Pending. Use `Ready`, `Blocked`, or `Reopened`; explain downstream impact.
