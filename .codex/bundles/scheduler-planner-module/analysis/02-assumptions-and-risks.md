# Assumptions And Risks

## Assumptions

- "Workflow" means AgentFramework workflow/execution definitions surfaced through `CanDoItAll.AgentFramework.Core`, not process-internal workflow steps only.
- "Process" means a top-level `ProcessesService.StartRunAsync` run from a saved process definition/template.
- Scheduler/Planner should be an operator-facing module, not only an internal Automation diagnostics page.
- Existing Automation runtime remains the Quartz integration layer. SchedulerPlanner should depend on Automation, not the other way around.
- Runtime database profiles must support SQLite and PostgreSQL unless implementation analysis proves one is intentionally unsupported.
- CanvasLib calendar is suitable for visualizing planned/actual scheduled run windows, but not for replacing the schedule setup form or history search grid.

## Critical Path Risks

- Quartz persistence is provider-specific. The implementation must choose and migrate the correct Quartz ADO.NET table scripts/delegates for supported profiles instead of assuming a single SQL dialect.
- Adding workflow/process adapters directly to `CanDoItAll.Modules.Automation` would invert dependencies and make the generic Automation runtime aware of product modules. The bundle avoids this by introducing a thin orchestration module.
- CRON description packages usually describe five-field UNIX CRON by default, while Quartz CRON commonly has seconds and optional year. The description adapter must be tested against Quartz-style expressions.
- A scheduled fire can succeed in Quartz but fail before a process/workflow run starts. Fire history needs explicit statuses so failures are searchable and not hidden behind retry mechanisms.
- Restart recovery has two layers: Quartz job-store recovery and application-level durable message dispatch. Both must be proven.
- Schedule creation can expose sensitive payload data if the target metadata is dumped into logs or history. Logs and stored history must use typed identifiers and masked payload summaries.

## Validation Risks

- The page has dense operational information. A generic card-heavy dashboard would make the workflow slower. The preferred layout is a table-first console with a detail drawer and compact setup form.
- The UI must not bypass the component library. If existing wrappers are missing for a needed control, the implementation should improve BaseLib or use the closest existing wrapper before adding raw ad hoc markup.
- CanvasCalendar is canvas/JS-backed and needs real browser proof. Component tests alone cannot prove that the calendar is nonblank and correctly framed.
- Integration tests can pass while Quartz still uses in-memory job storage if tests only verify `Automation_Triggers` rehydration. Restart proof must inspect or exercise Quartz persistent store behavior.
- Browser screenshots can look acceptable at one viewport while tabs or table controls overlap at narrower widths. UI closure requires both wide and narrower passes.

## Mitigations

- Require a Quartz persistent-store proof before launch adapters are considered complete.
- Store scheduler domain entities separately from `Automation_Triggers`, but keep `Automation_Triggers` as the trigger projection used by Quartz.
- Use typed schedule target records and constants for Automation owner keys/trigger keys.
- Add integration tests for schedule create/update/delete, restart recovery, fire dispatch, dedupe, and history.
- Add component and Playwright validation for all three tabs at wide and narrow widths.
- Build a SchedulerPlanner-to-`CanvasCalendarSurface` mapper and validate it separately so calendar rendering does not duplicate scheduling rules inside Razor markup.

## Reopen Triggers

- Any implementation that keeps Quartz on `RAMJobStore` or only proves application-level rehydration.
- Any schedule target stored only as string/JSON payload without typed target kind and id fields.
- Any CRON description implementation that cannot handle the Quartz-style expressions accepted by schedule validation.
- Any UI implementation that omits one of the three required tabs.
- Any UI implementation that skips CanvasLib calendar for scheduled-run preview without a documented blocker.
- Any target launch path that can start duplicate process/workflow runs for the same scheduled fire dedupe key.
