# Requirement Traceability

| Requirement | Source notes | Owning subbundle | Source files | Proof |
| --- | --- | --- | --- | --- |
| R001 | N001, N003 | `01-01-01-performance-scan-and-hot-path-baseline` | `analysis/01-current-state.md` | Scan checklist in execution report. |
| R002 | N002, N003 | `01-01-01-performance-scan-and-hot-path-baseline` | Runtime and dispatch files in inventory | Hot-path decision recorded. |
| R003 | N007 | `02-02-02-runtime-start-and-transition-allocation-repair` | `ProcessesService.Runtime.RunStart.cs` | Code diff contains no stack-specific logic. |
| R004 | N004 | `03-03-03-dispatch-and-dotnet-validation-proof` | Integration tests | Targeted tests and build. |
| R005 | N002 | `02-02-02-runtime-start-and-transition-allocation-repair` | `ProcessesService.Runtime.RunStart.cs` | Runtime-start code avoids repeated scans. |
| R006 | N004 | `02-02-02-runtime-start-and-transition-allocation-repair` | Runtime start code | Existing explicit validation failures remain. |
| R007 | N004, N005 | `03-03-03-dispatch-and-dotnet-validation-proof` | Test projects | Targeted integration test results. |
| R008 | N006, N007 | `03-03-03-dispatch-and-dotnet-validation-proof` | `.artifacts/process-runtime-execution-performance-review` | Simple .NET app build smoke command results. |

## Raw Note Closure Matrix

| Raw note | Requirement IDs | Owning subbundle | Planned proof | Status |
| --- | --- | --- | --- | --- |
| N001 | R001, R002 | `01-01-01-performance-scan-and-hot-path-baseline` | Code scan and source inventory | In progress |
| N002 | R002, R005 | `02-02-02-runtime-start-and-transition-allocation-repair` | Code diff and tests | In progress |
| N003 | R001, R002 | `01-01-01-performance-scan-and-hot-path-baseline` | Scan checklist | In progress |
| N004 | R004, R006, R007 | `03-03-03-dispatch-and-dotnet-validation-proof` | Tests and build | Pending |
| N005 | R007 | `03-03-03-dispatch-and-dotnet-validation-proof` | Mock-agent targeted test or explicit gap | Pending |
| N006 | R008 | `03-03-03-dispatch-and-dotnet-validation-proof` | Simple .NET app build smokes | Pending |
| N007 | R003, R008 | `02-02-02-runtime-start-and-transition-allocation-repair` | Review diff for generic logic | In progress |
