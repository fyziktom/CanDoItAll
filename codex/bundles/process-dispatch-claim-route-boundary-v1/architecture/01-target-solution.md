# Target Solution

Create a module-local dispatch route boundary:

```text
ProcessRunAutomationDispatchService.Dispatch.cs
  remains the orchestrator

ProcessDispatchRouteFacts
  small snapshot of current candidate/trigger/run/step facts

ProcessAutomationExecutionSelectionRules
  pure rules for blocking/stale/recoverable/competing execution runs

ProcessDispatchClaimSession
  local wrapper around step guard, durable claim, heartbeat, lease renew, lost-claim handling

ProcessDispatchRoutePlanner
  returns route decisions without side effects

ProcessDispatchStartTransitionBuilder
  builds InProgress transition request

ProcessDispatchFinalizerContextFactory
  builds ProcessStepCompletionFinalizerContext for direct-agent, workflow, subprocess, and manager-recovery routes
```

The result should be easier to review and later extract, but still remains within `CanDoItAll.Modules.Processes`.
