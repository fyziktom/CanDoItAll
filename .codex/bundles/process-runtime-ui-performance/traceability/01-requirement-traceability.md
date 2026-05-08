# Requirement Traceability

## Matrix

| Raw note | Requirement | Owning subbundle | Planned proof |
| --- | --- | --- | --- |
| N001 | R001, R002, R003, R004 | `02-02-core-runtime-bottleneck-repair`, `03-03-ui-observation-bottleneck-repair` | Core timings, targeted test, browser timing. |
| N002 | R006, R007 | `01-01-current-state-and-measurement`, `04-04-browser-measurement-and-closure` | Timings recorded outside Visual Studio assumptions. |
| N003 | R001, R003 | `01-01-current-state-and-measurement` | Current-state analysis with exact source references. |
| N004 | R001, R002, R003, R004 | `02-02-core-runtime-bottleneck-repair`, `03-03-ui-observation-bottleneck-repair` | Code changes plus tests. |
| N005 | R006 | `01-01-current-state-and-measurement`, `02-02-core-runtime-bottleneck-repair` | Stopwatch timing before and after. |
| N006 | R007, R008 | `04-04-browser-measurement-and-closure` | Playwright route timing and screenshot. |
| N007 | R005 | `02-02-core-runtime-bottleneck-repair`, `04-04-browser-measurement-and-closure` | Targeted tests and build. |
