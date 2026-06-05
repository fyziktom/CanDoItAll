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
- `ProcessMissingUpstreamArtifactMaterializationFacts`
- `ProcessMissingUpstreamArtifactMaterializationFactsResolver`
- `ProcessMissingUpstreamArtifactMaterializationBlocker`
- `ProcessMissingUpstreamArtifactMaterializationFingerprint`
- `ProcessMissingUpstreamArtifactMaterializationJournalCoordinator`
- `ProcessMissingUpstreamArtifactMaterializationCoordinator`
- `ProcessMissingUpstreamArtifactRerunRequestBuilder`
- `ProcessDispatchPreExecutionGuardHandler`
- `ProcessDispatchMissingUpstreamArtifactMaterializationPlan`

These names are module-local implementation vocabulary, not production driver APIs.

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
