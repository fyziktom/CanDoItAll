# Architecture Cutline

## Goal

Transform the route handler pipeline from a nested dispatcher implementation into a top-level module-local boundary.

## Current shape

```text
ProcessRunAutomationDispatchService
  RouteHandlers.cs
    private IProcessClaimedDispatchRouteHandler
    private ProcessClaimedDispatchRouteContext
    private FreshRecoverySkipRouteHandler
    private DatabaseRequirementRouteHandler(ProcessRunAutomationDispatchService dispatcher)
    ...
```

## Target shape for this bundle

```text
ProcessDispatchRouteContext
ProcessDispatchRouteResult
IProcessDispatchRouteHandler
ProcessDispatchRouteHandlerPipeline
ProcessDispatchRouteHandlerFactory

Route facets:
  IProcessDispatchRouteLog
  IProcessDispatchRouteClock
  IProcessDispatchTransitionPort
  IProcessDispatchCandidateReloadPort
  IProcessDispatchPreExecutionPort
  IProcessDispatchRecoveryPort
  IProcessDispatchSubprocessPort
  IProcessDispatchWorkflowPort
  IProcessDispatchDirectAgentPort
  IProcessDispatchFinalizerPort
  IProcessDispatchGuardPort

Top-level handlers:
  FreshRecoverySkipRouteHandler
  DatabaseRequirementRouteHandler
  UpstreamMaterializationRouteHandler
  StrandedArtifactRecoveryRouteHandler
  SubprocessRouteHandler
  StartTransitionRouteHandler
  WorkflowRouteHandler
  DirectAgentExecutionRouteHandler
  CompetingExecutionGuardRouteHandler
  RunClosedGuardRouteHandler
  FinalizerTransitionRouteHandler

Dispatcher adapter:
  ProcessDispatchRouteHost or ProcessDispatchRouteServices
    - can remain module-local
    - may forward to dispatcher as a transitional adapter
    - but handlers must not receive dispatcher directly
```

## Important non-goals

- Do not create `CanDoItAll.Processes.Core`.
- Do not introduce `IProcessDriverPack`.
- Do not create driver registry.
- Do not change behavior.
- Do not touch UI.
- Do not move EF entities.
- Do not remove process templates, workflows, artifact projection, recovery, subprocess, workflow or direct-agent paths.

## Driver readiness

This is still not a production driver bundle. The driver preparation is vocabulary-only:
- route stage intent,
- route side-effect class,
- required evidence family,
- future driver hook notes.

The only acceptable output is documentation and test metadata, not production driver interfaces.
