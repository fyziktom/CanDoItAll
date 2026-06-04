# SB09 Proof Manifest

- Status: Completed.
- Owned requirements: RQ-010, RQ-013.
- Semantic invariant contract: `bundle://proof/SB09/semantic-invariants.md`.
- Browser proof: N/A because SB09 changed no rendered UI route.

## Changed-File Hashes

| Path | SHA-256 |
| --- | --- |
| `repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests.cs` | `4DA87A03DC221F231282A8DDC8B12A496691B1DC273556261DC6EFA4CD72C24C` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | `97C8C0248118035694EBDBD1087555022DD5825789AC7CB9E0EFE692B22C82B4` |
| `bundle://proof/SB09/source-assertions/receipt-required-tool-lineage.txt` | `AB0F0573E4BBB4B5DF8AB94E06300A9B7042961B5D6061FF344A587AB2005DD4` |
| `bundle://proof/SB09/semantic-invariants.md` | `9C0AC1ABBE59CB7B90EBE02D2CB03ADFA78E33E318DBEE32DB52300D9A5AE4B5` |
| `bundle://subbundles/09-09-receipt-required-tool-and-artifact-lineage-hardening/README.md` | `C8530BAF36F5E0B0882E08C27E2CC616A9039739AC9BBF8CDBCCEF56F2ED4C46` |
| `bundle://reviews/01-execution-report.md` | `C874F1D35CA48A610527563AD8BFD5533EB914C3B43F09F34F4507E8895876E6` |

## Command Transcripts

- Receipt provider metadata failing-first: `bundle://proof/SB09/transcripts/receipt-provider-metadata-expansion-absent.failing-first.txt`.
- Required-tool family failing-first: `bundle://proof/SB09/transcripts/required-tool-family-test-absent.failing-first.txt`.
- Receipt and required-tool integration tests: `bundle://proof/SB09/transcripts/receipt-required-tool-lineage-integration-tests.txt`.
- Artifact-lineage smoke tests: `bundle://proof/SB09/transcripts/artifact-lineage-smoke-tests.txt`.
- Receipt provider metadata source scan: `bundle://proof/SB09/transcripts/receipt-provider-metadata-source-scan.txt`.
- Required-tool family source scan: `bundle://proof/SB09/transcripts/required-tool-family-source-scan.txt`.
- Artifact-lineage source scan: `bundle://proof/SB09/transcripts/artifact-lineage-source-scan.txt`.
- MAF product dependency scan: `bundle://proof/SB09/transcripts/maf-product-dependency-scan.txt`.
- No Process Core/driver project scan: `bundle://proof/SB09/transcripts/no-core-driver-project-scan.txt`.
- Hash capture: `bundle://proof/SB09/transcripts/hashes.txt`.

## Failing-First And Passing Proof

- Failing-first: `bundle://proof/SB09/transcripts/receipt-provider-metadata-expansion-absent.failing-first.txt`.
- Failing-first: `bundle://proof/SB09/transcripts/required-tool-family-test-absent.failing-first.txt`.
- Passing transcript: `bundle://proof/SB09/transcripts/receipt-required-tool-lineage-integration-tests.txt`.
- Passing transcript: `bundle://proof/SB09/transcripts/artifact-lineage-smoke-tests.txt`.
- Test name: `GetExecutionRunDetailAsync_projects_successful_playwright_browser_calls_into_tool_receipts`.
- Test name: `ResolveRequiredToolNames_preserves_boundary_tool_families_for_execution_lineage`.
- Test name: `WorkflowArtifactProjectionMapping_SB09_INV_001_uses_explicit_output_id_when_same_kind_names_conflict`.
- Test name: `ArtifactContractValidation_SB09_INV_001_accepts_current_run_org_scoped_path_with_matching_typed_lineage`.
- Invariant labels: `SB09_INV_001`, `SB09_INV_002`, `SB09_INV_003`.

## Source Assertions

- Receipt, required-tool, and artifact-lineage hardening: `bundle://proof/SB09/source-assertions/receipt-required-tool-lineage.txt`.

## Anti-Stub Audit

- Anti-stub transcript: `bundle://proof/SB09/transcripts/anti-stub-audit.txt`.
