# SB06 Semantic Invariants

## SB06-INV-001

- Invariant ID: `SB06-INV-001`
- Source raw note: N005, N007
- Expected behavior: review/approval disposition branches can route valid negative outcomes, but missing own required artifacts on non-disposition steps cannot be hidden by a branch.
- Disallowed shallow implementation: any validation failure selects the first negative branch, or branch routing ignores whether the current failure is missing input versus current-step output.
- Failing-first test: `bundle://proof/SB06/transcripts/failing-first.txt`
- Passing test: `bundle://proof/SB06/transcripts/passing.txt`
- Changed source files and hashes: `bundle://proof/SB06/transcripts/changed-file-hashes.txt`
- Production assertions: `bundle://proof/SB06/transcripts/source-assertions.txt`
- Red-team negative case: missing upstream input remains blocked instead of routed to repair.
- Downstream dependency check: SB09 lint warns definitions that mix artifact production with negative disposition branches without recovery policy.

## Production Behavior Artifact Matrix

| Artifact/signal | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Artifact disposition routing decision | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | `bundle://proof/SB06/transcripts/source-assertions.txt` | `bundle://proof/SB06/transcripts/failing-first.txt` |
