# Current Architecture Snapshot

## Current Core ownership
`CanDoItAll.Processes.Core` currently owns:
- Routing:
  - `ProcessDispatchRouteStage`
  - `ProcessDispatchRoutePipeline`
  - `ProcessDispatchRouteOrderAssertion`
  - `ProcessDispatchRouteSnapshot`
  - `ProcessDispatchTriggerFacts`
  - `ProcessDispatchRouteEligibility`
  - `ProcessDispatchRoutePlanner`
- Subprocess:
  - `ProcessSubprocessLifecycleRules`
  - `ProcessSubprocessRunFacts`
  - `ProcessSubprocessParentTransitionFacts`
- Artifacts:
  - artifact kind/trust/sensitivity enums
  - `ProcessArtifactExpectationSnapshot`
  - `ProcessArtifactRecordSnapshot`
  - `ProcessArtifactValidationSnapshot`
  - `ProcessSubprocessArtifactSourceResolver`
  - `ProcessArtifactExpectationMatcher`
  - recorded satisfaction rules and diagnostics/descriptors added by the previous stabilization bundle.

## Current module ownership
The Processes module still owns:
- EF/database read/write paths.
- Candidate hydration and technical-agent binding.
- Claim/lease/heartbeat.
- Transition execution.
- Direct AgentFramework execution, retry, provider repair and no-progress journals.
- Finalizer application and process state mutation.
- Projection persistence and workspace/storage/file IO.
- Validation orchestration.
- Process manager/runtime dispatch.

## Direction
The next bundle should expand Core with execution/finalizer **evidence descriptors** only. These descriptors must be immutable, deterministic and independent from the actual AgentFramework execution runtime. Production driver contracts remain blocked until permission/audit/sandbox design has executable negative tests.
