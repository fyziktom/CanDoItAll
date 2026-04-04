# Validation plan

## Validation strategy

Add failing characterization and invariant tests before refactor; keep them green throughout each phase.

## Required validation cases

- Each major invariant has at least one positive and one negative test.
- Projection outputs stay equivalent after refactors.
- Regression tests fail if duplicated truth reappears.

## Test commands

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj`
- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj`

## Closure rule

This finding closes only when the acceptance criteria are met **and** the validation cases above are evidenced in a real .NET environment.
