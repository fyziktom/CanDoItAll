# Validation plan

## Validation strategy

Relation invariant tests, graph cycle tests, and projection tests proving hierarchy and dependency semantics remain distinct.

## Required validation cases

- Moving a node changes containment only once.
- A parent-child relationship is not automatically mistaken for a critical-path dependency.
- Dependency traversal and hierarchy traversal produce predictable, testable results.

## Test commands

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests|FullyQualifiedName~ProjectsServiceIntegrationTests"`

## Closure rule

This finding closes only when the acceptance criteria are met **and** the validation cases above are evidenced in a real .NET environment.
