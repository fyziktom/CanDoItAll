# Entry Branch Audit

## Branch

- Current branch: `maf-processes-refactor`.
- Worktree state before implementation: clean except bundle readiness repairs made for this execution.
- Previous bundle proof root exists: `repo://codex/bundles/process-dispatch-artifact-validation-residual-boundary-v1/proof`.

## Baseline Line Counts

| File | Lines |
| --- | ---: |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs` | 662 |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs` | 1251 |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | 1146 |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs` | 1874 |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs` | 1700 |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | 1276 |

## Known Test Limits

- Focused process dispatcher tests are preferred for this bundle.
- Broad historical architecture tests may include unrelated proof fixtures; if they fail outside the edited surface, the failing class and method must be recorded in the relevant subbundle transcript.

## Closure

- SB01 creates no production behavior change.
- Downstream subbundles must compare movement against these line-count baselines and update the final line-count proof in SB40/SB44.
