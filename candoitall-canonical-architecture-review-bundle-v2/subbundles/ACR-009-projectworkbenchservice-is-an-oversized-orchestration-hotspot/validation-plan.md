# Validation plan

## Validation strategy

Characterization tests around façade behavior, constructor dependency shrinkage, and targeted tests for extracted collaborators.

## Required validation cases

- A node metadata change test no longer requires going through the entire workbench service hotspot.
- Graph assembly can be tested independently from node mutation.
- Artifact/media code can evolve without touching unrelated relation logic.

## Test commands

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests|FullyQualifiedName~ProjectPartyAssignmentIntegrationTests"`

## Closure rule

This finding closes only when the acceptance criteria are met **and** the validation cases above are evidenced in a real .NET environment.
