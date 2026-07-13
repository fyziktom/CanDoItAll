# SB15 Proof Manifest

- Status: `Completed`
- Owned requirement: R15
- Semantic invariant contract: `bundle://proof/SB15/semantic-invariants.md`

## Required Artifacts

- `bundle://proof/SB15/changed-file-hashes.txt`
- `bundle://proof/SB15/transcripts/failing-first.txt`
- `bundle://proof/SB15/transcripts/passing-tests.txt`
- `bundle://proof/SB15/transcripts/source-assertions.txt`
- `bundle://proof/SB15/transcripts/anti-stub-audit.txt`
- `bundle://proof/SB15/transcripts/codeanalytics.txt`

## Closure Evidence

- A diagnostic identity is the hash of its normalized diagnostic code and evidence hash, independent of incidental diagnostics in the same batch.
- One automatic retry is allowed for a new safe/idempotent completion-gate blocker. The same identity on the next attempt requires manager action.
- Replacement diagnostics still receive the bounded global retry opportunity.
- Generic runtime and application source contain no .NET, Blazor, browser, QA, or sample-application policy.
- Focused recovery tests, the 1,998-test unit suite, and the solution build passed.
