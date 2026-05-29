# CanDoItAll Workflow MAF Hardening Bundle

This bundle is a coordination and execution package for hardening the CanDoItAll Agents/Workflows implementation after the Microsoft Agent Framework (MAF) update.

## Profile

- `architecture-hardening`
- `maf-workflow-upgrade`
- `plugin-executor-hardening`
- `agents-workflows-refactor`

## Mission

Audit and harden the workflow implementation under the Agents module so it uses the newer MAF workflow capabilities instead of only storing repository-local workflow graphs. The work must also harden plugin-provided executors because plugin executors are now part of the workflow runtime surface.

## Outcome Contract

- Requested outcome: a reliable workflow runtime path where repository-owned workflow definitions/templates are validated, compiled or adapted into native MAF workflows, executed through typed executors, and observed through durable event/artifact records.
- Hard constraints:
  - Work on branch `processes-hardening` unless the operator explicitly says otherwise.
  - Preserve user-managed workflow definitions; do not overwrite definitions that do not contain the managed seed marker.
  - Preserve repository-owned template pack loading through `Templates/Workflows/manifest.yaml` and external YAML workflow files.
  - Do not add hard-coded C# workflow example graphs as a replacement for template files.
  - Do not introduce a second independent workflow engine that competes with MAF. The CanDoItAll domain model may remain canonical for persistence/UI, but execution must have a clear native-MAF adapter/compiler boundary.
  - Keep plugin executor behavior deterministic, cancellable, permission-checked, and testable without live external services.
- Evidence required before closure:
  - Repo-local inventory of current workflow/agent/plugin runtime code.
  - MAF package/version baseline and upgrade decision.
  - Targeted unit/integration tests for template loading, graph validation, route semantics, MAF compilation/adaptation, executor registry, plugin executors, event/artifact capture, and approval handling.
  - Build/test transcripts and, where UI is touched, browser/Playwright proof.
- Known blockers or explicit scope exceptions:
  - Live Gmail/Office365/Docker execution may require secrets and local service availability. Use fakes/stubs for deterministic proof and record live-service validation as optional/manual.
  - If .NET 10 SDK or preview package feeds are unavailable in the environment, record the blocker and still complete the static analysis/refactor plan honestly.

## Bundle Layout

- `inputs/` raw request, source artifacts, and structured input
- `analysis/` current state, MAF delta findings, assumptions, and risks
- `requirements/` normalized testable requirements
- `inventories/` scope inventory and repo-local audit checklist
- `architecture/` target solution and runtime boundaries
- `plan/` execution order, dependency gates, and rollback points
- `traceability/` requirement-to-subbundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` self-review and execution report template
- `references/` source notes used when preparing this bundle
- `templates/` reusable subbundle template

## Recommended Execution Order

1. `subbundles/01-repo-local-inventory-and-maf-version-baseline`
2. `subbundles/02-workflow-domain-model-and-template-loader-hardening`
3. `subbundles/03-maf-workflow-compiler-and-executor-foundation`
4. `subbundles/04-plugin-executor-contract-and-sandbox-hardening`
5. `subbundles/05-runtime-events-state-and-checkpoint-alignment`
6. `subbundles/06-agent-workflow-ui-seeding-and-compatibility-migration`
7. `subbundles/07-tests-observability-and-final-hardening-review`

## Dependency And Validation Map

- SB01 is a mandatory gate. No implementation subbundle may proceed before the local source inventory and MAF package baseline are complete.
- SB02 must pass before SB03 because native MAF compilation needs a validated canonical graph.
- SB03 must pass before SB04 because plugin executors need a stable executor adapter contract.
- SB04 and SB05 may be implemented in parallel only if the executor contracts and event schema are frozen in writing.
- SB06 must not migrate UI or seed data before SB02/SB03/SB04 have stable DTOs.
- SB07 is the closure gate and must include build/test/browser evidence plus an architecture review of all changes.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed`
- Subbundle gate review: `SB01-SB07 completed`
- Final closure gate: `Passed`
- Browser validation analytics: `Passed; no workflow/agent UI files changed`
