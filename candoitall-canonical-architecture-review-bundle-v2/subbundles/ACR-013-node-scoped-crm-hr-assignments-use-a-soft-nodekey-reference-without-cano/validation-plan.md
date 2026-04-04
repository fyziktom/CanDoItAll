# Validation plan

## Validation strategy

Add failing tests for orphan node keys, wrong-project node keys, and disallowed node-kind role assignments before refactor; make them green via one validator.

## Required validation cases

- Saving an assignment for a non-existent node fails.
- Saving a `WorkItemAssignee` assignment on a node that is not a work item fails.
- Saving a node-scoped assignment for a node in another project fails.

## Test commands

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectPartyAssignmentIntegrationTests|FullyQualifiedName~CrmHrCrossModuleIntegrationTests"`

## Closure rule

This finding closes only when the acceptance criteria are met **and** the validation cases above are evidenced in a real .NET environment.
