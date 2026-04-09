# Codex task — PRM-F17

Implement **Process ownership, interfaces, customer, and value alignment** inside the uploaded CanDoItAll solution.

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

- A process definition cannot be published without a process owner, primary customer, criticality tier, and value statement.
- Process definitions can declare sponsor, stewarding managers, strategic objective links, and upstream/downstream interface contracts.
- Interface contracts capture sender, receiver, required inputs/outputs, definition of done, and handoff expectation metadata.
- Actor assignments remain separate from org hierarchy; the model does not force the process graph to mirror reporting lines.
- Shared-project or shared-library processes preserve explicit ownership instead of being duplicated as shadow copies.

## Recommended first files to touch

- `src/CanDoItAll.Modules.Processes/ProcessGovernanceModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessPortfolioServices.cs`
- `src/CanDoItAll.Modules.Processes/ProcessInterfaceServices.cs`
- `src/CanDoItAll.Modules.Processes/Pages/ProcessGovernancePage.razor`
- `src/CanDoItAll.Modules.Processes/Pages/ProcessDesignerPage.razor`
- `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessOwnershipIntegrationTests.cs`