# SB01 Semantic Invariants

## Status

Completed.

## Invariants

- Invariant ID: `SB01-INV-001`
- Source raw note: N001, N004, N005, and N006 require a process-owned finalization path for every executor kind.
- Expected behavior: Direct AgentFramework completion, workflow-backed role completion, and manager artifact recovery completion all enter `FinalizeStepCompletionAsync` before any process step transition is attempted.
- Disallowed shallow implementation: Only adding a helper for direct-agent execution while leaving `HandleWorkflowExecutionOutcomeAsync` as a transition-only bypass.
- Failing-first test: `DispatchSource_routes_direct_and_workflow_completion_through_process_owned_finalizer` with pre-change source assertion in `bundle://proof/SB01/transcripts/failing-first.txt`.
- Passing test: `DispatchSource_routes_direct_and_workflow_completion_through_process_owned_finalizer` in `bundle://proof/SB01/transcripts/passing.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`, and `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`.
- Production assertions: `FinalizeStepCompletionAsync`, `ProcessStepCompletionExecutorKind.DirectAgent`, `ProcessStepCompletionExecutorKind.WorkflowBackedRole`, and `ProcessStepCompletionExecutorKind.ManagerArtifactRecovery` are asserted by `bundle://proof/SB01/transcripts/source-assertions.txt`.
- Red-team negative case: The pre-change workflow path could transition on `workflowOutcome.CompletionStatus`; the passing source test rejects `TargetStatus = workflowOutcome.CompletionStatus`.
- Downstream dependency check: SB02-SB06 now consume a single finalizer boundary instead of trusting executor-local completion state.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| ProcessStepCompletionFinalizerResult | `FinalizeStepCompletionAsync` in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | `ApplyFinalizedStepTransitionAsync` in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Created once per completion attempt after artifact validation; lifecycle source proof is in `bundle://proof/SB01/transcripts/source-assertions.txt` | `bundle://proof/SB01/transcripts/failing-first.txt` shows the missing pre-change route |
| ProcessStepCompletionExecutorKind | Dispatch call sites in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | Validation diagnostics and failure fingerprints in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Carried through validation and diagnostics before transition; verified by `bundle://proof/SB01/transcripts/passing.txt` | The passing test rejects a workflow-only transition shortcut |

## Red-Team Negative Cases

- Workflow-backed role completion cannot bypass the finalizer and transition directly.
- Stranded manager recovery cannot transition without finalizer validation.
