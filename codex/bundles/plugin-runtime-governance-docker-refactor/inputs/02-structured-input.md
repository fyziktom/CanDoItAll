# Structured Input

## Core Objective

- Prepare an implementation-ready refactor bundle that makes plugin execution grant-aware, auditable, and safe enough for host-tool use cases such as a Docker plugin.

## Success Criteria

- The bundle identifies current weak points in the plugin catalog, abstraction, workflow executor, secret, EF, and host-command surfaces.
- The bundle defines a generic permission model instead of Docker-specific core behavior.
- The bundle requires explicit user grants for file access, host commands, PowerShell, Docker recipes, HTTP/network, storage writes, secrets, and workflow execution.
- The bundle defines a Docker sample plugin and workflow only as a validation scenario for generic plugin contracts.
- The bundle includes validation depth, proof expectations, and progression gates for every subbundle.

## Hard Constraints

- Do not implement production changes during bundle preparation.
- Do not give plugins raw `IServiceProvider`, raw process execution, raw PowerShell, or unrestricted filesystem access.
- Do not treat plugin installation or enablement as permission approval.
- Do not silently fall back when a capability, grant, connection, recipe, or policy is missing.
- Keep plugins generic. Docker-specific logic belongs in a sample plugin or reviewed host-tool recipe implementation, not in the core plugin runtime model.
- Use strongly typed identifiers, enums, options, and records. Avoid magic string command identifiers except for external protocol text and persisted display text.

## Allowed Side Effects

- Create and edit files inside `C:\repositories\CanDoItAll\codex\bundles\plugin-runtime-governance-docker-refactor`.
- No product code changes.

## Source Artifacts

- Source plugin bundle: `C:\repositories\CanDoItAll\codex\bundles\plugin-workflow-executors`
- Current implementation files listed in `inputs/01-source-artifacts.md`
- Existing plugin/workflow/host execution tests listed in `inputs/01-source-artifacts.md`

## Input Coverage Signals

- `N001`: analyze the implementation added from the plugin workflow executors bundle.
- `N002`: find architectural weak points.
- `N003`: use `candoitall-bundle-workflow` discipline to prepare a new bundle.
- `N004`: include .NET performance analysis.
- `N005`: include EF Core query analysis.
- `N006`: use Docker plugin behavior as a concrete pressure test.
- `N007`: include workflow use where Docker logs are summarized by an LLM.
- `N008`: keep plugins generic.
- `N009`: require explicit user control over host tools such as files and PowerShell.
- `N010`: provide proper plugin APIs comparable to workflow and project-structure APIs so plugin state can be controlled during development and validation.
- `N011`: prove the implemented plugin workflow by starting a Qdrant vector database container through the workflow path while Docker is running.

## Dependency And Sequencing Signals

- Permission grants must be designed before workflow plugin execution can be completed.
- Host-tool recipes must be designed before a Docker plugin can be implemented.
- UI/API grant management must exist before real users can authorize Docker or PowerShell access.
- Workflow validation must consume the permission model before Docker log summary workflows are allowed to run.
- Persistence/performance hardening should verify earlier designs before closure, not invent a separate model late.

## Validation Expectations

- Run the bundle validator at prepared stage.
- During later implementation, run targeted unit, integration, component, browser, and workflow tests per subbundle.
- Browser validation is mandatory for plugin settings, permission toggles, and workflow-editor warnings.
- Host-command tests must prove denied-by-default behavior, bounded output, cancellation, timeout, tree-kill, receipt/audit creation, and secret environment exclusion.
- End-to-end validation must exercise the plugin workflow API path to install/enable/grant the Docker plugin, run a workflow that starts Qdrant, read logs, and summarize logs with an LLM-compatible workflow step or deterministic substitute when live LLM credentials are unavailable.

## Evidence Contract

- Prepared bundle validator output.
- Updated execution report rows for each subbundle during implementation.
- Unit tests for typed grant evaluation, capability proxy denial, and Docker recipe argument validation.
- Integration tests for plugin grants, connections, workflow bridge enforcement, and EF persistence.
- Browser screenshots and assertions for plugin settings and workflow editor permission warnings.
- Host execution receipts proving Docker recipe execution is bounded and audited.

## UI Validation Strategy

- Subbundle 04 must validate `/plugins` or the plugin settings route at a maximized large-screen viewport and a narrower viewport.
- Subbundle 05 must validate the workflow editor state that displays plugin executor availability and missing-grant diagnostics.
- Subbundle 06 must validate the sample Docker workflow route or run details route if the workflow execution result is browser-visible.

## Browser Validation Analytics

- Each UI-relevant subbundle must log route, viewport, Playwright MCP actions, DOM assertions, screenshot paths, and visual review outcome in `reviews/01-execution-report.md`.

## Working Assumptions

- The source bundle's SB10 catalog work is accepted as a starting point, but SB11 and workflow bridge work remain incomplete.
- The project keeps Radzen/component-library conventions where already present.
- Plugins remain in-process for this refactor; OS-level sandboxing is outside scope unless a later bundle introduces it.
- Docker CLI availability is optional and must be detected with actionable errors.
- Plugin management APIs must exist before end-to-end workflow validation so tests and local validation can control plugin install, enablement, grants, and sample workflow setup without hand-editing state.

## Primary Risks

- Confusing manifest-declared capabilities with user-granted runtime access.
- Leaking host credentials into plugin-launched processes through inherited environment variables.
- Allowing arbitrary PowerShell or Docker arguments under the label of a plugin capability.
- Storing unbounded Docker logs in EF or workflow payload JSON.
- Creating a Docker-specific core model that blocks future generic plugins.
