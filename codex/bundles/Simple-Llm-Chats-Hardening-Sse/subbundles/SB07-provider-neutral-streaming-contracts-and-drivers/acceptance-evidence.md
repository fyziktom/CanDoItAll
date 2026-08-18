# Acceptance evidence — SB07

For each criterion, provide behavioral/source evidence rather than only a test count.

- [ ] Existing ILlmInvocationPort callers remain source- and behavior-compatible.
- [ ] OpenAI, Azure OpenAI, and Ollama produce incremental text through one provider-neutral contract.
- [ ] A non-incremental supported driver uses a deterministic single-delta fallback or typed unsupported result.
- [ ] No automatic retry occurs after the first emitted delta.
- [ ] Every actual provider dispatch attempt receives a distinct monotonic audit ordinal and deterministic outcome.
- [ ] Streaming failures expose no credentials, raw frames, or raw provider errors.

## Required semantic proof

- Intended case:
- Negative/race/crash/failure case:
- Why the old implementation would fail this proof:
- Exact source owner:
- Exact command(s):
- Actual result:
- Evidence artifact:
- Commit SHA:
