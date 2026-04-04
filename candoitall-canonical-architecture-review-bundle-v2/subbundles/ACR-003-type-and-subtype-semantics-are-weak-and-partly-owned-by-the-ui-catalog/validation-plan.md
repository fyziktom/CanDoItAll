# Validation plan

## Validation strategy

Registry tests; one semantic source for UI/MCP/import/compiler classification; smoke tests that UI catalogs are generated from registry definitions.

## Required validation cases

- Every node kind has one authoritative definition.
- Attempting an illegal transition or illegal relation fails with a clear error.
- Adding a new kind no longer requires copying semantic rules across UI and services.

## Test commands

- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~NodeKind|FullyQualifiedName~ProjectObjectMetadata"`

## Closure rule

This finding closes only when the acceptance criteria are met **and** the validation cases above are evidenced in a real .NET environment.
