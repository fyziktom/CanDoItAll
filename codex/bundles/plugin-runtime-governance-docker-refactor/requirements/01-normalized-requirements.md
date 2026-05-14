# Normalized Requirements

## Runtime Permission Model

| Id | Requirement | Success criteria |
| --- | --- | --- |
| `R001` | Plugin installation and enablement must remain separate from runtime permission grants. | A plugin can be installed and enabled while file, host-command, Docker, HTTP, storage, secret, and workflow grants remain denied. |
| `R002` | Manifest capabilities must declare what a plugin may request, not what it is allowed to use. | Grant evaluation requires both manifest declaration and persisted user approval. |
| `R003` | Runtime grants must be strongly typed. | Grants use typed ids/enums/records for plugin id, connection id, capability kind, host recipe id, scope, approval state, actor, and timestamps. |
| `R004` | Capability access must fail predictably when not declared or not granted. | Denied capability proxies return typed denial results or throw explicit domain exceptions with actionable messages; no silent fallback. |
| `R005` | Grant changes must be audited with trusted identity. | API/application services derive actor from authenticated user or trusted system context, not request body strings. |

## Host Tool And Docker Capability

| Id | Requirement | Success criteria |
| --- | --- | --- |
| `R006` | Plugins must not receive raw shell, raw PowerShell, raw `IWorkspaceCommandExecutionService`, or raw `IServiceProvider`. | Architecture guardrails and tests reject direct exposure. |
| `R007` | Add a generic host-tool capability based on reviewed recipes and typed arguments. | Plugins request recipe execution through a narrow interface that validates recipe id, arguments, policy, grants, and output caps. |
| `R008` | PowerShell access must be explicitly granted and recipe-scoped. | A plugin cannot run PowerShell unless both host-tool and PowerShell recipe grants exist. |
| `R009` | Docker access must be explicitly granted and recipe-scoped. | Docker list, pull, start, and logs recipes each have typed request models and can be granted independently. |
| `R010` | Docker recipe implementation must deny dangerous defaults. | Privileged mode, host network, arbitrary volume mounts, uncontrolled registries, unbounded logs, and unrestricted args are denied unless a future explicit policy adds them. |
| `R011` | Plugin host-command environments must exclude unrelated secrets by default. | `OPENAI_API_KEY`, broad `OPENAI_` variables, and unrelated credentials are not passed to plugin host tools unless explicitly scoped through a plugin connection/secret grant. |

## Workflow Integration

| Id | Requirement | Success criteria |
| --- | --- | --- |
| `R012` | Workflow plugin executor descriptors must only be runnable when the plugin is installed, enabled, compatible, and grant-valid. | Workflow catalog and runtime produce unavailable diagnostics rather than runtime surprises. |
| `R013` | Workflow validation must detect missing plugin grants and connections before execution. | Invalid workflow nodes show actionable messages in validation and editor UI. |
| `R014` | Docker log summary workflows must use a normal LLM workflow step after log retrieval. | The Docker plugin returns bounded log text or an artifact reference; a separate LLM node performs summarization. |
| `R015` | Workflow output must remain bounded before and after plugin execution. | Host command stdout/stderr, plugin result payloads, and LLM inputs have limits and truncation metadata. |

## Settings, Connections, UI, And API

| Id | Requirement | Success criteria |
| --- | --- | --- |
| `R016` | Plugin settings UI must let users inspect manifest capabilities and manage explicit grants. | UI shows requested vs granted capabilities, risk labels, and disabled controls for unavailable capabilities. |
| `R017` | Plugin connections must persist settings separately from grants and secrets. | Connections have typed ids, schema validation, health state, secret bindings, concurrency token, and audit fields. |
| `R018` | Plugin API must expose minimal command-style operations for grants and connections. | Endpoints validate ids, derive actor identity, enforce antiforgery/auth policy per app conventions, and return typed errors. |
| `R019` | UI validation must include plugin settings and workflow-editor missing-grant paths. | Execution report contains route, viewport, assertions, screenshots, and visual review notes. |
| `R025` | Plugin API must be complete enough for development and validation automation, comparable in practical control to workflow and project-structure APIs. | API clients can list plugin catalog, install, enable/disable, inspect settings, list/update grants, list/update connections, and trigger sample setup without direct database edits. |

## Persistence, Performance, EF, And Observability

| Id | Requirement | Success criteria |
| --- | --- | --- |
| `R020` | Grant and connection persistence must be queryable without N+1 behavior. | Read APIs use projections, `AsNoTracking`, stable ordering, paging where list size can grow, and proper indexes. |
| `R021` | Docker logs and command output must not be stored as large EF JSON/text payloads. | EF stores metadata, artifact references, summaries, and bounded previews only. |
| `R022` | Hot-path grant checks must be designed for workflow execution. | Grant state is loaded with bounded queries or cached per workflow run; compiled queries are considered only if measurements justify them. |
| `R023` | Observability must capture grants and host-tool execution without leaking secrets. | Audit records include plugin id, connection id, workflow/node ids, recipe id, grant ids, policy, boundary, truncation, and redacted messages. |
| `R024` | Final validation must include architecture review, targeted tests, browser proof, and performance/EF review. | Closure report lists commands, screenshots, assertions, and residual risks. |
| `R026` | End-to-end proof must start a Qdrant vector database container through a plugin workflow. | Validation creates or reuses a workflow that invokes the Docker plugin start-container executor for Qdrant, then reads logs and routes them to an LLM-summary-compatible workflow step; failures are repaired before closure. |
