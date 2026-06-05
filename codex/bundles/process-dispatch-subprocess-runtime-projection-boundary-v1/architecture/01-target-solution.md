# Target Solution

The target is a module-local subprocess boundary:

```text
DispatchAsync
  -> route says Subprocess
  -> ProcessSubprocessDispatchCoordinator / partial
      -> start transition if needed
      -> observe or create child subprocess run
      -> inspect capability gaps
      -> if non-terminal: observe only
      -> if completed: project child artifacts into parent
      -> finalize parent step
      -> if failed/blocked/cancelled: mirror parent status
```

Supporting helpers:

```text
ProcessSubprocessStatusRules
ProcessSubprocessStartTransitionBuilder
ProcessSubprocessCapabilityGapInspector
ProcessSubprocessArtifactProjectionPlanner
ProcessSubprocessArtifactProjectionWriter
ProcessSubprocessProjectionGapJournalCoordinator
ProcessSubprocessDispatchCoordinator
```

All helpers remain internal and module-local.
