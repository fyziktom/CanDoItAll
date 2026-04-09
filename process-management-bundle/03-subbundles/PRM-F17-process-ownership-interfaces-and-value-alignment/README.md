# PRM-F17 — Process ownership, interfaces, customer, and value alignment

## Objective

Make process owner, sponsor, customer, strategic objective, criticality, and upstream/downstream interfaces first-class so processes are governed end-to-end by value flow rather than org-chart convenience.

## Priority and wave

- Priority: **Critical**
- Planned wave: **Wave 1**
- Depends on: **PRM-F02, PRM-F03, PRM-F04**

## Why this feature exists

The senior review showed that a process-management system without owner, customer, and interface semantics will optimize local boxes but not end-to-end value flow.

## In scope

- Process owner, sponsor, customer, and criticality metadata
- Strategic objective and value statement links
- Upstream and downstream interface contracts
- Definition-of-done semantics for interfaces and handoffs

## Non-goals

- Do not force the process graph to mirror the org chart.
- Do not allow publish of critical processes without explicit owner/customer metadata.

## Primary repo touchpoints

- `src/CanDoItAll.Modules.Processes/ProcessGovernanceModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessPortfolioServices.cs`
- `src/CanDoItAll.Modules.Processes/ProcessInterfaceServices.cs`
- `src/CanDoItAll.Modules.Processes/Pages/ProcessGovernancePage.razor`
- `src/CanDoItAll.Modules.Processes/Pages/ProcessDesignerPage.razor`
- `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessOwnershipIntegrationTests.cs`