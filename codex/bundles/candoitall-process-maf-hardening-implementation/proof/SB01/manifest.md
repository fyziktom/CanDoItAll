# Proof Manifest - SB01

Status: `Completed`

## Owned Requirements

- R09, R14, R15 inventory baseline.

## Semantic Invariant Contract

- `bundle://proof/SB01/semantic-invariants.md`

## Evidence

- Failing-first transcript: `bundle://proof/SB09/transcripts/adversarial-negative.md`
- Passing transcript: `bundle://proof/SB01/transcripts/template-inventory.txt`
- Passing transcript: `bundle://proof/SB01/transcripts/source-test-inventory.txt`
- Passing transcript: `bundle://proof/SB09/transcripts/final-validation.md`
- Anti-stub audit transcript: `bundle://proof/SB09/transcripts/anti-stub-audit.md`
- Changed-file hashes: `bundle://proof/SB09/changed-file-hashes.md`
- Representative SHA-256: `sha256:2306a5b8684a264a405af100ffe43d67bec10f74c041b7da1960072010a5d91b`

## Source Assertions

- `bundle://inventories/01-scope-inventory.md`
- `bundle://inventories/02-subprocess-contract-inventory.md`
- CodeAnalytics scoped snapshot: `snap-20260708111133-0494a6f9`; dependency cycles `[]`.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Inventory artifacts | `bundle://inventories` | Later subbundle READMEs | Phase gates in `bundle://plan/01-phase-plan.md` | Missing-template-parent audit fails gate |
