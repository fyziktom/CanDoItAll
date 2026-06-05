# Target Boundary

## New module-local helper families

The implementation may choose exact class names, but must preserve this responsibility split:

| Helper family | Responsibility | Must not own |
| --- | --- | --- |
| `ProcessImplementationContractSnapshot` | Gather normalized contract text from run, step, work brief, expected artifacts, and optional context. | DB access, execution calls, driver APIs |
| `ProcessImplementationStackRules` | Detect `.NET`, JavaScript, negated `.NET`, explicit test request, runnable app contract signals. | File IO, tool execution |
| `ProcessImplementationReceiptTimeline` | Normalize successful receipts, failed receipts, latest mutation/read/validation/run receipts, receipt ordering. | Candidate mutation, DB writes |
| `ProcessConcreteProductPathRules` | Classify concrete product paths, deliverable/source paths, ignored paths, source/write paths. | Workspace mutation, driver calls |
| `ProcessConcreteImplementationProofRules` | Resolve missing implementation proof summary and concrete mutation/read proof. | Retry persistence, final transition |
| `ProcessRunnableApplicationProofRules` | Resolve runnable app proof summary and host/run proof. | Actual dotnet run execution |
| `ProcessDotNetHostEvidenceRules` | Find runnable .NET host project paths and invalid host shape summaries. | New dotnet driver API |
| `ProcessCarriedImplementationProofRules` | Carry proof state across attempts and historical runs. | Loading historical details |
| `ProcessImplementationProofDriverReadinessMap` | Documentation-only map for future drivers. | Production driver API |

## Existing wrappers

Existing methods in `ProcessRunAutomationDispatchService.ImplementationProof.cs` may remain as wrappers if tests or other partials rely on them. However, wrappers should delegate to helpers after each migrated section.

## No public surface

All production helper types stay `internal` and under `CanDoItAll.Modules.Processes`.
