# Validation plan

## Validation strategy

Projection-equivalence tests, golden files for Gantt/Calendar, and proof that projection builders are side-effect free.

## Required validation cases

- Changing schedule data updates Gantt without any cache rewrite step.
- A node with no schedule facet does not accidentally leak into calendar views.
- Export generation never mutates canonical state.

## Test commands

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests|FullyQualifiedName~ProjectPartyAssignmentIntegrationTests"`

## Closure rule

This finding closes only when the acceptance criteria are met **and** the validation cases above are evidenced in a real .NET environment.
