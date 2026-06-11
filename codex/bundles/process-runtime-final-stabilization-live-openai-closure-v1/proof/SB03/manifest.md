# SB03 Proof Manifest

## Status
- Subbundle: SB03
- Status: Completed
- Owned requirements: REQ-005
- Raw notes: RN-001, RN-004
- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`

## Changed File Manifest
| Path | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs` | `1b015fc87d1e252d8be309e484fc60de19ac354fd1d7df06cd05576d3436b722` | `1b015fc87d1e252d8be309e484fc60de19ac354fd1d7df06cd05576d3436b722` |
| `repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs` | `124cb6da1d97e979d525c97effdce128b830ff7a8b9d0950c9ec7be12187a4ae` | `124cb6da1d97e979d525c97effdce128b830ff7a8b9d0950c9ec7be12187a4ae` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs` | `5412aa9fece197b982b0f16571bf478a4382af5d0d7b5b0935eca53e5a9d7fb2` | `5412aa9fece197b982b0f16571bf478a4382af5d0d7b5b0935eca53e5a9d7fb2` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs` | `78c5f534c3a51369ad8ac5eddaf0903954e715a635d97ca0e9b7cb9d6970b7dc` | `78c5f534c3a51369ad8ac5eddaf0903954e715a635d97ca0e9b7cb9d6970b7dc` |

## Command Transcripts
- Focused integration matrix: `bundle://proof/SB03/transcripts/focused-integration-matrix.txt`
- Suppress automation dispatch scan: `bundle://proof/SB03/transcripts/suppress-automation-dispatch-scan.txt`
- PostgreSQL classification: `bundle://proof/SB03/transcripts/postgresql-classification.txt`
- Source assertion transcript: `bundle://proof/SB03/transcripts/source-assertions.txt`
- Anti-stub audit transcript: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`
- Failing-first applicability note: `bundle://proof/SB03/transcripts/failing-first-validation-note.txt`

## Artifact Hashes
| Artifact | SHA-256 |
| --- | --- |
| `bundle://proof/SB03/transcripts/focused-integration-matrix.txt` | `d81449dc9d18ebc1fd44d29185c35e97c600017cf64407639fd821e95cf46be6` |
| `bundle://proof/SB03/transcripts/suppress-automation-dispatch-scan.txt` | `00fdc2ae2dd4544dfcda60af8da4168aea7d652fdf496798cfa2cf405a03c95a` |
| `bundle://proof/SB03/transcripts/postgresql-classification.txt` | `857e002899fa7064605f3a381f52239521f560610347aa3c164d8365c25ead70` |
| `bundle://proof/SB03/transcripts/source-assertions.txt` | `a4a4a8379939da00dc5d4677c79aba82dffee177a87c8134f512489bfbb803a9` |
| `bundle://proof/SB03/transcripts/anti-stub-audit.txt` | `3a6efeea6e780aec9052941eece7938e62c251e5c845b88ffb57d76391615886` |
| `bundle://proof/SB03/transcripts/failing-first-validation-note.txt` | `24bc69c7f35136f47005ec727aa336aca5877f74a161d05970d61e59f4a9c873` |

## Runtime Matrix
- Blazor automation dispatch/finalizer/readback passed.
- Multi-team software delivery automation dispatch passed.
- Business-plan PostgreSQL automation dispatch/finalizer/readback passed with PostgreSQL available.
- Runtime-host readback on real process run and step ids passed.
- Scheduler/workflow read-only job modeling and runner lifecycle readback passed.
- Scheduler/workflow origin runs started through the process-owned path without driver hooks.

## Source Assertions
- `bundle://proof/SB03/transcripts/suppress-automation-dispatch-scan.txt` proves automation proof methods do not set `SuppressAutomationDispatch = true`.
- `bundle://proof/SB03/transcripts/postgresql-classification.txt` proves PostgreSQL was available and the PostgreSQL test was a pass, not a skip.
- `bundle://proof/SB03/transcripts/source-assertions.txt` proves all representative matrix methods and supporting classification transcripts are present.

## Failing-First And Passing Proof
- Failing-first: N/A; no production behavior change in this process validation subbundle.
- Passing: `bundle://proof/SB03/transcripts/focused-integration-matrix.txt` exits zero with 7/7 integration tests passing.

## Anti-Stub Audit
- `bundle://proof/SB03/transcripts/anti-stub-audit.txt` reports no `TODO`, `NotImplemented`, or `fixture-specific` markers in SB03 referenced integration source files.

## Browser Or Host Proof
- N/A. SB03 validates deterministic integration behavior; SB04 owns browser proof.

## Downstream Smoke
- SB04 may proceed because deterministic runtime matrix proof is green.
