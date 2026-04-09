# PRM-F20 — Change governance, prioritization, literacy, and management adoption

## Objective

Govern process changes through impact analysis, prioritization, communications, role-specific guidance, and management sponsorship so process management becomes an operating discipline rather than a document library.

## Priority and wave

- Priority: **High**
- Planned wave: **Wave 3**
- Depends on: **PRM-F02, PRM-F06, PRM-F10, PRM-F17**

## Why this feature exists

Immutable versions alone do not create management adoption or prevent unofficial live variants; process change needs an operating model.

## In scope

- Change requests with impact analysis
- Criticality-based governance and prioritization
- Communication and acknowledgement for process changes
- Role-based guidance and process literacy aids

## Non-goals

- Do not force identical governance depth on every low-impact process.
- Do not treat immutable version storage as sufficient change governance by itself.

## Primary repo touchpoints

- `src/CanDoItAll.Modules.Processes/ProcessChangeGovernanceModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessChangeGovernanceService.cs`
- `src/CanDoItAll.Modules.Processes/Pages/ProcessGovernancePage.razor`
- `src/CanDoItAll.Modules.Activity/*`
- `src/CanDoItAll.Components.BaseLib/Components/*`
- `tests/CanDoItAll.Tests.Playwright/ProcessGovernanceFlowTests.cs`