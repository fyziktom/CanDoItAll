# Runtime Diagnostics Lineage

## Status

- `Ready`

## Objective

- Persist and project typed blocked-result diagnostics so a process escalation can be classified without direct database spelunking or prompt guessing.

## Success Criteria

- Blocked `NeedsManager` results expose safe diagnostic code, category, summary, retry safety, idempotency, source step/run, and failed capability/tool/artifact when available.
- API/projection readback shows actionable blocked reasons for step and run detail.
- Result and artifact lineage can point from a blocked step to the relevant finalizer result and produced artifact refs.

## Covered Inputs

- R01 Runtime Diagnostics.
- R02 Artifact And Result Lineage.
- Latest run observation that `process_projection_history` had generic blocked payloads only.
- Latest run observation that `process_strategy_result_receipts` had hashes/statuses but not actionable result details.

## Prerequisites

- Rollback of the prior generic .NET validation change remains intact.
- No domain-specific repair logic is added in this subbundle.

## Exact Source References

- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.Results.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimePorts.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeState.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeEvidenceSourceProvider.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit`
- `repo://tests/Integration/CanDoItAll.Tests.Integration`

## Deliverables

- Typed persisted diagnostic record or safe normalized diagnostic payload linked to strategy result receipts.
- Projection/read-model enrichment for blocked step/run details.
- Artifact/result lineage readback sufficient to locate primary evidence refs.
- Characterization tests for the current missing-diagnostics behavior.
- Unit and integration tests for new diagnostic persistence and projection.

## Dependency Impact

- SB02 can use typed diagnostics to explain readiness failures.
- SB03 depends on these diagnostics for recovery classification.
- SB04 and SB05 depend on this proof to avoid guessing whether .NET/template changes caused an escalation.
- Weak proof here invalidates the whole bundle because later phases would still be diagnosing from incomplete read models.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Add characterization tests that submit a `NeedsManager`/blocked strategy result and prove current readback loses actionable diagnostics.
2. Design the smallest durable diagnostic model that stores safe diagnostic facts without persisting sensitive provider text.
3. Persist normalized diagnostics beside or near strategy result receipts.
4. Project blocked diagnostic summaries into run detail, live process, and history readbacks where appropriate.
5. Enrich evidence-source snapshots with the same diagnostic facts.
6. Add artifact lineage fields or resolvers needed to connect produced artifact slots to stable refs.
7. Add regression tests for blocked result, missing artifact, and diagnostic-free fallback behavior.

## Scope Exceptions

- Do not implement readiness policy or recovery strategy selection here.
- Do not change process templates in this subbundle except test fixtures required for diagnostics.

## Do Not Do

- Do not add .NET, Blazor, Calculator, Tetris, screenshot, or Playwright-specific diagnostics to generic runtime.
- Do not store raw provider text if it may contain sensitive data; store safe summaries and typed references.
- Do not hide missing diagnostics by returning a generic "unknown" success path.
- Do not add a new partial class to hide responsibility growth.

## Acceptance Checklist

- A blocked step with diagnostics can be inspected through process API/projection without direct DB queries.
- A blocked step with no diagnostics produces an explicit missing-diagnostics category.
- Result receipt lineage links to diagnostic records deterministically.
- Artifact ledger readback includes stable refs or a documented resolver.
- Unit and integration tests fail on the old behavior and pass on the new behavior.

## Proof Required

- `dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter Process`
- `dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter Process`
- API readback sample for a synthetic blocked run showing diagnostic category and summary.
- Architecture scan showing no domain-specific strings added to generic runtime files.

## Browser Validation Logging

- N/A: no browser-visible behavior is expected in this subbundle.

## Progression Gate

- SB02 and SB03 may start only after blocked diagnostics and artifact/result lineage are readable through projections/API and covered by tests.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Start with characterization tests for the missing blocked diagnostics. Add the smallest generic diagnostic persistence/projection path that explains blocked strategy results without domain-specific rules. Keep runtime domain-neutral, avoid raw sensitive provider text, capture unit and integration proof, update the execution report, and stop if blocked reasons still require direct database inspection.
```
