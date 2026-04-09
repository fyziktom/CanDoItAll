# Codex task — PRM-F20

Implement **Change governance, prioritization, literacy, and management adoption** inside the uploaded CanDoItAll solution.

## Constraints

- Treat `CanDoItAll.Modules.Processes` as the canonical owner for process-management behavior.
- Reuse CRM-HR, Activity, Automation, Validation, TestLab, and Security seams where the bundle says so.
- Do not add direct compile-time dependency on the uploaded AgentFramework repo.
- Keep all code comments in English.
- Preserve buildability for the current solution layout.

## Required outputs

- Code changes for this feature
- Matching tests
- Migration updates if persistence changes
- A short implementation note describing what changed and how it was verified

## Done definition

- Change proposals capture reason, impacted processes and roles, expected outcomes, risk, and rollout plan.
- Publish, retire, and critical-change operations can require governance approval based on criticality and impact.
- Affected owners, stewards, approvers, and participants receive communication and acknowledgement tasks when governed versions change.
- The process portfolio can classify criticality and prioritization tiers so not every process is modeled to the same depth.
- UI surfaces provide role-based guidance and glossary/help so middle management and operators can understand the process model.

## Recommended first files to touch

- `src/CanDoItAll.Modules.Processes/ProcessChangeGovernanceModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessChangeGovernanceService.cs`
- `src/CanDoItAll.Modules.Processes/Pages/ProcessGovernancePage.razor`
- `src/CanDoItAll.Modules.Activity/*`
- `src/CanDoItAll.Components.BaseLib/Components/*`
- `tests/CanDoItAll.Tests.Playwright/ProcessGovernanceFlowTests.cs`