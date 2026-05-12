# Normalized Requirements

| ID | Requirement | Priority | Validation |
| --- | --- | --- | --- |
| SPM-R001 | Add a Scheduler/Planner module for planning automatic workflow and process runs. | Must | Module registered, route reachable, services isolated from generic Automation runtime. |
| SPM-R002 | Reuse existing Quartz triggering through the Automation runtime; do not add a second scheduler loop. | Must | Trigger records project to Quartz and fires publish existing durable Automation messages. |
| SPM-R003 | Configure Quartz with DB-backed persistent store/recovery for supported runtime database profiles. | Must | Restart/recovery integration proof verifies persisted Quartz jobs/triggers survive process restart. |
| SPM-R004 | Persist schedule definitions with strongly typed target kind, target id, CRON expression, time zone, misfire policy, enabled state, start/end windows, and audit timestamps. | Must | EF model, validation tests, and query tests cover required fields and constraints. |
| SPM-R005 | Persist and display a human-readable CRON description for every workflow/process schedule. | Must | CRON description service tests cover Quartz-style CRON expressions and invalid input errors. |
| SPM-R006 | Provide a `Scheduled runs` tab showing active schedules, next planned fire, last fire, state, target, CRON, CRON description, time zone, and recovery/misfire indicators. | Must | Component and browser proof. |
| SPM-R007 | Provide a `New schedule` tab for creating schedules with target selection, CRON validation, live description preview, time zone, misfire policy, enabled toggle, start/end windows, and run metadata. | Must | Component tests for validation states and browser proof. |
| SPM-R008 | Provide a `Run history` tab with search/filter over old scheduled fires and target run outcomes. | Must | Query tests, component tests, and browser proof. |
| SPM-R009 | A scheduled fire must create a durable schedule fire/history row and launch the target through typed process/workflow adapters, not inline Quartz job logic. | Must | Integration tests prove durable handoff and target adapter invocation. |
| SPM-R010 | Schedule fire history must correlate schedule id, trigger id, Automation envelope/delivery where available, workflow/process run id, status, timestamps, error summary, and retry/dead-letter state. | Must | Query and history UI tests. |
| SPM-R011 | Duplicate fires must be prevented with deterministic dedupe/correlation keys. | Must | Integration test for repeated trigger fire request. |
| SPM-R012 | Logs must include actionable scheduler state and correlation ids while masking sensitive payloads. | Must | Code review and targeted logging tests where feasible. |
| SPM-R013 | Existing Automation diagnostics and tests must keep working. | Must | Existing Automation integration tests pass. |
| SPM-R014 | UI must follow existing BaseLib/Radzen-style component patterns and avoid raw ad hoc UI for standard controls. | Must | Component review and browser screenshot review. |
| SPM-R015 | Navigation must expose the Scheduler/Planner page without hiding the existing `/automation` diagnostics page. | Should | Navigation/component proof. |
| SPM-R016 | The implementation must document package/version choices for CRON descriptions and Quartz serialization/store dependencies. | Should | Architecture note or implementation report. |
| SPM-R017 | SchedulerPlanner must use the existing CanvasLib `CanvasCalendar` for scheduled-run calendar preview. | Must | Calendar surface mapper tests and browser proof that CanvasCalendar renders planned/actual schedule events. |
