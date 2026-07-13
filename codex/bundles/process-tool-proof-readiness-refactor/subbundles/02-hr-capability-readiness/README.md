# 02-hr-capability-readiness

## Status

- `Completed`

## Objective

- Make project-structure HR matching and process launch readiness evaluate the effective step contract before run start and before dispatch.

## Success Criteria

- HR matching can report missing or suppressed tool, skill, MCP, capability, and access requirements.
- Launch preview can detect that a process step cannot satisfy required proof before execution.
- Readiness uses the same compiled contract as runtime metadata and receipt gates.

## Covered Inputs

- R2 HR Readiness And Matching.
- R7 Testability And Performance.
- User question: whether the HR matching dialog/procedure could detect that an agent lacks required access/tool/skill/MCP for a process step.

## Prerequisites

- `01-runtime-receipt-contracts` completed with an effective step contract and typed required receipts.
- Contract compiler available without launching providers.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.Processes.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureProcessAssignmentDialog.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Components/ProjectPartyAssignmentPanel.razor`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessProviderReadinessRules.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/CapabilitySetup/AgentCapabilitySetupFlowService.cs`
- `bundle://architecture/04-csharp-testability-plan.md`

## Deliverables

- Readiness evaluator service that compares selected agent/runtime capabilities to the effective process step contract.
- Typed readiness gap reason codes for missing capability, missing MCP server, missing MCP tool, missing runtime tool, suppressed skill/tool, and missing project access.
- Launch preview and HR matching projection updates that surface readiness gaps clearly.
- Tests for positive readiness, missing Playwright, missing image tool, suppressed development skill, and project access denial.

## Dependency Impact

- `04-template-process-e2e` depends on readiness gaps being visible before launch.
- `03-manager-fallback-drivers` can reuse readiness diagnostics for reassignment decisions.

## Validation Depth

- Critical launch and assignment gate.
- Unit, projection, and at least one UI/route validation pass are required if rendered readiness output changes.

## Implementation Steps

1. Add a process application readiness evaluator that accepts compiled step contract plus agent/runtime capability snapshot.
2. Keep readiness comparison side-effect free; do not launch MCP servers or runtime tools.
3. Replace or wrap component-local readiness logic with calls into the evaluator.
4. Add projection fields for contract readiness status and typed gap details.
5. Update HR matching and launch preview UI to show blocking readiness gaps.
6. Add tests for missing and suppressed tools/skills/MCPs.
7. Validate readiness performance against repeated role matching.

## Scope Exceptions

- Do not implement fallback routing in this phase.
- Do not migrate all templates in this phase.
- Do not launch actual Playwright from readiness checks.

## Do Not Do

- Do not duplicate contract compilation in Blazor components.
- Do not represent readiness gaps as arbitrary strings in core logic.
- Do not mutate the agent's main settings to make a step-specific suppression work.
- Do not hide readiness failures behind generic "not ready" output.

## Acceptance Checklist

- HR matching distinguishes "agent lacks Playwright MCP" from "Playwright is suppressed for this step".
- A management-only step can suppress a development skill without changing the agent definition.
- Launch preview blocks or warns when a required proof tool is unavailable.
- Readiness output includes actionable state for the process manager and user.
- Tests cover repeated readiness evaluations without provider startup.

## Proof Required

- `dotnet test` for readiness evaluator and projection tests.
- UI or component test proof for readiness gap rendering if Blazor output changes.
- Source proof that project-structure components no longer own core contract readiness decisions.
- Performance note showing readiness uses cached/static capability data.

## Browser Validation Logging

- Route: project structure process assignment or process launch preview route affected by the implementation.
- Viewport: large desktop pass and a narrower-width pass if the dialog layout changes.
- Playwright evidence: navigate to the dialog, inspect readiness gap text, and capture screenshot if UI is modified.

## Progression Gate

- Downstream work may start only after readiness can block or warn on missing/suppressed proof capabilities before launch.

## C# Architecture Impact

- Extracts deterministic readiness logic from UI partials into testable process application services.

## Boundary Ownership

- Workbench renders readiness. Process application owns the readiness decision and typed reason codes.

## Dependency Direction

- Workbench may depend on process application services. Process application must not depend on Workbench UI.

## Pattern Decision

- Use a stateless evaluator with typed result records. Do not introduce a strategy chain unless multiple materially different readiness algorithms appear.

## Testability Contract

- Evaluator tests must build fixtures for agents, tools, MCPs, skills, access scopes, and compiled contracts without a database or browser.

## Partial Class Policy

- Avoid adding more readiness logic to `ProjectStructurePage.Processes.cs`; move non-trivial logic into services.

## Architecture Proof Required

- Include source proof of UI-to-service delegation.
- Include tests demonstrating suppression does not alter the persisted agent definition.

## Suggested Agent Prompt

```text
Implement subbundle 02 only. Use the compiled contract from subbundle 01 to evaluate HR/project-structure readiness, keep checks side-effect free, add typed readiness gap results, update UI projections only as needed, and capture targeted tests plus UI proof if rendering changes.
```
