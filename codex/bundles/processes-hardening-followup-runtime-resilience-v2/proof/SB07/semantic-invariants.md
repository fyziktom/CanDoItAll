# SB07 Semantic Invariants

## SB07-INV-001

- Invariant ID: `SB07-INV-001`
- Source raw note: N005, N006, N007
- Expected behavior: artifact validation inspects actual storage-backed content when the expectation requires parseable content such as JSON.
- Disallowed shallow implementation: accepting a `.json` path by extension, validating only review summary text, or requiring physical files when managed storage can provide content.
- Failing-first test: `bundle://proof/SB07/transcripts/failing-first.txt`
- Passing test: `bundle://proof/SB07/transcripts/passing.txt`
- Changed source files and hashes: `bundle://proof/SB07/transcripts/changed-file-hashes.txt`
- Production assertions: `bundle://proof/SB07/transcripts/source-assertions.txt`
- Red-team negative case: malformed JSON content is rejected.
- Downstream dependency check: SB08 no-progress fingerprinting uses artifact validation status signals.

## Production Behavior Artifact Matrix

| Artifact/signal | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Storage-backed artifact format validation | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | `bundle://proof/SB07/transcripts/source-assertions.txt` | `bundle://proof/SB07/transcripts/failing-first.txt` |
