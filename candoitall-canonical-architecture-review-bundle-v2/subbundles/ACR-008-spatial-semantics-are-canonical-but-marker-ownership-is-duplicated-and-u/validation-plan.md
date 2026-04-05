# Validation plan

## Validation strategy

Signal/spatial mutation tests and migration assertions proving marker edits round-trip through one owner while view-state remains separate.

## Required validation cases

- Changing zoom or selection does not mutate canonical data.
- Changing a semantic marker updates one owner only.
- Node reclassification preserves spatial semantics unless an explicit policy says otherwise.

## Test commands

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests"`
- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~Marker|FullyQualifiedName~ViewState"`

## Closure rule

This finding closes only when the acceptance criteria are met **and** the validation cases above are evidenced in a real .NET environment.
