# SB05-ef-console-logging-option-and-final-validation

## Status

- `Completed`

## Objective

Make EF console output opt-in with a default-off configuration option, then validate the full hardening change set.

## Success Criteria

- `DatabaseOptions` has a strongly typed EF console logging option that defaults to false.
- Web host logging filters suppress EF command/infrastructure categories when the option is false.
- App settings make the default explicit.
- Unit tests cover the default and config binding behavior.
- Targeted component tests, web build, and web startup validation pass.

## Covered Inputs

- `REQ-EF-001`
- Final validation for `REQ-PROC-001`, `REQ-PROJ-001`, and `REQ-WF-001`

## Prerequisites

- `SB02` complete.
- `SB03` complete.
- `SB04` complete.

## Exact Source References

- `repo://src/CanDoItAll.Infrastructure/Configuration/AppOptions.cs`
- `repo://src/CanDoItAll.Web/Program.cs`
- `repo://src/CanDoItAll.Web/appsettings.json`
- `repo://src/CanDoItAll.Web/appsettings.Development.json`
- `repo://tests/CanDoItAll.Tests.Unit/DatabaseConfigurationTests.cs`
- `repo://src/CanDoItAll.Web/CanDoItAll.Web.csproj`

## Deliverables

- Default-off EF logging configuration.
- Unit tests.
- Final execution report with commands and results.

## Dependency Impact

- This is the final closure subbundle. Failure here reopens the relevant implementation subbundle or logging option work.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Add the logging option to `DatabaseOptions`.
2. Apply EF logging category filters in web startup when the option is false.
3. Add explicit appsettings entries.
4. Add unit tests for default and binding.
5. Run targeted component/unit tests.
6. Build the web project.
7. Start the web app and verify it reaches a ready HTTP endpoint.
8. Update execution report and final bundle status.

## Scope Exceptions

- This phase does not change EF provider selection or connection-string handling.

## Do Not Do

- Do not disable all application logging.
- Do not make noisy EF console output the default.

## Acceptance Checklist

- EF logging default is false.
- Config can turn EF console logging on.
- Tests and web startup pass.
- Execution report records all proof.

## Proof Required

- Targeted unit test command covering `DatabaseConfigurationTests`.
- Targeted component test commands from earlier subbundles.
- `dotnet build src/CanDoItAll.Web/CanDoItAll.Web.csproj`
- Web-app startup proof and log path.

## Browser Validation Logging

- Target route: web host readiness endpoint or home page.
- Viewport passes: N/A unless layout changes are introduced.
- Playwright actions or assertions: N/A unless layout changes are introduced.
- Screenshot evidence: N/A unless layout changes are introduced.
- Review questions: confirm host starts without default EF command log noise.

## Progression Gate

- All proof must pass before final response.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
