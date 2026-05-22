# Normalized Requirements

| Requirement id | Requirement | Acceptance criteria | Source notes |
| --- | --- | --- | --- |
| `R001` | Add a strongly typed persisted global Cognitive Memory usage flag. | `CognitiveMemoryAutomationSettings` and update/record/API request types expose a boolean setting with default enabled. | `N003`, `N004` |
| `R002` | Expose runtime enable/disable through existing settings UI and API. | The settings tab has a "Use Cognitive Memory" enabled/disabled control and save round-trips the value. API PUT accepts the value. | `N003`, `N004` |
| `R003` | Agent context contribution must skip when disabled. | With the flag off, `CognitiveMemoryAgentContextContributor` returns `Skipped` without project scope and without calling recall. | `N001`, `N002`, `N005`, `N006` |
| `R004` | Workflow Cognitive Memory executors must skip when disabled. | Recall/probe/learning proposal workflow executors return deterministic skipped payloads before project id validation or downstream service calls. | `N005`, `N006` |
| `R005` | Scheduled automation must skip when disabled. | Runner returns `Executed = false` and a disabled warning before source ingestion, consolidation, or professor-anchor scans. | `N005`, `N006` |
| `R006` | Enabled behavior must remain strict. | Existing failure behavior for missing project scope or unavailable memory remains when the flag is enabled. | `N002`, `N006` |
| `R007` | Database schema must support the setting. | PostgreSQL and SQLite migrations add the setting column with safe default enabled. | `N003`, `N007` |
| `R008` | Development PostgreSQL must be clean after implementation. | `candoitall_development` is recreated or cleaned, migrations are applied, and the app can use it. | `N007` |

## Scope Exceptions

- Direct Cognitive Memory status/settings/database-management endpoints remain available while disabled so the runtime flag can be changed back.
- This bundle does not remove Cognitive Memory DI registrations because runtime enable/disable cannot be implemented by startup-only registration changes.
