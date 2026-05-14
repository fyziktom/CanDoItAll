# SB02 Plugin Observability And Logs Tab

## Status

- `Ready`

## Objective

Add durable plugin installation/runtime logging and expose it in a dedicated plugins page subtab with installation logs sorted separately from runtime logs.

## Success Criteria

- Plugin install/package lifecycle operations write durable log records.
- Plugin executor/runtime usage writes durable log records through existing observer/event surfaces.
- The plugins page has a log subtab that separates installation logs and runtime logs.
- Logs are sorted newest-first by default and filter by selected plugin/package where practical.
- Sensitive data is redacted consistently.

## Covered Inputs

- PRH-004 Durable Installation Logs
- PRH-005 Durable Runtime Logs
- PRH-006 Plugins Page Logs Subtab
- FIND-003, FIND-004

## Prerequisites

- SB01 progression gate passed.
- Package identity/source ownership is stable.
- Read `analysis/01-current-state.md`, especially `Observability Gap`.
- Read the `Plugin Logs` rows in `inventories/plugin-runtime-architecture-hardening-checklist.xlsx`.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowExecutorContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowExecutorObservability.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Services\AgentFrameworkModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Hosting\AgentFrameworkServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Plugins.Abstractions\PluginExecutionContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Persistence\PluginInstallationRecord.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Persistence\PluginRuntimeRecords.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Persistence\PluginSchemaInitializer.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Pages\PluginsPage.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\PluginsPageTests.cs`

## Deliverables

- Durable plugin log persistence model and schema initialization/migration hook consistent with current persistence patterns.
- Plugin log write/query service with typed stream/severity/operation enums.
- Installation log writers for package upload/validation/install/enable/disable/restart-required/activation outcomes.
- Runtime log writer bridge from workflow executor audit and plugin execution events.
- Plugins page log subtab with installation/runtime separation.
- Tests for persistence, redaction, sorting, filtering, and UI display.

## Dependency Impact

- SB05 may optimize log queries after the shape is stable.
- SB06 must rely on these logs to diagnose Docker ZIP install and activation failures.

## Validation Depth

- `Critical UI and observability foundation`

## Implementation Steps

1. Design the minimal durable log model with typed stream, operation, severity, status, ids, message, redacted details, and timestamp.
2. Add persistence configuration using existing plugin module database conventions.
3. Add a central redaction helper or reuse existing workflow audit redaction patterns.
4. Write logs in package lifecycle paths, including validation failures and restart-required outcomes.
5. Replace null workflow observer registration with a durable plugin-aware observer or bridge that records plugin executor events only.
6. Implement `IPluginExecutionEvents` or bridge it into the same durable log service.
7. Add query service methods for installation/runtime logs with paging/sorting/filtering.
8. Add the plugins page log subtab using existing page/component patterns.
9. Add tests for persistence and UI behavior.
10. Capture browser proof and update execution report.

## Scope Exceptions

- Do not build a full centralized application logging system.
- Do not log full plugin settings, OAuth tokens, command arguments, or package file contents.
- Do not make logs a prerequisite for executing plugins; logging failure should be explicit and observable, not silently swallowed.

## Do Not Do

- Do not store secrets in `DetailsJson`.
- Do not show runtime logs mixed with installation logs without a clear stream separation.
- Do not add plugin-specific branches for Docker/Gmail/Office365 in generic log UI.

## Acceptance Checklist

- [ ] Installation logs persist for success and failure paths.
- [ ] Runtime logs persist for plugin executor start/success/failure.
- [ ] Logs expose plugin id/package id/workflow executor id when available.
- [ ] Logs are sorted newest-first and filter by selected plugin/package.
- [ ] Plugins page contains a dedicated logs subtab.
- [ ] Tests prove redaction for settings/OAuth-sensitive fields.
- [ ] Browser proof shows installation and runtime streams separately.

## Proof Required

- Unit/integration tests for log write/query/redaction.
- Component test for plugins page logs subtab.
- Browser screenshots of the logs subtab with both streams populated or seeded.
- Execution report with command summaries and screenshot paths.

## Browser Validation Logging

- Target route: `/plugins`.
- Required viewport passes: maximized desktop and one narrower width affected by tabs/tables.
- Required actions: select a plugin, open logs subtab, switch installation/runtime log views, verify ordering and filtered rows.
- Screenshot evidence: `artifacts/sb02-plugin-logs-installation.png`, `artifacts/sb02-plugin-logs-runtime.png`, `artifacts/sb02-plugin-logs-narrow.png`.
- Review questions: Are install/runtime logs clearly distinct? Are messages actionable? Is any sensitive value visible?

## Progression Gate

- SB06 may not close until Docker package install/activation events are visible through this durable logging path or the execution report explicitly justifies why Docker proof used a later equivalent path.

## Suggested Agent Prompt

```text
Implement SB02 only from C:\repositories\CanDoItAll\codex\bundles\plugin-runtime-architecture-hardening-followup.
Add durable plugin installation/runtime logs and the plugins page log subtab. Preserve generic runtime boundaries and centralize redaction. Capture tests, browser screenshots, and update reviews/01-execution-report.md.
```
