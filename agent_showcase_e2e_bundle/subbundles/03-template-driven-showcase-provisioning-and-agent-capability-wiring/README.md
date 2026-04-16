# Template-driven showcase provisioning and agent capability wiring

## Status

- `Completed`

## Objective

- Provision the showcase data and automation surfaces for a Blazor SSR calculator delivery flow using the existing template system and the requested database, including role-to-agent coverage and UI-agent Playwright or screenshot capability wiring.

## Covered Inputs

- `U004`
- Functional requirements `5`, `6`, `7`, and `8`
- Dependency support for functional requirement `9`

## Prerequisites

- Prepared bundle validator pass
- Closed subbundle `01-cross-module-agent-source-alignment`
- Closed subbundle `02-processes-workspace-and-database-profile-ux-fixes`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDevelopmentSeedService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDevelopmentSeedService.RuntimeSeeds.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDevelopmentSeedService.RuntimeSeeds.Complex.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplatePackLoader.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplateProjectionService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRunAutomationDispatchService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.RunStart.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.StepTransitions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\AgentFrameworkCrmHrMetadata.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentCatalogPanel.razor.cs`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\software-delivery\definition.json`
- `C:\repositories\CanDoItAll\Templates\Processes\seed-catalog\baseline-scenarios.json`
- `C:\repositories\CanDoItAll\tools\CanDoItAll.ScenarioSeeder\Program.cs`
- `C:\repositories\CanDoItAll\tools\CanDoItAll.ScenarioSeeder\ScenarioSeederHost.cs`
- `C:\repositories\CanDoItAll\tools\CanDoItAll.ScenarioSeeder\AgentFrameworkIntegrationSimulationSeeder.cs`
- `C:\Users\lucys\AppData\Local\CanDoItAll\control-plane\database-profiles\managed-sqlite\529c12060808489fad29feb5bc60dda1\db\candoitall.db`

## Deliverables

- A repeatable provisioning path that seeds or projects the showcase process definitions and runs from templates against the requested profile root.
- Showcase project structure, delivery blocks, roles, and agent resources for the calculator application.
- UI-oriented agent capability wiring for Playwright and screenshot work without inventing a second capability store.
- Bundle notes capturing any template or capability gaps discovered during provisioning.

## Dependency Impact

- Subbundle `04` depends entirely on this phase. If provisioning is weak, the live run will not be credible because it could be executing hand-built or partially hardcoded data.
- Weak proof here would also make future showcase reruns non-repeatable.

## Validation Depth

- `Process-critical foundation`

## Implementation Steps

1. Map the current hardcoded seeder behavior against the existing process-template projection path and choose the narrowest extension point that enables a template-driven showcase.
2. Define a distinct showcase project and process naming scheme for the requested database so reruns are discoverable and non-destructive.
3. Provision the calculator showcase project structure, process definitions, runs, roles, and agent resources using the template path rather than hardcoded process definitions.
4. Wire UI-facing agents with the required Playwright and screenshot-related capabilities through the existing agent metadata or capability model.
5. Record any missing templates, roles, or capability gaps in the bundle as explicit findings before moving to live execution.

## Scope Exceptions

- This phase does not claim the showcase succeeded. It only provisions the data and capability foundation for the live run.

## Do Not Do

- Do not add showcase-only hardcoded process definitions when templates can be extended or projected.
- Do not overwrite or reset the requested user database.
- Do not wire Playwright or screenshot capability through ad hoc CRM-HR-only flags that runtime cannot consume.

## Acceptance Checklist

- The requested database contains a distinct calculator showcase project and related process definitions or runs.
- Provisioned process definitions are traceable back to the template system, not copied hand-written JSON.
- Required delivery roles are represented and mapped to provisioned agent resources.
- UI-capable agents expose Playwright or screenshot capability information through the same metadata path used by the rest of the agent system.

## Proof Required

- Command or tool proof that provisioning ran against the requested profile root.
- Snapshot or exported evidence of the seeded showcase project, definitions, runs, roles, and agents.
- Planned screenshots:
  - `C:\repositories\CanDoItAll\agent_showcase_e2e_bundle\reviews\evidence\03-showcase-project-structure.png`
  - `C:\repositories\CanDoItAll\agent_showcase_e2e_bundle\reviews\evidence\03-showcase-agent-capabilities.png`
- Bundle notes listing any template, role, or capability gaps discovered before live execution.

## Closure Evidence

- The showcase seeder continues to project from the existing `software-delivery` template path and binds the live showcase to the requested SQLite profile instead of introducing a second showcase-only process registry.
- Provisioned agents, role bindings, project structure, and process definitions were all present in the successful end-to-end run `aff6699b-5c0f-441b-b484-4fadfad41ab1`.
- UI-facing agent capabilities were wired through the seeded capability model, including Playwright-backed browser tooling and screenshot processing.
- Final provisioning fix: `.playwright-mcp/qa-validation` and `.playwright-mcp/execute-release-rollout` scratch folders are now created up front so Playwright browser tools can write the required per-step evidence files.

## Browser Validation Logging

- Target routes: showcase project structure route, process workspace route for the showcase project, and agent detail surfaces as needed
- Required viewport: `1600x900`
- Required browser actions: open the showcase project, confirm process definitions or runs exist, inspect at least one UI-capable agent for tooling capability, and capture screenshots.
- Review questions:
  - Is the showcase clearly provisioned from templates rather than from a one-off manual setup?
  - Are the needed roles and agents visible and coherent?
  - Do UI-related agents visibly carry the expected Playwright or screenshot capability data?

## Progression Gate

- Live execution may continue only when the requested database contains the fully provisioned showcase foundation, the template-driven path is defensible, and any discovered provisioning gaps are explicitly recorded in the bundle.

## Suggested Agent Prompt

```text
Implement subbundle 03 only. Provision the calculator showcase against the requested managed profile database using the existing process-template system and related runtime services, not hardcoded showcase process definitions. Ensure the required roles, agents, and UI-agent Playwright or screenshot capabilities are wired through existing metadata paths. Record any gaps before handing off to live execution.
```
