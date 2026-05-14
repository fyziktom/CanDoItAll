# Requirement Traceability

| Requirement | Owning subbundle(s) | Required proof |
| --- | --- | --- |
| SPM-R001 | 01, 04 | Module registration, route reachable, navigation proof. |
| SPM-R002 | 02 | Integration test proving SchedulerPlanner creates Automation trigger projection and Quartz fires through Automation job. |
| SPM-R003 | 02, 05 | Restart/recovery test with Quartz persistent store tables. |
| SPM-R004 | 01 | EF mapping and persistence tests. |
| SPM-R005 | 01, 04 | CRON description service tests and UI preview tests. |
| SPM-R006 | 04 | Component test and Playwright screenshot for `Scheduled runs`. |
| SPM-R007 | 04 | Component test and Playwright screenshot for `New schedule`. |
| SPM-R008 | 01, 04 | History query tests, component test, and Playwright screenshot for `Run history`. |
| SPM-R009 | 02, 03 | Fire handler integration test and adapter tests. |
| SPM-R010 | 01, 02, 03, 04 | History row persistence/query tests and UI history rendering. |
| SPM-R011 | 02 | Dedupe integration test for repeated fire message. |
| SPM-R012 | 02, 03, 05 | Logging review and failure-path tests. |
| SPM-R013 | 02, 05 | Existing Automation integration tests still pass. |
| SPM-R014 | 04 | Component code review and browser screenshot review. |
| SPM-R015 | 04 | Navigation route test or browser proof. |
| SPM-R016 | 01, 02, 05 | Execution report documents package/store choices. |
| SPM-R017 | 04, 05 | Mapper tests and browser screenshot/pixel proof showing CanvasCalendar scheduled-run preview. |
