# Validation plan

## Validation strategy

Attachment binding tests, route resolver tests, and project move tests proving routes are recomputed rather than rewritten as canonical data.

## Required validation cases

- A change in storage provider does not change the node carrier schema.
- Artifact bindings can be migrated or rehydrated independently of node kind transitions.
- Route/UI changes do not require domain data migrations.

## Test commands

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests"`

## Closure rule

This finding closes only when the acceptance criteria are met **and** the validation cases above are evidenced in a real .NET environment.
