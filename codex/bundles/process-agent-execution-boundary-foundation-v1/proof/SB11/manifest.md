# SB11 Proof Manifest

- Status: Completed.
- Owned requirements: RQ-012, RQ-013.
- Semantic invariant contract: `bundle://proof/SB11/semantic-invariants.md`.
- Browser proof: N/A because SB11 changed no rendered UI route.

## Changed-File Hashes

| Path | SHA-256 |
| --- | --- |
| `bundle://proof/SB11/source-assertions/runtime-smoke-large-screen-policy.txt` | `027495460E1BB6DD95AE64B4D29F1B2F77B29EC90093D30A9CA7963B7F56DD09` |
| `bundle://proof/SB11/semantic-invariants.md` | `A5F9DBF2B5ABABFE43B790DA8520AE738DC45485726BA9B3DC2A0FD48168B210` |
| `bundle://subbundles/11-11-runtime-smoke-large-screen-policy-check/README.md` | `8D0188A27CDBF37D5FDE7664B34BD2A15FA447B82AA435C35914072E98D9AA6C` |
| `bundle://reviews/01-execution-report.md` | `5B4179F4EEFB59FE954E477E0E3F4454C1BAA4BF2B4E24C7CEA8404B85157C86` |

## Command Transcripts

- Provider/policy unit tests: `bundle://proof/SB11/transcripts/provider-policy-unit-tests.txt`.
- Process-filtered integration timeout: `bundle://proof/SB11/transcripts/process-filtered-integration-tests.timed-out.txt`.
- Process-filtered integration rerun: `bundle://proof/SB11/transcripts/process-filtered-integration-tests.txt`.
- Full solution build: `bundle://proof/SB11/transcripts/full-solution-build.txt`.
- Git whitespace check: `bundle://proof/SB11/transcripts/git-diff-whitespace-check.txt`.
- Source trailing whitespace scan: `bundle://proof/SB11/transcripts/trailing-whitespace-source-scan.txt`.
- Hidden dependency MAF/Tooling scan: `bundle://proof/SB11/transcripts/hidden-dependency-maf-tooling-scan.txt`.
- Hidden dependency Contracts scan: `bundle://proof/SB11/transcripts/hidden-dependency-contracts-scan.txt`.
- Hidden dependency dispatcher scan: `bundle://proof/SB11/transcripts/hidden-dependency-dispatcher-scan.txt`.
- No forbidden viewport proof path scan: `bundle://proof/SB11/transcripts/no-forbidden-viewport-proof-path-scan.txt`.
- Hash capture: `bundle://proof/SB11/transcripts/hashes.txt`.

## Failing-First And Passing Proof

- Failing-first: `bundle://proof/SB11/transcripts/process-filtered-integration-tests.timed-out.txt`.
- Passing transcript: `bundle://proof/SB11/transcripts/provider-policy-unit-tests.txt`.
- Passing transcript: `bundle://proof/SB11/transcripts/process-filtered-integration-tests.txt`.
- Passing transcript: `bundle://proof/SB11/transcripts/full-solution-build.txt`.
- Test name: `AgentToolInvocationPolicyTests`.
- Test name: `MafAgentRuntimeToolProviderCompositionTests`.
- Test name: `ProcessAgentRuntimeToolProviderTests`.
- Test name: `ProcessAgentExecutionBoundaryArchitectureTests`.
- Test filter: `FullyQualifiedName~Process`.
- Invariant labels: `SB11_INV_001`, `SB11_INV_002`, `SB11_INV_003`.

## Source Assertions

- Runtime smoke and large-screen policy: `bundle://proof/SB11/source-assertions/runtime-smoke-large-screen-policy.txt`.

## Anti-Stub Audit

- Anti-stub transcript: `bundle://proof/SB11/transcripts/anti-stub-audit.txt`.
