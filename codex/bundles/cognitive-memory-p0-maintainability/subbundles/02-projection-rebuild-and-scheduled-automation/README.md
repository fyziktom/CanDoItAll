# Projection Rebuild And Scheduled Automation

## Status

- `Completed`

## Objective

- Add explicit product paths for projection rebuild and Cognitive Memory scheduled automation execution.

## Success Criteria

- Rebuild-required projections can be processed through a service/API path with projected/failed/skipped counts.
- Automation schedule settings can be evaluated explicitly and run ingestion/consolidation with a summary.
- Tests cover core success and no-op/failure cases.

## Covered Inputs

- CM-P0-002 projection rebuild.
- CM-P0-003 scheduled automation execution.

## Prerequisites

- Subbundle 01 compile gate passed or no conflicting split remains.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Taxonomy\CognitiveMemoryTaxonomyContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Taxonomy\CognitiveMemoryTaxonomyServices.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Settings\CognitiveMemorySettingsContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Settings\CognitiveMemorySettingsServices.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit

## Deliverables

- Projection rebuild contract and service.
- Automation runner contract and service.
- API endpoint(s) for explicit execution.
- Tests proving behavior.

## Dependency Impact

- Docs cannot mark P0 projection/automation complete without this service/test proof.

## Validation Depth

- Operational hardening.

## Implementation Steps

1. Add contracts and service implementations.
2. Register services in DI.
3. Add explicit API endpoints.
4. Add targeted tests.

## Scope Exceptions

- Hosted background worker was deferred. P0 implemented explicit service/API execution that honors schedule settings without hidden background mutation; the roadmap now calls out the hosted-scheduler decision as a residual.

## Do Not Do

- Do not make projection canonical memory.
- Do not run background automation silently without explicit execution proof.

## Acceptance Checklist

- Rebuild service processes stale records.
- Automation service respects manual/no-op schedule state.
- Errors are reported explicitly.
- Tests pass.

## Proof Required

- Targeted unit tests for new services.
- API compile/build proof.

## Proof Captured

- Added `ICognitiveMemoryProjectionRebuildService` and `CognitiveMemoryProjectionRebuildService`.
- Added `ICognitiveMemoryScheduledAutomationRunner` and `CognitiveMemoryScheduledAutomationRunner`.
- Added `/api/cognitive-memory/projections/rebuild` and `/api/cognitive-memory/automation/run`.
- Added `CognitiveMemoryOperationalServicesTests`.
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemory|FullyQualifiedName~AgentContextContributionTests" --logger "console;verbosity=minimal" -m:1` passed 135/135.
- `dotnet build src\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore -m:1 --verbosity:minimal` passed with 0 warnings and 0 errors.

## Browser Validation Logging

- N/A - service/API behavior only.

## Progression Gate

- Agent context/docs work may continue only after projection and automation tests pass or blockers are recorded.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
