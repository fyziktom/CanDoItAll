# SB29 Process tabs and live runtime UI proof

Date: 2026-06-16

## Scope

This proof covers the repair that makes the Process workspace tabs and Live Processes page data-backed again while preserving the original Process UX shape from `maf-processes-refactor`:

- definition tree/list remains the left navigation surface,
- workspace tabs remain Definition, Roles, Steps, Runs, Graphs, Analytics, Exchange, and Manager chat,
- Runs restores nested Launch, Activity, Control, Execution, Graphs, Coordination, and Evidence sections,
- Manager chat uses the real agent chat workspace with run and manager-agent selection,
- Live Processes has its own main-menu item and supports runtime history windows, status/run filtering, paging controls, run details, charts, and tool usage.

## Validation

| Check | Result |
| --- | --- |
| `dotnet build CanDoItAll.slnx -v:minimal` | Passed, 0 warnings, 0 errors. |
| `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --no-build --filter ProcessWorkspaceShellTests --logger "trx;LogFileName=process-workspace-shell.trx"` | Passed, 26/26 tests. |
| `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-build --filter ProcessProjectionPipelineTests --logger "trx;LogFileName=process-projection-pipeline.trx"` | Passed, 6/6 tests. |
| CodeAnalytics MCP scoped snapshot | `snap-20260616203816-1073337c`, no diagnostics, no blocking errors. |

## Browser proof

Validated with Playwright MCP at `http://localhost:5032` using a 1600x1000 desktop viewport.

| Route | Observed proof |
| --- | --- |
| `/processes` | Main navigation contains `Processes` and `Live Processes`; left treeview lists process definitions by scope. |
| `/processes`, Runs tab | Runtime toolbar has history window, run selector, event paging, refresh; nested run tabs render; run cards and event ledger are populated from runtime projection data. |
| `/processes`, Graphs tab | Cost, tokens, duration, tool usage cards render with `CdaChart` charts for runtime activity, token/cost telemetry, and tool usage. |
| `/processes`, Analytics tab | Readiness, compatibility, runtime event count, projection backlog, event ledger, and tool usage ledger render from projection data. |
| `/processes`, Manager chat tab | Manager agent selector, run selector, availability badges, reload action, and `ChatWorkspacePanel` render; composer is seeded with selected-run context. |
| `/processes/live`, Activity tab | History window filters real events by time; 1-hour view can hide older selected-run events, 24-hour view shows them again. |
| `/processes/live`, Status filter | Selecting `NeedsAttention` filters run cards and run selector options to attention runs only. |
| `/processes/live`, Graphs tab | Selected-history chart and tool-usage chart render with the same history/status/run filters. |
| `/processes/live`, Tool analytics tab | Tool-usage ledger and event ledger render from runtime event grouping. |
| `/processes/live`, run details | Run detail dialog opens from live cards and shows status, recent-event count, incidents, and recent events. |

Screenshot proof:

- `bundle://proof/SB29-process-tabs-live-runtime-ui/processes-live-runtime-filter-proof.png`

## Notes

- The runtime/core/dispatch layers remain generic. The UI consumes application/projection contracts and does not query runtime EF entities directly.
- Attention summaries now prefer blocked, failed, denied, rejected, escalated, and incident events before ordinary dispatch events, so blocked cards surface actionable causes such as `StepBlocked`.
- Live status filtering now re-resolves the selected run and filters run selector options, avoiding stale selection when the active status filter excludes a previously selected run.
