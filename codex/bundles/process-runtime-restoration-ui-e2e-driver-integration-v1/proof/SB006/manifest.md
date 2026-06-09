# SB006 Proof Manifest

Status: `Completed`

## Changed File Hashes

| Path | SHA-256 |
| --- | --- |
| `repo://src/CanDoItAll.Processes.Drivers.Abstractions/Evidence/ProcessDriverEvidencePolicy.cs` | `3AEB5F521FE64DB94B6B5666F52C0D876B1276328122645F73C2AB4EAA679CB4` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` | `81A952FB60033B1D9C814A26533A2247F3CE70FD87CB89F30937A0869DE3D726` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractApiVerificationBoundaryTests.cs` | `64C4CDA5F2C1CFFD13860ED5F0476A7475F4FEF728DDEC7FDAD469FD2E4DD306` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractPrerequisitesVerificationTests.cs` | `AF1C3B436E1A1A040106E1FBAB72E83E27687100A87587566537BC9D4199022C` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverFakeProofResistanceTests.cs` | `709F80716C3F4F05C60798CCF222848EB5E802522C99648195D6301413DBAC87` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverTranscriptVerificationAlphaTests.cs` | `E4047119F7DB47B02A0E20B1761A1C0F7C3FA2DBD62D13424B2DD1053DB3C067` |
| `repo://tests/CanDoItAll.Tests.Unit/SecretScanningTests.cs` | `4BAD269D838E7CE6D798E70613CD71724360E1CAB74C6D613040D34E9C260CB8` |

## Command Transcripts

| Proof | Transcript | Result |
| --- | --- | --- |
| Failing-first transient bundle dependency scan against HEAD | `bundle://proof/SB006/transcripts/failing-first-head-bundle-path-scan.txt` | Failed as expected |
| Passing transient bundle dependency scan against working tree | `bundle://proof/SB006/transcripts/passing-working-tree-bundle-path-scan.txt` | Passed |
| Focused architecture/evidence guard tests | `bundle://proof/SB006/transcripts/focused-bundle-path-guard-tests.txt` | Passed |
| Anti-stub audit | `bundle://proof/SB006/transcripts/anti-stub-audit-changed-files.txt` | Passed |
| Full unit suite | `bundle://proof/SB006/transcripts/full-unit-tests.txt` | Passed |

## Transcript Hashes

| Path | SHA-256 |
| --- | --- |
| `bundle://proof/SB006/transcripts/failing-first-head-bundle-path-scan.txt` | `9BB81F4E132A4AC6AA5156BAB61388B54D067505F6FC2011A544980FE83500CE` |
| `bundle://proof/SB006/transcripts/passing-working-tree-bundle-path-scan.txt` | `0702004CCF4A548A9D2B39A7A5B919E3F92A66CC824AC59D8529A54A3606B9C2` |
| `bundle://proof/SB006/transcripts/focused-bundle-path-guard-tests.txt` | `AA1DAD39A7B3ACCA5A64A7056276717208A388F2F6330570A18FE859BCA41E55` |
| `bundle://proof/SB006/transcripts/anti-stub-audit-changed-files.txt` | `324EA7EE319B3203B978D30F69B959E21A82229A6BE60264DF04BEF71E830FD6` |
| `bundle://proof/SB006/transcripts/full-unit-tests.txt` | `35B3F5270B5F165FA166305F60AA8BFC92606F10B248A0876784E13A0D724E9B` |

## Semantic Evidence

- Semantic invariant contract: `bundle://proof/SB006/semantic-invariants.md`
- Source assertions: stable fixture readers replace direct bundle folder reads in `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractApiVerificationBoundaryTests.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractPrerequisitesVerificationTests.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverFakeProofResistanceTests.cs`, and `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverTranscriptVerificationAlphaTests.cs`.
- Source assertions: `repo://tests/CanDoItAll.Tests.Unit/SecretScanningTests.cs` skips transient bundle artifacts by path segments so historical bundle transcripts cannot break full unit proof.
- Source assertions: `repo://src/CanDoItAll.Processes.Drivers.Abstractions/Evidence/ProcessDriverEvidencePolicy.cs` accepts stable `bundle://`, `process://`, `artifact://`, and `repo://tests/` supplied evidence URIs and does not approve `repo://codex/bundles/`.
- Failing-first transcript: `bundle://proof/SB006/transcripts/failing-first-head-bundle-path-scan.txt`
- Passing transcript: `bundle://proof/SB006/transcripts/passing-working-tree-bundle-path-scan.txt`
- Anti-stub audit transcript: `bundle://proof/SB006/transcripts/anti-stub-audit-changed-files.txt`

## Raw Note Closure

| Raw note | Status | Proof |
| --- | --- | --- |
| Tests contain bundle names and bundle folders are being deleted. | Solved | `bundle://proof/SB006/transcripts/passing-working-tree-bundle-path-scan.txt`, `bundle://proof/SB006/transcripts/focused-bundle-path-guard-tests.txt`, and `bundle://proof/SB006/transcripts/full-unit-tests.txt`. |

