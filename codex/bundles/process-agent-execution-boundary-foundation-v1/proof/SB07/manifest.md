# SB07 Proof Manifest

- Status: Completed.
- Owned requirements: RQ-008, RQ-011, RQ-013.
- Semantic invariant contract: `bundle://proof/SB07/semantic-invariants.md`.
- Browser proof: N/A because SB07 changed no rendered UI route.

## Changed-File Hashes

| Path | SHA-256 |
| --- | --- |
| `bundle://proof/SB07/source-assertions/gate-b-coupling-review.md` | `154D26D2494F04A98A0844E694098014631214A18A9B030EE13B87C121E7F407` |
| `bundle://proof/SB07/semantic-invariants.md` | `0B2C58C41AE07A9874CBC2C56999EA0FC79FF428783A38D3A45440121B1C978E` |
| `bundle://subbundles/07-07-refactor-gate-b-coupling-reduction-proof/README.md` | `F6D9BDF4A1A121CF6F62FF84598E86282BFCBDAE34F513BAE9106D29AC4D7063` |
| `bundle://reviews/01-execution-report.md` | `37B03308DFF76362517735F59B22ECD350E05FF7D798127F2E8872E49F51F57B` |

## Command Transcripts

- Coupling reduction scan: `bundle://proof/SB07/transcripts/coupling-reduction-scan.txt`.
- Remaining AgentFramework usage scan: `bundle://proof/SB07/transcripts/remaining-agentframework-usage-scan.txt`.
- Dispatcher partial line counts: `bundle://proof/SB07/transcripts/dispatcher-partial-line-counts.txt`.
- Gate B unit architecture/provider tests: `bundle://proof/SB07/transcripts/gate-b-unit-architecture-provider-tests.txt`.
- Gate B integration provider/receipt tests: `bundle://proof/SB07/transcripts/gate-b-integration-provider-receipt-tests.txt`.
- MAF and large-screen policy scans: `bundle://proof/SB07/transcripts/maf-and-large-screen-policy-scans.txt`.
- No core/driver project scan: `bundle://proof/SB07/transcripts/no-core-driver-project-scan.txt`.
- Hash capture: `bundle://proof/SB07/transcripts/hashes.txt`.

## Failing-First And Passing Proof

- Failing-first: N/A - SB07 is a refactor checkpoint; production movement occurred in SB06 and cites `bundle://proof/SB06/transcripts/dispatcher-direct-call-baseline.failing-first.txt`.
- Passing transcript: `bundle://proof/SB07/transcripts/coupling-reduction-scan.txt`.
- Passing transcript: `bundle://proof/SB07/transcripts/gate-b-unit-architecture-provider-tests.txt`.
- Passing transcript: `bundle://proof/SB07/transcripts/gate-b-integration-provider-receipt-tests.txt`.
- Test name: `ProcessAgentExecutionBoundaryArchitectureTests`.
- Test name: `MafAgentRuntimeToolProviderCompositionTests`.
- Test name: `ProcessAgentRuntimeToolProviderTests`.
- Test name: `ProcessRuntimeToolProviderCompositionIntegrationTests`.
- Test name: `AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests`.
- Invariant labels: `SB07_INV_001`, `SB07_INV_002`, `SB07_INV_003`.

## Source Assertions

- Gate B coupling review: `bundle://proof/SB07/source-assertions/gate-b-coupling-review.md`.

## Anti-Stub Audit

- Anti-stub transcript: `bundle://proof/SB07/transcripts/anti-stub-audit.txt`.
