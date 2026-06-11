# SB01 Proof Manifest

## Status
Completed.

## Owned Requirements And Notes
- REQ-001: Re-check current source/test delta and enforce a strict source/test-heavy code-first ratio.
- Raw note: Review real code and test outcome.
- Raw note: Keep code-first, fewer larger subbundles.

## Semantic Contract
- `bundle://proof/SB01/semantic-invariants.md`

## Changed File Hashes
| File | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs` | `68BA8C3E45D60F52532430D281CE97FA41DB317AA0303E0B46828C694B63DBAC` | `2E26F49903EC61823981D74672B2AB7C1FFEB82B5D0FF310BF15DB3641E737B5` |

## Command Transcripts
- Failing-first proof: `bundle://proof/SB01/transcripts/failing-first-source-assertion.txt`
- Passing proof: `bundle://proof/SB01/transcripts/focused-test.txt`
- Source assertions: `bundle://proof/SB01/transcripts/source-assertions.txt`
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`

## Semantic Proof
- Test name: `Process_runtime_host_codefirst_SB01_INV_005_numstat_summary_accepts_exact_five_to_one_source_test_dominance`
- Shallow-pass trap: a 4x guard or prose-only report would still permit bundle-heavy closure.
- Adversarial negative proof: `bundle://proof/SB01/transcripts/failing-first-source-assertion.txt`
- Semantic positive proof: `bundle://proof/SB01/transcripts/focused-test.txt`
- Source proof: `bundle://proof/SB01/transcripts/source-assertions.txt`
- Anti-stub audit: no TODO or NotImplemented markers in the changed guard test, proven by `bundle://proof/SB01/transcripts/anti-stub-audit.txt`.

## Downstream Decision
SB02 can start. SB01 changed no Process Core or dispatch runtime files and established the 5x code-first executable guard required by final closure.
