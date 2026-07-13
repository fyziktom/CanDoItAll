# SB15 Semantic Invariants

## INV-SB15-001 Persistent Blocker Identity

- Raw requirement: when one retry reproduces the same blocker, further blind retries are unlikely to help.
- Expected behavior: the second occurrence of any stable safe/idempotent diagnostic identity routes to manager action even when other diagnostics are added, removed, or reordered.
- Disallowed implementation: aggregate-batch fingerprints, summary-text parsing, or a globally reduced retry budget.
- Failing-first proof: `bundle://proof/SB15/transcripts/failing-first.txt`.
- Passing proof: `bundle://proof/SB15/transcripts/passing-tests.txt`.

## INV-SB15-002 Generic Runtime Boundary

- Expected behavior: runtime compares typed diagnostic facts only; domain recovery advice remains in registered drivers/providers.
- Disallowed implementation: .NET, UI, browser, software-delivery, spreadsheet, Tetris, or Calculator conditions in generic runtime/dispatcher code.
- Passing proof: `bundle://proof/SB15/transcripts/source-assertions.txt`.

## Closure Result

- Result: `Passed`.
- Default recurrence budget: one automatic attempt for a new identity; manager action on recurrence.
- Global progress budget: four safe/idempotent current-step reworks when blocker identities genuinely change.
- Dependency result: no new project reference and zero cycles in the scoped architecture snapshot.
