# PRM-F18 — Variants, exceptions, input quality, and decision rights

## Objective

Model controlled variants, exception paths, input-quality requirements, explicit decision rights, and risk-based controls so the runtime handles real-world deviations without degenerating into bureaucracy.

## Priority and wave

- Priority: **High**
- Planned wave: **Wave 2**
- Depends on: **PRM-F04, PRM-F05, PRM-F06, PRM-F07, PRM-F17**

## Why this feature exists

Large-firm processes fail on bad intake, unclear decision rights, and unmanaged exceptions far more often than on missing happy-path diagrams.

## In scope

- Input completeness and quality rules
- Approved variants and exception playbooks
- Decision-right rules and override routes
- Risk-tiered controls to prevent unnecessary bureaucracy

## Non-goals

- Do not turn every low-risk step into an approval gate.
- Do not hide decision-right rules inside free-text notes.

## Primary repo touchpoints

- `src/CanDoItAll.Modules.Processes/ProcessInputQualityModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessExceptionServices.cs`
- `src/CanDoItAll.Modules.Processes/ProcessDecisionRightsService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessPolicyModels.cs`
- `src/CanDoItAll.Modules.Validation/*`
- `src/CanDoItAll.Modules.Security/SecurityModels.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessDecisionRightsIntegrationTests.cs`