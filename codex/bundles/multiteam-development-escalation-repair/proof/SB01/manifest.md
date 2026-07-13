# SB01 Proof Manifest

## Subbundle

- Subbundle: `01-live-run-escalation-diagnosis`
- Status: `Completed`
- Owned requirement: diagnose the false escalation path in the live Calculator multiteam run.

## Changed Files And Hashes

| File | SHA-256 |
| --- | --- |
| `repo://codex/bundles/multiteam-development-escalation-repair/analysis/01-current-state.md` | `14077239CDA6BE3CC0658AE586C9528030A31BF400315022871DF46CC83C1596` |

## Proof Artifacts

- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`
- Failing-first transcript: `bundle://proof/SB01/transcripts/failing-first.txt`
- Passing transcript: `bundle://proof/SB01/transcripts/passing.txt`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub.txt`
- Source assertion: `repo://codex/bundles/multiteam-development-escalation-repair/analysis/01-current-state.md`

## Closure

- Failing-first: `bundle://proof/SB01/transcripts/failing-first.txt` records the original false escalation diagnosis.
- Semantic positive proof: `bundle://proof/SB01/transcripts/passing.txt` records the repaired run status and launch readiness proof.
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub.txt`.
