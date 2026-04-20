# Live Agent Delivery Run And Observation

## Status

- `Ready`

## Objective

- Execute the serious units-converter delivery flow with real CanDoItAll agents, explicit human approvals, Playwright-backed QA, screenshot review, and detailed observation so process and architecture weaknesses are harvested from reality.

## Covered Inputs

- `N005`
- `N007`
- `N008`

## Prerequisites

- `subbundles/01-canonical-agentframework-ownership-and-crm-hr-projection` closed with proof
- `subbundles/02-openai-agent-capability-and-process-template-hardening` closed with proof
- `subbundles/03-units-converter-project-and-process-provisioning` closed with proof

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRunAutomationDispatchService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureAssemblyService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `C:\repositories\CanDoItAll\tools\CanDoItAll.ScenarioSeeder\AgentShowcaseCalculatorSeeder.Workflow.cs`
- `C:\repositories\CanDoItAll\units-converter-agent-delivery-bundle\reviews\01-execution-report.md`

## Deliverables

- A real process run for the serious units-converter project with agent activity, approvals, artifacts, and project-structure updates.
- Browser and runtime evidence for QA, review, and delivered-app behavior.
- A harvested list of weak spots, missing steps, capability gaps, and architectural defects discovered during the live run.

## Dependency Impact

- The repair phase depends on this phase to produce trustworthy evidence. If the observation is weak, later refactors become speculative and the rerun will not mean much.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Start the serious process run from the provisioned project structure and attach the human approval role where required.
2. Observe process transitions, agent assignments, runtime tool usage, artifact flow, and project-structure updates throughout the delivery.
3. Ensure QA runs use Playwright and screenshot review rather than text-only checks.
4. Verify the delivered units-converter app and record visual and behavioral findings.
5. Write the observed weaknesses into the execution report before changing code.

## Scope Exceptions

- This phase records weaknesses but does not implement the resulting repairs; that belongs to subbundle `05`.

## Do Not Do

- Do not wave away failed agent handoffs or missing artifacts as acceptable because the app happens to compile.
- Do not skip screenshot review for QA if Playwright interaction succeeds.

## Acceptance Checklist

- The serious delivery flow starts and advances through real process steps.
- Agent-generated artifacts are handed off between steps and visible in runtime evidence.
- QA uses Playwright plus screenshot review.
- The delivered units-converter app can be inspected and exercised.
- Weak spots are captured in writing before repair work starts.

## Proof Required

- Process-run logs or events.
- Project-structure evidence for progress and artifact propagation.
- Screenshots of relevant process, workbench, and delivered-app surfaces.
- Execution-report entries describing concrete weak spots or failures.

## Browser Validation Logging

- Target routes: process-run or workbench pages for the serious project, plus the delivered app route
- Required viewports: `1600x900` and `390x844` when responsive layout matters
- Required Playwright MCP actions: navigate through workbench surfaces, exercise the delivered app, capture screenshots, and record assertions about visible behavior
- Expected evidence paths: execution-report entries for process surfaces and delivered-app screenshots
- Screenshot review questions: does the delivered app look intentional and clean, do the process surfaces show believable progress, and are artifacts discoverable from the workbench

## Progression Gate

- Do not start subbundle `05` until the live run has either completed or reached a blocking failure with enough evidence to justify concrete repairs.

## Suggested Agent Prompt

```text
Implement only subbundle 04. Run the serious units-converter delivery path end to end with real CanDoItAll agents, explicit human approvals, Playwright-backed QA, and screenshot review. Record every weak spot, missing artifact handoff, capability gap, and architecture issue in the execution report before any repair work begins.
```
