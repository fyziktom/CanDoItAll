# SB01 Proof Manifest

## Status
- Subbundle: SB01
- Status: Completed
- Owned requirements: REQ-001, REQ-002
- Raw notes: RN-001, RN-004
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`

## Changed File Manifest
| Path | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs` | `a92dda19dd04f99621660a942e327375aeb83d94d414a17c0133436c2ea39fe5` | `0e267c8114195ddbd7103e41e050e0a41cde67ccfa0f7ff397b4a9ab082c9d67` |

## Command Transcripts
- Passing transcript: `bundle://proof/SB01/transcripts/focused-guard-test.txt`
- Failing-first transcript: `bundle://proof/SB01/transcripts/failing-first-source-assertion.txt`
- Source assertion transcript: `bundle://proof/SB01/transcripts/source-assertions.txt`
- Production source coupling scan: `bundle://proof/SB01/transcripts/source-coupling-scan.txt`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`

## Source Assertions
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs` contains `Process_runtime_host_codefirst_SB01_INV_011_ratio_failure_is_advisory_when_runtime_release_evidence_is_green`.
- `bundle://proof/SB01/transcripts/source-assertions.txt` proves the advisory and functional-blocker reasons are both represented.

## Failing-First And Passing Proof
- Failing-first: `bundle://proof/SB01/transcripts/failing-first-source-assertion.txt` exits non-zero because baseline `HEAD` lacks `SB01_INV_011`.
- Passing: `bundle://proof/SB01/transcripts/focused-guard-test.txt` exits zero and includes `Process_runtime_host_codefirst_SB01_INV_011_ratio_failure_is_advisory_when_runtime_release_evidence_is_green`.

## Anti-Stub Audit
- `bundle://proof/SB01/transcripts/anti-stub-audit.txt` reports no `TODO`, `NotImplemented`, or `fixture-specific` markers in the changed guard file.

## Browser Or Host Proof
- N/A. SB01 changes only test policy proof and bundle records.

## Downstream Smoke
- SB02 entry may proceed because SB01 now distinguishes functional runtime blockers from advisory code/proof churn.
