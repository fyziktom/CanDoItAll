# SB01 Proof Manifest

## Subbundle

- Subbundle: `01-critical-proof`
- Status: `Completed`
- Owned requirement: fake accepted-use signal proof.
- Test name: `FakeProof.AcceptedUseConsumerOnly`

## Changed Files And Hashes

| File | SHA-256 |
|---|---:|
| `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` | `079561F5460E68FAE447ECA8CD1D5072EEDB7CCD9A7FF09035131EAA92012D9A` |

## Proof Artifacts

- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`
- Failing-first transcript: `bundle://proof/SB01/transcripts/failing-first.txt`
- Passing transcript: `bundle://proof/SB01/transcripts/passing.txt`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub.txt`
- Source assertion: `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py`

## Closure

- Failing-first: `bundle://proof/SB01/transcripts/failing-first.txt` records a consumer-only accepted-use fake proof.
- Semantic positive proof: `bundle://proof/SB01/transcripts/passing.txt` records the consumer/evaluator/test-seed evidence as if it were complete.
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub.txt`.
