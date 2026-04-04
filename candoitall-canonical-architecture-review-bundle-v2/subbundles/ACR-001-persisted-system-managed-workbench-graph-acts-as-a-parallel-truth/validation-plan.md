# Validation plan

## Validation strategy

Add assembled-graph equivalence tests for structure/calendar/Gantt and actor overlays; prove any retained cache rebuild is lossless; verify system-managed rows are no longer authoritative.

## Required validation cases

- Deleting the cache and rebuilding it does not change structure/calendar/Gantt outcomes.
- Actor overlays appear in the same place regardless of whether a cache exists.
- Upstream aggregate changes appear in the assembled graph without requiring duplicated truth edits.

## Test commands

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests|FullyQualifiedName~ProjectPartyAssignmentIntegrationTests|FullyQualifiedName~CrmHrCrossModuleIntegrationTests"`

## Closure rule

This finding closes only when the acceptance criteria are met **and** the validation cases above are evidenced in a real .NET environment.
