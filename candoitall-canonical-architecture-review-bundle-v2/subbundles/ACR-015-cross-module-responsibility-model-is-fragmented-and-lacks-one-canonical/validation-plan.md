# Validation plan

## Validation strategy

Add canonical-ownership tests and migration tests proving which layer owns each responsibility fact and how mirrors stay consistent.

## Required validation cases

- Project-level customer/delivery-unit/manager data is not confused with node-level assignees or aggregate-local responsibility.
- Cross-module views can answer 'who owns what' without reading contradictory fields.
- Later migration paths are documented before any module-local field is removed.

## Test commands

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~CrmHrCrossModuleIntegrationTests|FullyQualifiedName~ProjectPartyAssignmentIntegrationTests"`

## Closure rule

This finding closes only when the acceptance criteria are met **and** the validation cases above are evidenced in a real .NET environment.
