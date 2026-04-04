# Validation plan

## Validation strategy

Migration tests for the record split, serialization round-trips, and mutation tests proving each concern is edited through the correct owner.

## Required validation cases

- A node can change typed facet without rewriting unrelated storage/media/schedule fields.
- A schema change in one facet does not force touching the base carrier table.
- Serialization/import/export can clearly separate core node data from facet data.

## Test commands

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests"`
- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~Node|FullyQualifiedName~Facet|FullyQualifiedName~Metadata"`

## Closure rule

This finding closes only when the acceptance criteria are met **and** the validation cases above are evidenced in a real .NET environment.
