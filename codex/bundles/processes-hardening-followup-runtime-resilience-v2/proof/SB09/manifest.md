# SB09 Proof Manifest

## Status

Completed.

## Owned Requirements And Raw Notes

- Requirements: RQ13
- Raw notes: N006, N007
- Semantic invariant contract: `bundle://proof/SB09/semantic-invariants.md`

## Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs` supports advisory and strict lint modes with suggestions and strict error elevation.
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.Publication.cs` and `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs` block strict publish/start when lint errors exist.
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessDefinitionForm.razor` renders lint warnings and errors in the editor.
- Transcript: `bundle://proof/SB09/transcripts/source-assertions.txt`

## Production Behavior Artifact Matrix

| Artifact/signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| `ProcessDefinitionLintResult` and strict lint gate | `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs` | `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.Publication.cs`, `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs`, and `repo://src/CanDoItAll.Modules.Processes/Components/ProcessDefinitionForm.razor` | Editor, publish, and run-start paths evaluate lint before execution | `bundle://proof/SB09/transcripts/failing-first.txt` covers strict missing contract/recovery errors and generic no false positive scenarios |

## Failing-First Or Red-Team Proof

Transcript: `bundle://proof/SB09/transcripts/failing-first.txt`

## Passing Proof

Transcript: `bundle://proof/SB09/transcripts/passing.txt`

## Anti-Stub Audit

Transcript: `bundle://proof/SB09/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

Transcript: `bundle://proof/SB09/transcripts/changed-file-hashes.txt`

Representative changed-file SHA-256: `4970834d5515a5ab11f2f487b0e2ed0aee474551ed936d5379b955ed6f7a0acf`

## Validation

Completed through focused linter integration tests, unit tests, build validation, and source assertions.

## Blockers

None.
