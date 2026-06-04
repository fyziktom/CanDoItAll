# SB11 Project-Structure Preservation Rule Extraction Assertions

## Result

Passed.

## Assertions

- Added `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectStructureRequirementValidationRules.cs` for project-structure requirement preservation, source-line filtering, weakening phrase detection, and requirement tokenization.
- Dispatcher now aggregates candidate contract text and delegates pure preservation checks to the helper.
- Project-structure noise tokens moved out of the dispatcher root.
- Mandatory vs optional source-line handling is preserved.
- The helper does not own file, directory, storage, DbContext, record-write, dispatcher nested expectation, Core, or driver-pack dependencies.
- `ArtifactValidation.cs` line count decreased from 3394 after SB10 to 3223 after SB11.
- Focused architecture tests passed: 8 tests.
- Focused project-structure preservation integration tests passed: 2 tests.

## Proof

- Passing architecture transcript: `bundle://proof/SB11/transcripts/focused-unit-architecture-tests.txt`
- Passing project-structure preservation integration transcript: `bundle://proof/SB11/transcripts/focused-project-structure-preservation-integration-tests.txt`
- Hashes: `bundle://proof/SB11/transcripts/changed-file-hashes.txt`
- Source scans and anti-stub audit: `bundle://proof/SB11/transcripts/project-structure-rule-source-scans.txt`
- No-core/no-driver scan: `bundle://proof/SB11/transcripts/no-core-no-driver-scan.txt`
- Line count: `bundle://proof/SB11/transcripts/line-count.txt`
