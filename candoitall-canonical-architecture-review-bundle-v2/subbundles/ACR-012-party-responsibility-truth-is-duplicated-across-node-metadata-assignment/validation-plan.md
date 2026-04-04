# Validation plan

## Validation strategy

Add tests proving there is only one writable source for node-scoped responsibility, and verify that module-level mirrors do not drift from the canonical assignment owner.

## Required validation cases

- Meeting participants can be edited without also maintaining a second authoritative list in metadata.
- Changing a party display name updates projections without mutating node metadata truth.
- Participant nodes still support project-local-only behavior without forcing directory linkage.

## Test commands

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectPartyAssignmentIntegrationTests|FullyQualifiedName~CrmHrCrossModuleIntegrationTests"`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~CrossModuleResponsiblePartyPageTests"`

## Closure rule

This finding closes only when the acceptance criteria are met **and** the validation cases above are evidenced in a real .NET environment.
