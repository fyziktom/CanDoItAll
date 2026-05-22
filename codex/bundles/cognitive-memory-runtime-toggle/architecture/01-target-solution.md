# Target Solution

## Design

Add `IsEnabled` to `CognitiveMemoryAutomationSettings`, `CognitiveMemoryAutomationSettingsUpdate`, and `CognitiveMemoryAutomationSettingsRecord`. The default is `true` to preserve current behavior after migrations.

Use a small shared constants/helper surface for disabled metadata and messages so skip results are consistent and not stringly typed at call sites.

Gate optional integrations at their entry points:

- `CognitiveMemoryAgentContextContributor.ContributeAsync` checks `settings.IsEnabled` immediately after loading settings and returns `Skipped`.
- `CognitiveMemoryRecallWorkflowExecutor`, `CognitiveMemoryProbeWorkflowExecutor`, and `CognitiveMemoryLearningProposalWorkflowExecutor` load settings first and return skipped JSON payloads when disabled.
- `CognitiveMemoryScheduledAutomationRunner.RunAsync` checks settings before validating actor/take/project-specific inputs or calling ingestion/consolidation.

Expose the setting through existing Cognitive Memory settings UI and API:

- API DTO gets `IsEnabled`.
- API update builder passes the value into the settings update.
- Settings page loads/saves a backing `isEnabled` field.
- Settings tab renders an enabled/disabled control using existing component wrappers and CSS.

## Boundaries

- Do not turn disabled mode into a catch-all fallback. Enabled mode must still fail predictably for missing project scope and unavailable required memory.
- Do not unregister services at startup.
- Do not introduce a new configuration system for one setting.
- Do not change unrelated Cognitive Memory algorithms or data models.

## Data Shape

`CognitiveMemory_AutomationSettings` gains a non-null boolean column:

```text
IsEnabled true
```

Existing rows default to enabled during migration.

## Production Behavior Artifact Matrix

| Signal/state | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| `CognitiveMemoryAutomationSettings.IsEnabled` | Settings API/UI and settings service | Agent context contributor, workflow executors, scheduled automation runner | Loaded on every call, so runtime changes take effect without restart | Disabled tests verify no recall/automation downstream call occurs. |
