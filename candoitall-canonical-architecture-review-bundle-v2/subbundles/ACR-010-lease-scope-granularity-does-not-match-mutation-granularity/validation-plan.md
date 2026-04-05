# Validation plan

## Validation strategy

Concurrency tests, lease contention telemetry, and authorization tests for node/project/repo-branch scope selection.

## Required validation cases

- Two independent node edits in the same project do not block each other unnecessarily.
- A subtree move still acquires a broad enough lock to stay safe.
- Lock scope is visible and explainable in logs/telemetry.

## Test commands

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectStructureAgent|FullyQualifiedName~Lease"`

## Closure rule

This finding closes only when the acceptance criteria are met **and** the validation cases above are evidenced in a real .NET environment.
