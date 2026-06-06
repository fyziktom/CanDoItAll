# Target Solution

## Current transitional shape

```text
Projection coordinators
  -> small projection facets
  -> one nested ProcessArtifactProjectionServices implementation
  -> ProcessRunAutomationDispatchService wrappers/static helpers
```

## Target after this bundle

```text
Projection coordinators
  -> only required projection facets
  -> focused module-local facet implementations
  -> pure helper rules where possible
  -> dispatcher adapter only for unavoidable instance side effects
```

## Expected module-local facet implementation families

- `ProcessProjectionClaimGuard`
- `ProcessProjectionFileIo`
- `ProcessProjectionPathResolver`
- `ProcessProjectionArtifactClassifier`
- `ProcessProjectionExpectationMatcher`
- `ProcessProjectionProcessMockRules`
- `ProcessProjectionProjectStructureMatcher`
- `ProcessProjectionSessionObservationSource`
- `ProcessProjectionResponseTextRules`
- `ProcessProjectionBrowserOutputRules`
- `ProcessProjectionDecisionArtifactRules`
- `ProcessProjectionLineageFactory`
- `ProcessProjectionCandidateStateUpdater`

These names are illustrative. Codex may choose slightly different names, but the behavior and split boundaries must remain clear.

## Important distinction

This is not yet a driver pack. It is a module-local boundary that later driver packs may consume or replace after a future process-core split is safer.
