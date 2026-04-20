# Units Converter Project And Process Provisioning

## Status

- `Ready`

## Objective

- Create a serious Blazor SSR basic-units-converter project in the requested profile, with feature blocks, delivery phases, template-driven process attachments, role assignments, and AgentFramework-owned serious-delivery agents ready to execute the work.

## Covered Inputs

- `N004`
- `N005`
- `N006`
- `N008`

## Prerequisites

- `subbundles/01-canonical-agentframework-ownership-and-crm-hr-projection` closed with proof
- `subbundles/02-openai-agent-capability-and-process-template-hardening` closed with proof

## Exact Source References

- `C:\repositories\CanDoItAll\tools\CanDoItAll.ScenarioSeeder\AgentShowcaseCalculatorSeeder.cs`
- `C:\repositories\CanDoItAll\tools\CanDoItAll.ScenarioSeeder\AgentShowcaseCalculatorSeeder.Workflow.cs`
- `C:\repositories\CanDoItAll\Templates\Processes`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureAssemblyService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructureNodeDetailFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureLocalFileOpener.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchServiceIntegrationTests.cs`

## Deliverables

- A serious project record and project structure for the units-converter app in the target SQLite profile.
- Feature blocks, phase blocks, and delivery descriptions without showcase naming.
- Template-driven process and role attachments covering intake, architecture, implementation, review, UI review, QA, release, and learning.
- Project-structure visibility for durable output folders or equivalent runtime artifact nodes.

## Dependency Impact

- The live run cannot be trusted if the project structure, role mapping, or process attachments are wrong here. Downstream observation depends on this phase being correct and inspectable.

## Validation Depth

- `Critical UI foundation`
- `Process-critical closure`

## Implementation Steps

1. Refactor or extend the current scenario or provisioning path so it creates a serious units-converter project instead of a showcase-branded artifact.
2. Use reusable process templates and role assignments instead of hardcoded one-off flow composition where the template system already covers the need.
3. Seed or provision the required AgentFramework-owned delivery agents into the target profile.
4. Ensure project-structure nodes expose progress and durable output paths for the resulting delivery work.
5. Browser-verify the created project, project structure, and process surfaces before executing the live run.

## Scope Exceptions

- This phase provisions the serious project and its delivery plan but does not yet claim the end-to-end run has passed.

## Do Not Do

- Do not name the project or its agents as a showcase.
- Do not skip project-structure visibility just because artifacts also exist on disk.

## Acceptance Checklist

- A serious units-converter project exists in the target profile.
- The project structure shows feature or delivery blocks, phases, and process attachments.
- The process plan includes roles for architecture, implementation, review, UI review, QA, release, and learning where appropriate.
- Artifact output visibility is wired into project structure or equivalent inspectable workbench surfaces.

## Proof Required

- Provisioning command output or runtime logs.
- Project-structure inspection proof from service or UI.
- Screenshots of the created project structure and process surfaces.

## Browser Validation Logging

- Target routes: `/projects`, `/project-structure`, `/processes`
- Required viewports: `1600x900` primary and `1280x900` secondary
- Required Playwright MCP actions: navigate to the created project, open its project structure, inspect process attachments, capture screenshots
- Expected evidence paths: execution-report entries for project list, project structure, and process surface screenshots
- Screenshot review questions: does the project read like a serious delivery effort, are the phases and features understandable, and are durable artifacts or output-folder nodes inspectable

## Progression Gate

- Do not start subbundle `04` until the serious units-converter project, process attachments, and artifact-visibility surfaces exist in the target profile and are proven through browser or service inspection.

## Suggested Agent Prompt

```text
Implement only subbundle 03. Provision a serious Blazor SSR basic-units-converter project into the requested profile with feature blocks, phases, template-driven processes, role assignments, AgentFramework-owned delivery agents, and project-structure visibility for durable outputs. Prove the created project and its process surfaces before closing the phase.
```
