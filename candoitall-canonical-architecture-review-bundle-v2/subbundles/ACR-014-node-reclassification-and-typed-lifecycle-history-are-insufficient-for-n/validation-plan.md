# Validation plan

## Validation strategy

Add transition tests for note→task, note→decision, and note→block; verify history survives; verify assignments and spatial semantics are preserved or transformed by policy.

## Required validation cases

- A note can become a task while keeping the same node identity and same XY/marker context.
- Transition history can be queried later for analytics or review.
- Illegal transitions are rejected by the registry.

## Test commands

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests"`
- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~NodeTransition|FullyQualifiedName~Facet"`

## Closure rule

This finding closes only when the acceptance criteria are met **and** the validation cases above are evidenced in a real .NET environment.
