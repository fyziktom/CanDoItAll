# SB07 Title And Text Rule Extraction Assertions

## Result

Passed.

## Assertions

- Added `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactTextMatchRules.cs` for pure title, slug, token, content-signal, visual-token, and narrative-purpose matching rules.
- Dispatcher wrappers remain for existing call sites, preserving matching order.
- Artifact title/content noise-token sets moved out of the dispatcher root into the text-match helper.
- The helper does not own file, directory, storage, DbContext, record-write, dispatcher nested expectation, Core, or driver-pack dependencies.
- `ArtifactValidation.cs` line count decreased from 3855 after SB06 to 3720 after SB07.
- Focused architecture tests passed: 5 tests.
- Focused title/text integration tests passed: 16 tests.

## Proof

- Failing-first compile transcript: `bundle://proof/SB07/transcripts/focused-unit-architecture-tests.txt`
- Passing architecture transcript: `bundle://proof/SB07/transcripts/focused-unit-architecture-tests-rerun.txt`
- Passing title/text integration transcript: `bundle://proof/SB07/transcripts/focused-title-text-integration-tests.txt`
- Hashes and source scans: `bundle://proof/SB07/transcripts/changed-file-hashes.txt`, `bundle://proof/SB07/transcripts/text-rule-source-scans.txt`
