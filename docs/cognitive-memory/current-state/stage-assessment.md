# Cognitive Memory Stage Assessment

## Decision

The honest stage after the P1 follow-up pass is **P1-complete beta-candidate alpha**.

It is past a prototype and past P0 hardening: the module has durable EF models, migrations for SQLite and PostgreSQL, production-shaped services, legacy plus v1 HTTP API surfaces, contract metadata/examples, a Blazor operator route, source ingestion, consolidation, recall, review approval, projection rebuild, retention cleanup, explicit automation execution, operator audit, probes, self-regulation, MAF context contribution policy, child-tab UI components, and targeted tests. It is still not called beta because live Qdrant/provider validation must be run in a configured environment and broader production workflow browser proof is still needed.

## Stage Matrix

| Area | Current stage | Evidence | What still blocks beta |
| --- | --- | --- | --- |
| Module registration | Done | `AddCognitiveMemoryModule()` is wired from `AddCanDoItAllRuntimeModules()`. | None beyond normal startup tests. |
| Durable schema | Alpha complete | 109 cognitive-memory entity records and 15 SQLite plus 15 PostgreSQL migrations exist. | Schema needs stabilization review before treating it as long-lived external contract. |
| Source ingestion | P1 hardened alpha | Workbench, process, workflow, external file, and web-link ingestion paths exist. External source ingestion has centralized limits, contextual extraction failures, sensitive URL/content rejection, and focused tests. | More source-provider coverage and operational retry/diagnostic UX. |
| Consolidation | Alpha | `CognitiveMemoryConsolidationEngine` creates candidates, mutation commands, review rows, and can materialize memory records through the candidate applicator. | Current fact extraction is deterministic/rule-based; model execution profiles are settings, not a fully wired model-assisted consolidation pipeline. |
| Human review | P1 hardened alpha | Review decisions apply or reject consolidation candidates with concurrency checking. Operator audit now surfaces mutation commands/events, claim state, evidence anchors, projection failures, and retention cleanup runs. | Bulk review ergonomics and broader browser workflows before production use. |
| Recall | Alpha complete | `CognitiveMemoryRecallOrchestrator` persists traces, candidates, context packs, source refs, and warnings. | Vector channel is optional and skipped/unavailable unless projection collection/profile/embedding profile and provider support are present. |
| Projection/RAG | P1 hardened alpha | RAG adapter, projection lifecycle, and `ICognitiveMemoryProjectionRebuildService` exist. `/api/cognitive-memory/projections/rebuild` and `/api/cognitive-memory/v1/projections/rebuild` consume stale/failed projection records and rebuild from durable memory inputs. Rebuild has adapter-backed proof and provider-failure proof that preserves failed/rebuildable state. | Qdrant remains a rebuildable projection only. Live Qdrant/provider validation must still be run before beta. |
| MAF context integration | P0 alpha | `CognitiveMemoryAgentContextContributor` now renders an agent-facing context package and explicitly fails process-critical modes when required memory context is unavailable. | Need broader workflow/A2A integration tests and API contract documentation for agent-safe context. |
| Operator UI | P1 hardened alpha | `/cognitive-memory` and `/memory` render dashboard, probe workbench, settings, sources, memory, review queue, traces, health, self-regulation, and scale tabs. Health now includes `cognitive-memory-operator-audit`. | The route is usable, but beta still needs broader workflow browser coverage and more extraction of parent-owned render fragments. |
| API surface | P1 versioned alpha | The API is split into endpoint groups/DTOs and exposes 35 routes under both `/api/cognitive-memory` and `/api/cognitive-memory/v1`, plus contract metadata/examples under `/contract`. | API is versioned locally; beta still needs live-provider release proof and stable external-client review. |
| Probing and self-regulation | Alpha | Probe sessions, feedback, self-model, calibration, answer gate, professor review, learning proposals, cross-project promotions, and distributed jobs are represented. | Some behaviors are policy/control ledgers rather than mature workflow automation. |
| Automation scheduling | P0 decision closed | `ICognitiveMemoryScheduledAutomationRunner` reads schedule settings and runs enabled ingestion/consolidation through `/api/cognitive-memory/automation/run` and operator settings controls. | No dedicated hosted background scheduler was added by design. Scheduled moments are observable when explicitly triggered; autonomous work needs a scoped future design. |
| Validation | Strong alpha | Current P1 proof includes operational, review UI, settings, and component tests plus final build/browser validation recorded in the bundle report. | Add live Qdrant/provider validation, richer API contract tests, and broader workflow browser proof before beta. |
| Maintainability | P1 improved | Advanced services, recall orchestration, recall channels/internal types, Minimal API mapping, DTOs, review UI queries/previews/audit queries, page code-behind, rendering helpers, ten Razor tab components, and external source policy helper files are split by responsibility. | Older broad services such as consolidation, procedure, temporal replay, ingestion, workspace, and signal services remain hardening targets. |

## What Is Actually Done

- The module is in the solution and loaded by the runtime composition root.
- EF configuration discovery includes Cognitive Memory module assemblies through `AppDbContextModelRegistry`.
- Durable memory state is not Qdrant. It lives in the active `AppDbContext` profile.
- Source truth is preserved through source manifests, source items, source links, evidence anchors, and claim/evidence links.
- Review-approved consolidation can create canonical memory records and claims.
- Recall persists detailed trace state and renders context packs with included/excluded source refs.
- The Blazor page uses the project component library wrappers such as `PageScaffold`, `PageHeader`, `Tabs`, `SurfaceCard`, `Button`, and `StatusBadge`, with focused child tab components under `Pages/Components`.
- API access is hosted by the web app under legacy `/api/cognitive-memory` and additive `/api/cognitive-memory/v1` routes.
- Agent model access policy exists and can limit Cognitive Memory context contribution by provider profile or local/remote policy.
- Stale and failed projection records can be rebuilt explicitly through a service/API/UI path from durable memory records, claims, evidence anchors, source links, entities, and context-boundary policy metadata.
- Automation settings now have an explicit runner/API/UI path that honors schedule mode and produces ingestion/consolidation summary output.
- Retention cleanup has an explicit dry-run-first service/API path for operational traces, candidates, probe sessions, and distributed jobs, and it records a durable run row for audit visibility.
- Operator audit signals are included in the review UI snapshot and rendered on the health tab.
- External source ingestion rejects likely credentials and sensitive URL query parameters before source/evidence records are persisted.
- Process-critical MAF execution modes now fail predictably when memory context is required but unavailable.

## What Is Not Yet True

- Cognitive Memory is not an autonomous memory daemon. Scheduled settings and an explicit runner exist, but a dedicated hosted scheduler/worker is intentionally absent until the product defines unattended scope, retry, idempotency, and audit behavior.
- Qdrant is not authoritative memory. It is an optional projection target behind `IRagDriver`.
- Model execution profiles do not by themselves mean every cognitive-memory task calls a chat model. Current consolidation fact extraction is rule-based and source-backed.
- Vector recall is not guaranteed. It requires projection collection/profile/embedding profile inputs and a configured provider with typed filters.
- Cross-project and distributed compute are represented, but they should be treated as alpha control surfaces until exercised by product workflows.
- The API is useful for local agents and now has v1 aliases plus a contract endpoint. It still needs external-client compatibility review before public beta.
- P0 closed the child-tab split, but some parent-owned render fragments remain and older broad services still need incremental decomposition.

## Senior Risks

- **UI size risk:** the page is split into child tabs, but the parent still owns several render fragments. Broader browser-facing workflows need proof before beta.
- **Projection provider risk:** rebuild orchestration, adapter-backed proof, deterministic failure proof, and audit visibility exist, but live Qdrant/provider validation still needs beta-grade execution evidence.
- **Automation truth risk:** schedule settings are executable through explicit API/UI runners, and the no-hosted-worker decision is closed for P0. Future autonomous scheduling must be designed explicitly.
- **Provider semantics risk:** recall can still degrade when vector/semantic providers are unavailable. Process-critical MAF context is stricter now, but broader workflow callers need the same clarity.
- **API contract risk:** v1 aliases and contract examples exist; the residual risk is external-client compatibility and live release proof.
- **Diagnostic payload risk:** recall returns rich trace/candidate data. Agent-facing context now has a separate package, but future endpoints must preserve that separation.

## Validation Evidence

The current source tree contains:

- 17 unit-test files for Cognitive Memory.
- 12 integration-test files for Cognitive Memory persistence and behavior.
- 1 component test file for the Blazor page.
- 1 Playwright file for Cognitive Memory review UI.
- 2 support/fake files for Cognitive Memory tests.

Current P1 validation evidence is recorded in `codex/bundles/cognitive-memory-p1-beta-hardening/reviews/01-execution-report.md`.

Current P1 validation evidence records:

- External-source/settings unit focus: 10/10 passed.
- Unit Cognitive Memory and agent-context tests: 142/142 passed.
- Integration Cognitive Memory tests: 25/25 passed.
- Component Cognitive Memory tests: 1/1 passed.
- Web project build: passed with 0 warnings and 0 errors.
- Browser proof: `/cognitive-memory` health tab at 1440x1000 and 390x900 with operator audit rendered.

Current P0 validation evidence records:

- Unit Cognitive Memory and agent-context tests: 136/136 passed.
- Integration Cognitive Memory tests: 25/25 passed.
- Component Cognitive Memory tests: 1/1 passed.
- Web project build: passed with 0 warnings and 0 errors.
- Browser proof: `/cognitive-memory` settings tab on `http://127.0.0.1:5289`, 1440x1000 and 390x900 viewports, operational controls present, no narrow horizontal overflow, no console errors beyond normal Blazor connection logs.

Use [validation and testing](../operations/validation-and-testing.md) for the current commands.

