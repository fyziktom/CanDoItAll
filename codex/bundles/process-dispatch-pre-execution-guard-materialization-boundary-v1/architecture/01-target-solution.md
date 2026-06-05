# Target Solution

## Current dispatch flow after this bundle

```text
Dispatch.cs
  -> header selector
  -> guard/claim/heartbeat
  -> hydration loader
  -> candidate assembly/factory
  -> pre-execution guard handler
       -> database requirement blocker
       -> upstream materialization coordinator
  -> subprocess/workflow/agent execution route
  -> finalizer
```

## New module-local helpers

Expected helpers:

- `ProcessDispatchDatabaseRequirementBlocker`
- `ProcessDispatchDatabaseRequirementDecision`
- `ProcessUpstreamArtifactGapFacts`
- `ProcessUpstreamArtifactMaterializationPlanner`
- `ProcessUpstreamArtifactMaterializationFingerprint`
- `ProcessUpstreamArtifactMaterializationJournalCoordinator`
- `ProcessUpstreamArtifactMaterializationRerunRequestBuilder`
- `ProcessDispatchPreExecutionGuardHandler`
- `ProcessDispatchPreExecutionGuardOutcome`

These names can be adjusted if the current code shape suggests better names, but the responsibilities must remain separated.

## Purity split

Pure / side-effect-free:

- gap fact extraction
- materialization target selection
- fingerprint creation
- block reason construction
- rerun directive construction
- transition request construction

Explicit side-effect coordinators:

- transition downstream step to Blocked
- record journal event
- call `ProcessesService.RerunAgentStepAsync`

Dispatch orchestration:

- owns route order
- owns logging
- owns returns/continues
- owns cancellation and claim passing
