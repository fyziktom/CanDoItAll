# SB07 Semantic Invariants

- Invariant ID: SB07-INV-TEMPLATE-ARTIFACT-CONTRACTS
- Source raw note: Bundle required template and artifact audit beyond the observed process example.
- Expected behavior: Strict scan rejects file-only artifact acceptance and the shipped pack has no artifact contract diagnostics.
- Disallowed shallow implementation: Auditing only Tetris, Calculator, or business-plan artifacts.
- Failing-first test: `Template_compatibility_strict_scan_rejects_file_only_artifact_acceptance_contract` in `bundle://proof/SB07/transcripts/passing.txt`.
- Passing test: `ProcessTemplateCompatibilityHistoryTests` in `bundle://proof/SB07/transcripts/passing.txt`.
- Changed source files: `repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplateCompatibilityScanner.Artifacts.cs`, `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessTemplateCompatibilityHistoryTests.cs`.
- Production assertions: All 20 shipped artifact JSON templates have semantic acceptance contracts and reject file-only acceptance.
- Red-team negative case: A temp artifact with `fileExistenceIsSufficient: true` is rejected.
- Downstream dependency check: CodeAnalytics snapshot `snap-20260709182007-390484e5` returned `cycles: []`.
