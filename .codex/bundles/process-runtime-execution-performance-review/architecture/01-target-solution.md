# Target Solution

## Boundary

Keep process runtime generic. Runtime code may optimize how it indexes and walks already-loaded process data, but it must not infer stack-specific instructions or validation rules.

## Runtime Start Repair

- Pre-index role requirements by `StepDefinitionId` once after loading the run-start context.
- Pre-index artifact expectation titles by `StepDefinitionId` once for work brief evidence summaries.
- Build effective assignments once per step and pass the lookup into executor selection and capability-gap selection.
- Replace LINQ grouping in effective assignment resolution with a single-pass dictionary update that preserves the current precedence rules.

## Validation Strategy

- Run targeted `ProcessesServiceIntegrationTests` to preserve runtime start and transition semantics.
- Run process mock-agent coverage where feasible to ensure dispatch behavior remains intact.
- Run independent simple .NET app build smoke cases outside the process core.
- Run `dotnet build CanDoItAll.slnx -v:minimal` after targeted tests if time permits.
