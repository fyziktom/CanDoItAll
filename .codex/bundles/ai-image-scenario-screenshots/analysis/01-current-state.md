# Current State

## Repo Shape

- The solution is `C:\repositories\CanDoItAll\CanDoItAll.slnx`.
- The relevant agent model and seed code lives in:
  - `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence`
- The relevant process template pack lives in `C:\repositories\CanDoItAll\Templates\Processes`.
- The project-structure HTTP API is in `C:\repositories\CanDoItAll\src\CanDoItAll.Web\ProjectStructureAgentApi.cs`.
- The process and agent HTTP APIs are in `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\ProcessesApi.cs` and `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\AgentsApi.cs`.

## Provider And Agent State

- Provider profiles currently model chat/text provider behavior in `ProviderProfile`, with `ProviderKind` values `OpenAi`, `AzureOpenAi`, and `Ollama`.
- The seed catalog creates OpenAI Responses, OpenAI Chat Completions, and Ollama provider profiles.
- Agent definitions already carry `ProviderProfileId`, model, workload, permissions, capabilities, tags, and `ConfigurationJson`.
- Access settings already exist for project structure, processes, and workspace tools. Image-generation preference should follow that metadata pattern rather than adding untyped strings inside prompts only.
- The current Playwright local MCP capability is already seeded and assigned to UI-capable delivery agents.

## Process Template State

- Process templates are data-pack driven through `Templates\Processes\manifest.json`.
- Template definitions use role usages, typed steps, artifact expectations, prompt references, validations, Mermaid diagrams, markdown docs, and import projections.
- This is the right extension point for screenshot and layout-generation workflows. The generic process core should consume definitions and run steps exactly as it does for existing templates.

## Project Structure State

- Project-structure routes support project creation, node creation/editing, metadata, process definition linking, process start, asset creation, asset content readback, asset revisions, leases, analytics, and knowledge queries.
- Existing observations from the prior Dev55 bundle mention a metadata read-projection gap. Execution must verify actual readback for asset nodes and not rely only on successful write responses.

## External Scenario State

- `C:\programovani\candoitall-dev-55-output\run-manifest.json` lists three scenarios.
- Scenario 01 is a Razor Pages snack-box inventory app with route `/inventory`.
- Scenario 02 is a Blazor calibration-log app with routes `/`, `/calibrations`, `/calibrations/new`, and `/calibrations/{RecordId}`.
- Scenario 03 is a Vite app with route `/`.
- The `business-analysis` folder has a `.keep` sentinel and must remain durable.
