# SB09 Provider-Native Visual Rule Extraction Assertions

## Result

Passed.

## Assertions

- Added `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProviderNativeVisualValidationRules.cs` for provider-native browser path/tool classification and visual artifact scoring.
- Dispatcher wrappers remain for provider-native reflection-visible methods and file-probing orchestration remains in the dispatcher.
- Expected/discovered projection modes are unchanged; `ArtifactProjection.cs` delegates browser artifact path classification to the helper only.
- The helper does not own file, directory, storage, DbContext, record-write, dispatcher nested expectation, Core, or driver-pack dependencies.
- `ArtifactValidation.cs` line count decreased from 3720 after SB08 to 3603 after SB09.
- Focused architecture tests passed: 6 tests.
- Focused provider-native visual integration tests passed: 12 tests.

## Proof

- Passing architecture transcript: `bundle://proof/SB09/transcripts/focused-unit-architecture-tests.txt`
- Passing provider-native visual integration transcript: `bundle://proof/SB09/transcripts/focused-provider-native-visual-integration-tests.txt`
- Hashes: `bundle://proof/SB09/transcripts/changed-file-hashes.txt`
- Source scans and anti-stub audit: `bundle://proof/SB09/transcripts/provider-native-visual-rule-source-scans.txt`
- No-core/no-driver scan: `bundle://proof/SB09/transcripts/no-core-no-driver-scan.txt`
- Line count: `bundle://proof/SB09/transcripts/line-count.txt`
