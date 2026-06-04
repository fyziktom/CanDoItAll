# SB09 Semantic Invariants

## Invariant SB09_INV_001

- Invariant ID: `SB09_INV_001`
- Source raw note: "Add tests proving runtime provider metadata is preserved in receipts where applicable."
- Expected behavior: Provider-native browser receipts projected from execution state preserve runtime tool provider key/name in both run detail and persisted receipt listing.
- Disallowed shallow implementation: Checking only that browser receipt names exist while losing `RuntimeToolProviderKey` and `RuntimeToolProviderName`.
- Failing-first test: `bundle://proof/SB09/transcripts/receipt-provider-metadata-expansion-absent.failing-first.txt` shows the expanded provider-metadata receipt assertions were absent in `HEAD`.
- Passing test: `bundle://proof/SB09/transcripts/receipt-required-tool-lineage-integration-tests.txt`.
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests.cs`.
- Production assertions: `bundle://proof/SB09/source-assertions/receipt-required-tool-lineage.txt`.
- Red-team negative case: A projected `browser_*` receipt with empty or mismatched runtime provider metadata fails `GetExecutionRunDetailAsync_projects_successful_playwright_browser_calls_into_tool_receipts`.
- Downstream dependency check: SB10 can review boundary consistency with receipt ownership metadata protected by targeted integration tests.

## Invariant SB09_INV_002

- Invariant ID: `SB09_INV_002`
- Source raw note: "Add tests proving required tool detection still sees workspace/browser/project_structure/image_generation tools."
- Expected behavior: Required-tool detection still identifies workspace file, browser evidence, project-structure, and image-generation tool families around the execution boundary.
- Disallowed shallow implementation: Testing one tool family while browser proof, project-structure writeback, or image-generation requirements silently stop being recognized.
- Failing-first test: `bundle://proof/SB09/transcripts/required-tool-family-test-absent.failing-first.txt` shows the consolidated four-family guard was absent in `HEAD`.
- Passing test: `bundle://proof/SB09/transcripts/receipt-required-tool-lineage-integration-tests.txt`.
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`.
- Production assertions: `bundle://proof/SB09/source-assertions/receipt-required-tool-lineage.txt`.
- Red-team negative case: Removing any hard-required family token from `ResolveRequiredToolNames` fails `ResolveRequiredToolNames_preserves_boundary_tool_families_for_execution_lineage`.
- Downstream dependency check: SB10 can rely on required-tool behavior while checking the boundary for hidden coupling or policy drift.

## Invariant SB09_INV_003

- Invariant ID: `SB09_INV_003`
- Source raw note: "Run artifact lineage smoke tests" and "Do not create driver packs."
- Expected behavior: Existing artifact-lineage projection and validation continue to protect typed lineage, workflow output disambiguation, compact recovery keys, and current-run artifact identity.
- Disallowed shallow implementation: Treating receipt and required-tool tests as enough while artifact projection can bind stale or ambiguous outputs.
- Failing-first test: N/A - SB09 added regression coverage and ran existing artifact-lineage smoke tests; production lineage code was unchanged.
- Passing test: `bundle://proof/SB09/transcripts/artifact-lineage-smoke-tests.txt`; `bundle://proof/SB09/transcripts/artifact-lineage-source-scan.txt`.
- Changed source files: No production artifact-lineage files changed in SB09.
- Production assertions: `bundle://proof/SB09/source-assertions/receipt-required-tool-lineage.txt`.
- Red-team negative case: Removing typed projection lineage, compact recovery keys, or explicit workflow output id mapping fails the SB09/SB07/SB02 lineage smoke tests.
- Downstream dependency check: SB10 can proceed because receipt projection, required-tool detection, and artifact lineage have explicit parity proof after the execution-boundary contract work.
