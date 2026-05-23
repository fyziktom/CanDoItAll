# SB02 Semantic Invariants

- Invariant ID: `INV-SB02-001`
- Source raw note: `N002`, `N003`
- Expected behavior: a downstream step with missing configured upstream artifact inputs blocks and asks the source step to materialize the missing artifact.
- Disallowed shallow implementation: re-executing the downstream step repeatedly.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first-current-behavior.txt`
- Passing test: `bundle://proof/SB02/transcripts/targeted-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Models.cs`
- Production assertions: dispatch checks artifact inputs before starting the downstream agent.
- Red-team negative case: non-agent or non-rerunnable source blocks visibly instead of fabricating artifacts.
- Downstream dependency check: source materialization depends on SB01 proof classification.

- Invariant ID: `INV-SB02-002`
- Source raw note: `N004`
- Expected behavior: a blocked downstream dependent waiting on upstream artifacts is reopened after the source step completes.
- Disallowed shallow implementation: leaving the downstream step blocked after the source producer completes.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first-current-behavior.txt`
- Passing test: `bundle://proof/SB02/transcripts/targeted-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeProgressionPlanner.cs`
- Production assertions: runtime progression checks explicit missing-upstream-artifact block reasons and satisfied dependencies.
- Red-team negative case: unrelated blocked steps are not reopened.
- Downstream dependency check: final dispatch test class passed after model shape change.

| Invariant ID | Source raw note | Expected behavior | Disallowed shallow implementation | Failing-first test | Passing test | Changed source files | Production assertions | Red-team negative case | Downstream dependency check |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `INV-SB02-001` | `N002`, `N003` | A downstream step with missing configured upstream artifact inputs blocks and asks the source step to materialize the missing artifact. | Re-executing the downstream step repeatedly. | `bundle://proof/SB02/transcripts/failing-first-current-behavior.txt` | `ShouldRetryIncompleteSuccessfulRun_does_not_retry_downstream_step_for_missing_upstream_artifact_block` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Models.cs` | Dispatch checks artifact inputs before starting the downstream agent. | Non-agent or non-rerunnable source blocks visibly instead of fabricating artifacts. | Source materialization depends on SB01 proof classification. |
| `INV-SB02-002` | `N004` | A blocked downstream dependent waiting on upstream artifacts is reopened after the source step completes. | Leaving the downstream step blocked after the source producer completes. | `bundle://proof/SB02/transcripts/failing-first-current-behavior.txt` | `ApplyTransitionConsequences_reactivates_blocked_dependent_after_upstream_artifact_materialization` | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeProgressionPlanner.cs` | Runtime progression checks explicit missing-upstream-artifact block reasons and satisfied dependencies. | Unrelated blocked steps are not reopened. | Final dispatch test class passed after model shape change. |

## Production Behavior Artifact Matrix

| Invariant | Producer | Consumer | Negative case |
| --- | --- | --- | --- |
| `INV-SB02-001` | Artifact input resolver | Dispatcher | Downstream same-step retries remain disabled for missing upstream input blocks |
| `INV-SB02-002` | Upstream step completion | Progression planner | Unrelated blocked steps are not reopened |
