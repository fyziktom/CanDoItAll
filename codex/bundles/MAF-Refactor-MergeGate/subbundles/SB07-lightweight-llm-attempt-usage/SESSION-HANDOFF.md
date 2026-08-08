# Session handoff — SB07

## Repository state

- Branch: `maf-refactor`
- Starting SHA: `d27fb8307aac6a5f88be6504eb5a284ca41ac1f6`
- Current SHA: `d27fb8307aac6a5f88be6504eb5a284ca41ac1f6`
- Worktree: dirty with cumulative SB00-SB07 implementation and proof changes; no commit was requested or created.

## Completed

- Converted `LlmUsage` into a validated immutable value with checked aggregation.
- Accumulated every reported attempt before inspecting response text.
- Returned aggregate usage on retry success and attached known aggregate usage to typed empty/provider/deadline failures.
- Preserved caller cancellation and kept provider-error behavior single-attempt after the preceding empty retry.
- Projected typed failure usage into workflow observations while preserving zero/missing behavior when usage is unknown.
- Reused checked aggregation for ordinary-conversation total usage.
- Release solution build passed with zero warnings and errors.

## In progress

- None for SB07.

## Blockers/failing tests

- None. All ten original failing-first characterizations now pass.

## Decisions

- `LlmUsage?` on typed failure distinguishes unknown usage from a known zero report.
- Invalid/overflowing provider counters become sanitized `ProviderFailure` with the last valid aggregate retained.
- Only empty terminal text triggers the existing bounded retry; provider errors are never retried.
- Caller cancellation remains `OperationCanceledException`; only the adapter's deadline becomes `DeadlineExceeded`.

## Changed files

- See `proof-manifest.json` and `../../proof/SB07/manifest.md`.

## Commands run

- See `../../proof/SB07/transcripts`.

## Next exact action

- Enter SB08 and remove dormant ordinary-conversation production registration while preserving isolated library composition.

## Risks not to forget

- Do not collapse unknown usage into a known zero inside the typed failure contract.
- Do not add provider-error retries.
- Keep raw provider detail out of public exception messages.
- Keep arithmetic checked; never saturate or silently wrap counters.
