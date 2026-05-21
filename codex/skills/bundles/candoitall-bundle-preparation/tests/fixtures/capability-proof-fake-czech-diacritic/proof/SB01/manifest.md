# SB01 Proof Manifest

## Subbundle

- Subbundle: `01-critical-proof`
- Status: `Completed`
- Owned requirement: critical artifact-backed proof validation.
- Test name: `ArtifactProof.ValidatesCompleteFixture`

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
- Source assertion: `bundle://source/english-only-professor-extraction.cs`

## Proof Claim To Code Matrix

| Capability claim | Required production source proof | Required test proof | Required negative fixture | Result |
|---|---|---|---|---|
| `Czech/diacritic` | `bundle://source/english-only-professor-extraction.cs` | `bundle://proof/SB01/transcripts/passing.txt` runs `ArtifactProof.ValidatesCompleteFixture` | `bundle://proof/SB01/transcripts/failing-first.txt` rejects an English-only negative fixture | Verified pass |

## Closure

- Failing-first: `bundle://proof/SB01/transcripts/failing-first.txt` records the shallow fixture failure.
- Semantic positive proof: `bundle://proof/SB01/transcripts/passing.txt` records the complete fixture pass.
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub.txt`.
