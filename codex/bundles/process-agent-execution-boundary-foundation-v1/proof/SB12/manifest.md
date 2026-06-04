# SB12 Proof Manifest

- Status: Completed.
- Owned requirements: RQ-014, RQ-013.
- Semantic invariant contract: `bundle://proof/SB12/semantic-invariants.md`.
- Browser proof: N/A because SB12 changed no rendered UI route.

## Changed-File Hashes

| Path | SHA-256 |
| --- | --- |
| `bundle://proof/SB12/source-assertions/final-red-team-next-core-cutline.txt` | `E72F2E27E47B91BF7E5FFDB6606688B3391DF170BC1A853298EBC95017D963D8` |
| `bundle://proof/SB12/semantic-invariants.md` | `1AD0D52A18DFEA16B86F60AA0D025AB50F82A07F5CC2E1D756CF4158C47D908F` |
| `bundle://subbundles/12-12-final-red-team-and-next-core-cutline/README.md` | `C4F551BAEAB96AF626D30FB8C09961B62F84BBB6D701CA63CBD0423FD16AB1D0` |
| `bundle://reviews/01-execution-report.md` | `811471751345B9CF4C66B1EEA7CF4AC78D537ED4F752F515BE2AAB96E39304A4` |

## Command Transcripts

- Hidden dependency final scan: `bundle://proof/SB12/transcripts/hidden-dependency-final-scan.txt`.
- Dispatcher direct coupling final scan: `bundle://proof/SB12/transcripts/dispatcher-direct-coupling-final-scan.txt`.
- No Process Core/driver final scan: `bundle://proof/SB12/transcripts/no-core-driver-project-final-scan.txt`.
- UI diff final scan: `bundle://proof/SB12/transcripts/ui-diff-final-scan.txt`.
- No forbidden viewport proof path final scan: `bundle://proof/SB12/transcripts/no-forbidden-viewport-proof-path-final-scan.txt`.
- Requirement traceability review: `bundle://proof/SB12/transcripts/requirement-traceability-review.txt`.
- Hash capture: `bundle://proof/SB12/transcripts/hashes.txt`.

## Failing-First And Passing Proof

- Failing-first: N/A - SB12 is final review and cutline proof with no production behavior change.
- Passing transcript: `bundle://proof/SB12/transcripts/hidden-dependency-final-scan.txt`.
- Passing transcript: `bundle://proof/SB12/transcripts/dispatcher-direct-coupling-final-scan.txt`.
- Passing transcript: `bundle://proof/SB12/transcripts/requirement-traceability-review.txt`.
- Passing transcript: `bundle://proof/SB11/transcripts/process-filtered-integration-tests.txt`.
- Invariant labels: `SB12_INV_001`, `SB12_INV_002`, `SB12_INV_003`.

## Source Assertions

- Final red-team and next Process Core cutline: `bundle://proof/SB12/source-assertions/final-red-team-next-core-cutline.txt`.

## Anti-Stub Audit

- Anti-stub transcript: `bundle://proof/SB12/transcripts/anti-stub-audit.txt`.
