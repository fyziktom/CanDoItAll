# Route Handler Boundary

## Target shape

```text
DispatchAsync
  -> load candidate headers
  -> claim via ProcessDispatchClaimCoordinator
  -> RunClaimedDispatchAsync
       -> ProcessDispatchRouteHandlerPipeline
            -> FreshRecoverySkipHandler
            -> DatabaseRequirementHandler
            -> UpstreamMaterializationHandler
            -> StrandedArtifactRecoveryHandler
            -> SubprocessRouteHandler
            -> StartTransitionHandler
            -> WorkflowRouteHandler
            -> DirectAgentExecutionHandler
            -> CompetingExecutionGuardHandler
            -> RunClosedGuardHandler
            -> FinalizerTransitionHandler
```

The pipeline is internal and module-local. It must not be public, not Core, and not driver API.

## Handler result contract

A route handler should return a module-local result such as:

```text
NotHandled
HandledComplete
HandledContinueCandidates
UpdateCandidateAndContinue
```

The exact names are implementation details, but the behavior must stay equivalent.

## Side-effect ownership

Allowed side effects must be visible by type name:

- `...TransitionHandler`
- `...Coordinator`
- `...Store`
- `...FinalizerHandler`
- `...ExecutionHandler`

Do not hide side effects in `Rules` classes.
