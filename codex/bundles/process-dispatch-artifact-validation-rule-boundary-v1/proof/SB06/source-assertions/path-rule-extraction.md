# SB06 Path Rule Extraction Assertions

## Result

Passed.

## Assertions

- Added `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactPathValidationRules.cs` for pure path and managed-artifact rules.
- Extracted rule bodies for managed path reference normalization, shallow shared managed artifact path classification, expected artifact relative-path parsing, exact expected-path comparison, scoped managed path comparison, and managed root segment classification.
- Dispatcher wrappers remain for existing tests/callers such as `IsShallowSharedManagedArtifactPath`.
- File-system operations such as `File.Exists`, `Directory.CreateDirectory`, and `File.Copy` remain in dispatcher orchestration, not in the path rule helper.
- `ArtifactValidation.cs` line count decreased from 3931 at SB01/SB04 baseline to 3855 after SB06.
- Focused path and managed-artifact integration tests passed: 16 tests.
- Focused architecture tests passed: 4 tests.

## Proof

- Failing-first compile transcript: `bundle://proof/SB06/transcripts/focused-unit-architecture-tests.txt`
- Passing architecture transcript: `bundle://proof/SB06/transcripts/focused-unit-architecture-tests-rerun.txt`
- Passing path integration transcript: `bundle://proof/SB06/transcripts/focused-path-integration-tests.txt`
- Hashes and source scans: `bundle://proof/SB06/transcripts/changed-file-hashes.txt`, `bundle://proof/SB06/transcripts/path-rule-source-scans.txt`
