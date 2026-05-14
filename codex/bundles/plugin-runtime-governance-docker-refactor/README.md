# Plugin Runtime Governance And Docker Refactor

This bundle is a preparation-only architecture and execution package for hardening the plugin runtime before plugins are allowed to execute host tools such as PowerShell or Docker.

## Profile

- `initiative`

## Mission

Design the smallest coherent refactor that turns the current plugin catalog module into a grant-aware, workflow-safe plugin runtime while keeping plugins generic. The Docker plugin use case is the concrete pressure test: a plugin must be able to list Docker containers, pull/start a container, retrieve bounded logs, and feed those logs into a workflow LLM summary step without receiving unrestricted shell, filesystem, or credential access.

## Outcome Contract

- Requested outcome: analyze the implementation added from `C:\repositories\CanDoItAll\codex\bundles\plugin-workflow-executors`, identify weak points, and prepare a new detailed bundle for architecture refactoring.
- Hard constraints: do not implement product code in this bundle-preparation pass; preserve generic plugin architecture; require explicit user grants for files, PowerShell/host commands, Docker, HTTP/network, storage, and secrets; avoid silent fallback behavior.
- Evidence required before closure: prepared bundle validates with `validate_bundle.py --profile initiative --stage prepared`; every raw input is mapped to a requirement and subbundle; UI-relevant subbundles define browser evidence; performance and EF risks are explicitly covered.
- Known blockers or explicit scope exceptions: implementation is intentionally deferred to subbundle execution; this bundle assumes the existing plugin-wave code can be refactored instead of reverted.

## Key Findings

- The current module installs, enables, disables, and lists plugin manifests, but installation state is not a permission grant.
- `IPluginCapabilityContext` exposes capabilities as direct properties without a visible grant evaluator or denied-capability proxy contract.
- There is no strongly typed host-tool capability for PowerShell, Docker, or reviewed command recipes.
- The existing workspace command host is policy-shaped, not a sandbox. `LocalWorkspaceProcessHost` reports `PolicyOnlyLocal` and `IsEnforcedByHost: false`.
- The command environment policy currently allows `OPENAI_API_KEY` and `OPENAI_` variables, which is inappropriate for plugin-launched host commands unless explicitly scoped.
- Plugin secret types are duplicated between `CanDoItAll.Plugins.Abstractions` and `CanDoItAll.Modules.Security`, creating drift risk.
- EF usage in the current catalog path is acceptable for the small bundled catalog, but future grant, connection, and workflow-runtime checks need projections, indexes, paging, and no large log payloads in EF.

## Bundle Layout

- `inputs/` raw request, source artifacts, and structured input
- `analysis/` current state, assumptions, risks, weak points, performance, and EF review
- `requirements/` normalized, testable requirements and input coverage
- `architecture/` target solution and permission/host-tool contracts
- `inventories/` scoped implementation inventory and weak-point inventory
- `plan/` execution order, dependencies, critical foundations, and phase gates
- `traceability/` requirement-to-subbundle mapping
- `shared-prompts/` reusable implementation, QA, and architecture review prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report template

## Recommended Execution Order

1. `subbundles/01-01-current-implementation-audit-and-docker-use-case-gate`
2. `subbundles/02-02-plugin-permission-grants-and-policy-model`
3. `subbundles/03-03-controlled-host-tool-and-command-capability`
4. `subbundles/04-04-plugin-settings-connections-and-permission-ui`
5. `subbundles/05-05-workflow-plugin-bridge-permission-enforcement`
6. `subbundles/06-06-docker-bundled-plugin-and-log-summary-workflow`
7. `subbundles/07-07-persistence-performance-observability-hardening`
8. `subbundles/08-08-validation-architecture-review-and-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Ready`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Completed after targeted build, integration tests, live Docker Qdrant workflow proof, UI screenshots, and completed-stage bundle validation`
- Browser validation analytics: `Captured for /plugins denied-by-default and granted states; workflow proof validated through API/integration path`
