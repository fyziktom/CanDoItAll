# Plugin Workflow Executors Architecture Bundle

This bundle is a coordination and execution package for `plugin-workflow-executors-architecture`.

## Profile

- `initiative`
- `architecture-first`
- `pre-implementation-readiness-gated`

## Mission

Design the plugin architecture for CanDoItAll so bundled plugins can later expose workflow executors, plugin-specific settings UI, safe access to existing services, and a path toward a public plugin shop without duplicating settings, secret, storage, or workflow code.

## Readiness Decision

- CanDoItAll is **not ready for arbitrary runtime/shop-installed plugins yet**.
- CanDoItAll is **ready to start prerequisite hardening** because workflow executor contracts, improved vault storage, workspace file services, and connector configuration schema already provide strong foundations.
- The plugin module must not start until the foundation review gate passes:
  - executor descriptors/provenance/settings validation,
  - canonical settings schema and renderer host,
  - consumer-bound secret authorization,
  - plugin-safe workspace/storage/project-structure facades,
  - execution policy/audit/sanitization.
- The MVP should support **bundled/static plugins first**. Remote shop catalog and package contracts can be designed before dynamic code loading, but arbitrary unsigned assembly loading remains out of scope until trust/signature/isolation is reviewed.

## Outcome Contract

- A Codex-ready roadmap with subbundles, source references, requirements, acceptance gates, proof requirements, and architecture reviews.
- A target plugin architecture that supports:
  - separate `CanDoItAll.Plugins.Abstractions` and `CanDoItAll.Modules.Plugins`,
  - plugin manifests, catalog, installation state, settings, connections, health checks, and workflow executor exposure,
  - schema-driven settings fallback plus optional bundled Razor renderer components,
  - safe plugin access to secrets, workspace files, storage, project structure, HTTP, and future OAuth2 through capability-gated facades,
  - remote shop/package metadata without premature dynamic code loading.
- A spreadsheet checklist artifact at `artifacts/plugin-workflow-executor-roadmap.xlsx`.

## Hard Constraints

- Do not persist raw secrets in plugin settings, workflow JSON, logs, screenshots, activity text, or package manifests.
- Do not pass arbitrary `IServiceProvider` to plugins.
- Do not copy hard-coded executor settings UI branches for each plugin.
- Do not create a second settings schema system when connector settings already provide a usable foundation.
- Do not expose raw storage drivers or concrete Workbench services as the default plugin API.
- Do not enable remote arbitrary code loading before a signed package and isolation review.
- Keep current built-in workflow executors and saved workflows compatible.

## Bundle Layout

- `inputs/` original request, source artifact summary, structured input
- `analysis/` current-state findings, readiness decision, gaps, prerequisite refactors
- `requirements/` normalized, testable requirements
- `architecture/` target architecture, interfaces, settings renderer/catalog, shop/trust, OAuth2 extension point
- `inventories/` source map, service capability map, risk register
- `plan/` phase plan, dependency map, review gates
- `traceability/` requirement-to-subbundle and source-to-subbundle mapping
- `shared-prompts/` reusable Codex prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report template
- `templates/` subbundle template aligned with existing repository bundle style
- `artifacts/` spreadsheet checklist

## Recommended Execution Order

1. `subbundles/01-01-plugin-readiness-source-audit-and-decision-gate`
2. `subbundles/02-02-workflow-executor-contract-hardening`
3. `subbundles/03-03-settings-schema-canonicalization-and-validator`
4. `subbundles/04-04-settings-renderer-registry-and-schema-fallback`
5. `subbundles/05-05-secret-runtime-authorization-and-plugin-secret-broker`
6. `subbundles/06-06-workspace-file-storage-project-facades`
7. `subbundles/07-07-policy-observability-and-sanitization`
8. `subbundles/08-08-architecture-review-gate-foundations`
9. `subbundles/09-09-plugins-abstractions-project-and-manifest`
10. `subbundles/10-10-plugins-module-catalog-and-persistence`
11. `subbundles/11-11-plugin-settings-page-and-connection-model`
12. `subbundles/12-12-workflow-plugin-executor-bridge`
13. `subbundles/13-13-sample-bundled-plugin`
14. `subbundles/14-14-architecture-review-gate-plugin-mvp`
15. `subbundles/15-15-plugin-shop-and-package-contracts`
16. `subbundles/16-16-oauth2-extension-point-and-connection-broker`
17. `subbundles/17-17-tests-api-and-browser-proof`
18. `subbundles/18-18-final-architecture-review-and-closure`

## Mandatory Architecture Reviews

- `SB08` after foundation hardening subbundles `SB01`-`SB07`.
- `SB14` after plugin MVP subbundles `SB09`-`SB13`.
- `SB18` after shop/OAuth/test closure subbundles `SB15`-`SB17`.
- Codex must update `reviews/01-execution-report.md` and the spreadsheet status/checklist before each review gate.
- If a review gate fails, stop implementation and repair the failing foundation before continuing.

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, `artifacts/plugin-workflow-executor-roadmap.xlsx`, and `reviews/01-execution-report.md` as durable state.
- Do not rely on memory from previous Codex sessions.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed`
- Execution status: `In progress`
- Subbundle gate review: `SB01-SB10 passed; SB11 pending`
- Final closure gate: `Pending`
- Browser validation analytics: `SB04 workflow settings fallback proof captured; SB10 plugin catalog route proof captured`
