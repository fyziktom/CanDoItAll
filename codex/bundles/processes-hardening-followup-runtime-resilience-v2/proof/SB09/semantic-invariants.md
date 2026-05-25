# SB09 Semantic Invariants

## SB09-INV-001

- Invariant ID: `SB09-INV-001`
- Source raw note: N006, N007
- Expected behavior: lint runs in advisory and strict modes, strict mode blocks publish/run-start for missing operation contracts or recovery policy, and editor surfaces actionable warnings/errors.
- Disallowed shallow implementation: tests that only call a helper without integrating publish/start paths, UI text without model data, or a linter that warns on every generic artifact creation.
- Failing-first test: `bundle://proof/SB09/transcripts/failing-first.txt`
- Passing test: `bundle://proof/SB09/transcripts/passing.txt`
- Changed source files and hashes: `bundle://proof/SB09/transcripts/changed-file-hashes.txt`
- Production assertions: `bundle://proof/SB09/transcripts/source-assertions.txt`
- Red-team negative case: business, legal, manufacturing, and research definitions avoid software-only lint assumptions.
- Downstream dependency check: SB10 uses linter red-team tests as final generic closure proof.

## Production Behavior Artifact Matrix

| Artifact/signal | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `ProcessDefinitionLintResult` and strict lint gate | `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs` | `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.Publication.cs` and `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs` | `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.cs` and `repo://src/CanDoItAll.Modules.Processes/Components/ProcessDefinitionForm.razor` | `bundle://proof/SB09/transcripts/failing-first.txt` |
