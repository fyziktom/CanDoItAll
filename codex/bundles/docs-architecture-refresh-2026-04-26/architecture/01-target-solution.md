# Target Solution

## Target Documentation Architecture

The repaired docs will use this structure:

- `README.md`: concise product overview, high-level architecture diagram, run/test commands, data/configuration notes, and links to detailed docs.
- `docs/README.md`: docs landing page that routes contributors to architecture, MCP, component, prompt, and governance docs.
- `docs/architecture-beta.md`: source-grounded detailed architecture with Mermaid `architecture-beta`, C4, and sequence diagrams.
- `architecture/README.md`: architecture landing page that separates current architecture docs, ADRs, and historical reviews.
- `docs/ui-shared-components/*`: repaired shared-component docs that describe the current split BaseLib/CanvasLib/OverlayLib/WebGlLib/facade/sandbox shape.
- `src/**/README.md`, `tests/**/README.md`, `tools/**/README.md`: concise project-level orientation files.

## Boundary Model

- `CanDoItAll.Web` remains the only Blazor web host.
- `CanDoItAll.Composition` remains the runtime composition root for modules and database bootstrapping.
- `CanDoItAll.Infrastructure` owns database profiles, runtime database switching, storage, search, readiness, health, control-plane secrets, and managed files.
- `CanDoItAll.Modules.*` own feature surfaces and application services.
- `CanDoItAll.Modules.Processes` owns process definition, template pack import/projection, canvas projection, process runtime, outbox/recovery, and process-run automation dispatch.
- `CanDoItAll.Modules.AgentFramework` bridges CRM/HR AI party records to technical AgentFramework agents and wires organization-scoped workspace execution.
- `CanDoItAll.AgentFramework.*` owns agent catalog models, file-backed organization workspaces, Microsoft Agent Framework runtime integration, workspace tools, MCP capabilities, and execution records.
- `CanDoItAll.Components.*` owns shared Blazor UI primitives, canvas libraries, overlay/WebGL experiments, and compatibility facade/sandbox surfaces.
- `CanDoItAll.Mcp.*` projects expose local or remote agent-facing control surfaces without duplicating canonical module behavior.

## Process AI-Agent Flow To Document

1. A published process definition or launch plan starts a `ProcessRun`.
2. Run start materializes assignments, step runs, briefs, artifact expectations, dependency state, and initial outbox work.
3. Transition logic validates step movement, completion artifacts, branch outcome choices, and dependency progression.
4. The outbox/recovery workers dispatch ready process steps when background workers are enabled.
5. Dispatch resolves the current executor party through `IAiTechnicalAgentBridge` to an AgentFramework technical agent.
6. Dispatch builds a prompt with work brief, upstream artifacts, branch outcomes, live project-structure grounding, governed tool/evidence rules, and recovery directives.
7. AgentFramework creates or reuses a chat session, executes the agent through the Microsoft Agent Framework runtime, attaches permitted tools/MCPs/skills, persists execution state, and auto-continues approved tool calls when the process run allows it.
8. Dispatch audits tool/evidence use, retries recoverable gaps, applies provider fallback when possible, projects execution artifacts into managed storage, records process artifacts, and transitions the step.
9. Step transition updates the run, unlocks dependent steps, records decisions/journals/conformance observations, syncs project structure, and enqueues further automation.

## Validation Strategy

- Run bundle prepared validator before implementing docs.
- Confirm Mermaid block presence with text search.
- Run project README coverage script.
- Run `git diff --check`.
- Optionally run a docs-only build if there is a Markdown linter in repo; otherwise record that none is configured.
