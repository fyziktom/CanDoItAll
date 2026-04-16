# Live showcase execution, bug harvest, and closure

## Status

- `Completed`

## Objective

- Execute the provisioned calculator-delivery showcase end to end, keep fixing blockers until the workflow passes, and record every discovered bug, gap, artifact, and closure decision in the bundle.

## Covered Inputs

- `U004`
- Functional requirements `9` and `10`
- Final bundle closure conditions

## Prerequisites

- Prepared bundle validator pass
- Closed subbundle `01-cross-module-agent-source-alignment`
- Closed subbundle `02-processes-workspace-and-database-profile-ux-fixes`
- Closed subbundle `03-template-driven-showcase-provisioning-and-agent-capability-wiring`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.RunStart.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.StepTransitions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRunAutomationDispatchService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.RunsPresenter.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.RuntimeOperations.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrAgentsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\AgentsHomePage.razor.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureProcesses.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProcessManagementBundle.cs`
- `C:\repositories\CanDoItAll\agent_showcase_e2e_bundle\reviews\01-execution-report.md`

## Deliverables

- A fully executed showcase flow for delivering the calculator application or an explicit blocking record that keeps the bundle open.
- Bug fixes for any missing behavior required to make the workflow pass.
- Bundle evidence for process progression, artifact handoffs, QA validation, agent assignment, and project-structure updates.
- Updated execution report with subbundle gates, browser analytics, command history, and raw-note closure state.

## Dependency Impact

- This is the final execution and closure phase. Weak proof here would invalidate the entire bundle because the user explicitly said the bundle is not done unless the full end-to-end test passes correctly.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Run the provisioned showcase through project structure, process runtime, CRM-HR resource sourcing, and agent task execution.
2. Observe failures, missing artifacts, stale status updates, or blocked assignments and fix them immediately when they block the intended workflow.
3. Re-run the affected part of the showcase after each blocker fix instead of stacking unverified changes.
4. Capture screenshots, logs, and any exported runtime evidence for each major stage.
5. Update the execution report continuously until raw notes are either fully closed or explicitly still blocking closure.

## Scope Exceptions

- No closure exception is allowed for the final showcase pass. If the flow does not pass end to end, this phase remains open.

## Do Not Do

- Do not declare success based only on seeding or partial process-start evidence.
- Do not ignore missing artifact handoffs, QA steps, or project-structure progress updates.
- Do not leave discovered blockers undocumented in the bundle.

## Acceptance Checklist

- The showcase reaches a completed or otherwise terminally validated delivery state for the calculator app.
- Roles are fulfilled by the expected agents and artifacts flow between steps rather than staying implicit in chat or memory.
- QA-related agents or steps validate the delivered application.
- Project structure reflects meaningful progress or completion for the showcase work.
- `reviews/01-execution-report.md` is updated with actual evidence, not placeholders.

## Proof Required

- Runtime command or service proof that the showcase processes were started and progressed.
- Browser proof for project structure, process runtime, CRM-HR or agent assignment surfaces, and final delivered application state.
- Planned screenshots:
  - `C:\repositories\CanDoItAll\agent_showcase_e2e_bundle\reviews\evidence\04-showcase-process-runtime.png`
  - `C:\repositories\CanDoItAll\agent_showcase_e2e_bundle\reviews\evidence\04-showcase-project-progress.png`
  - `C:\repositories\CanDoItAll\agent_showcase_e2e_bundle\reviews\evidence\04-showcase-calculator-app.png`
- Updated execution-report tables and raw-note closure rows.

## Closure Evidence

- Final showcase process run `aff6699b-5c0f-441b-b484-4fadfad41ab1` completed all eight steps with succeeded execution runs for scope, architecture, implementation, peer review, QA, security, release approval, rollout, and post-release learning.
- The generated app at `showcases/blazor-ssr-calculator/app/SimpleCalculatorApp` remained on the expected static SSR baseline (`net10.0`, `Program.cs` static SSR host, `Home.razor` GET-driven calculator flow).
- Durable UI evidence was imported for both `qa-validation` and `execute-release-rollout`, including screenshot, page snapshot, console log, and import summary files under the managed workspace artifacts root.
- The final bug-harvest loop closed the required-tool negation bug and the missing Playwright step-directory bug before the successful rerun.

## Browser Validation Logging

- Target routes: showcase project structure page, showcase process workspace page, CRM-HR or agent resource page as needed, and the delivered calculator app surface
- Required viewport: `1600x900`
- Required browser actions: navigate the showcase routes, inspect progression state, verify artifact or assignment visibility, and capture final application proof.
- Review questions:
  - Did the process actually progress through the expected development and QA path?
  - Are artifacts and progress updates visible in the system rather than inferred?
  - Does the delivered calculator app visibly satisfy the intended scope?

## Progression Gate

- Bundle closure is allowed only when the live showcase passes end to end, blocker fixes are verified, and the execution report contains concrete evidence for every raw note and every subbundle gate.

## Suggested Agent Prompt

```text
Implement subbundle 04 only. Execute the provisioned calculator showcase end to end, fix every blocker required to make it pass, and keep the execution report current with real evidence. Do not close the bundle on partial success.
```
