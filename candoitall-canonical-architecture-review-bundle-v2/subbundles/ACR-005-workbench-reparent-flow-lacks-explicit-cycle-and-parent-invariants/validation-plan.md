# Validation plan

## Validation strategy

Add failing tests for self-parent, descendant-parent, cross-project edge violations, and illegal kind-to-kind relations before refactor; make them green through one invariant service.

## Required validation cases

- Self-parent attempt is rejected.
- Parenting under a descendant is rejected.
- Cross-project reparenting is rejected unless explicitly supported by a dedicated transfer flow.

## Test commands

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests|FullyQualifiedName~ProjectStructureAgentIntegrationTests|FullyQualifiedName~ProjectsServiceIntegrationTests"`

## Closure rule

This finding closes only when the acceptance criteria are met **and** the validation cases above are evidenced in a real .NET environment.
