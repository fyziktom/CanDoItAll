# Cognitive Memory Stage Assessment

## Decision

The honest stage after the P0 continuation pass is **P0-complete validation-grade alpha**.

It is past a prototype: the module has durable EF models, migrations for SQLite and PostgreSQL, production-shaped services, an endpoint-grouped HTTP API, a Blazor operator route, source ingestion, consolidation, recall, review approval, projection rebuild, explicit automation execution, probes, self-regulation, MAF context contribution policy, child-tab UI components, and targeted tests. It is still not beta because the API is not versioned, background scheduling is intentionally explicit/UI/API-triggered rather than hosted, live provider failure modes need runbooks, and older broad service files still need beta hardening.

## Stage Matrix

| Area | Current stage | Evidence | What still blocks beta |
| --- | --- | --- | --- |
| Module registration | Done | `AddCognitiveMemoryModule()` is wired from `AddCanDoItAllRuntimeModules()`. | None beyond normal startup tests. |
| Durable schema | Alpha complete | 109 cognitive-memory entity records and 15 SQLite plus 15 PostgreSQL migrations exist. | Schema needs stabilization review before treating it as long-lived external contract. |
| Source ingestion | Alpha complete | Workbench, process, workflow, external file, and web-link ingestion paths exist. | More source-provider coverage and operational retry/diagnostic UX. |
| Consolidation | Alpha | `CognitiveMemoryConsolidationEngine` creates candidates, mutation commands, review rows, and can materialize memory records through the candidate applicator. | Current fact extraction is deterministic/rule-based; model execution profiles are settings, not a fully wired model-assisted consolidation pipeline. |
| Human review | Alpha complete | Review decisions apply or reject consolidation candidates with concurrency checking. | Need audit UX and bulk review ergonomics before production use. |
| Recall | Alpha complete | `CognitiveMemoryRecallOrchestrator` persists traces, candidates, context packs, source refs, and warnings. | Vector channel is optional and skipped/unavailable unless projection collection/profile/embedding profile and provider support are present. |
| Projection/RAG | P0 closed alpha | RAG adapter, projection lifecycle, and `ICognitiveMemoryProjectionRebuildService` exist. `/api/cognitive-memory/projections/rebuild` consumes stale/failed projection records and rebuilds from durable memory inputs. Rebuild now reconstructs entity/boundary payload metadata and has adapter-backed proof through `RagCognitiveMemoryProjectionAdapter`. | Qdrant remains a rebuildable projection only. Live Qdrant/provider failure validation and operational failure UI are still needed before beta. |
| MAF context integration | P0 alpha | `CognitiveMemoryAgentContextContributor` now renders an agent-facing context package and explicitly fails process-critical modes when required memory context is unavailable. | Need broader workflow/A2A integration tests and API contract documentation for agent-safe context. |
| Operator UI | P0 closed alpha | `/cognitive-memory` and `/memory` render dashboard, probe workbench, settings, sources, memory, review queue, traces, health, self-regulation, and scale tabs. The tab bodies are now ten child components, settings includes operation controls, and browser proof covered desktop plus 390px narrow viewport. | The route is usable, but beta still needs broader workflow browser coverage and more extraction of parent-owned render fragments. |
| API surface | P0 alpha | The API is split into endpoint groups and DTO files and exposes 33 endpoints under `/api/cognitive-memory`. | Public contract needs versioning, stricter OpenAPI examples, and explicit agent-output DTO contract documentation. |
| Probing and self-regulation | Alpha | Probe sessions, feedback, self-model, calibration, answer gate, professor review, learning proposals, cross-project promotions, and distributed jobs are represented. | Some behaviors are policy/control ledgers rather than mature workflow automation. |
| Automation scheduling | P0 decision closed | `ICognitiveMemoryScheduledAutomationRunner` reads schedule settings and runs enabled ingestion/consolidation through `/api/cognitive-memory/automation/run` and operator settings controls. | No dedicated hosted background scheduler was added by design. Scheduled moments are observable when explicitly triggered; autonomous work needs a scoped future design. |
| Validation | Strong alpha | Current P0 proof: 136 Cognitive Memory/agent-context unit tests, 25 Cognitive Memory integration tests, 1 Cognitive Memory component test, web build, and browser settings-tab proof passed. | Add live Qdrant/provider failure integration, API contract tests, retention/load tests, and broader workflow browser proof before beta. |
| Maintainability | P0 closed for scoped surfaces | Advanced services, recall orchestration, recall channels/internal types, Minimal API mapping, DTOs, review UI queries/previews, page code-behind, rendering helpers, and ten Razor tab components were split by responsibility. | Older broad services such as consolidation, settings, procedure, temporal replay, ingestion, workspace, and signal services remain beta-hardening targets. |

## What Is Actually Done

- The module is in the solution and loaded by the runtime composition root.
- EF configuration discovery includes Cognitive Memory module assemblies through `AppDbContextModelRegistry`.
- Durable memory state is not Qdrant. It lives in the active `AppDbContext` profile.
- Source truth is preserved through source manifests, source items, source links, evidence anchors, and claim/evidence links.
- Review-approved consolidation can create canonical memory records and claims.
- Recall persists detailed trace state and renders context packs with included/excluded source refs.
- The Blazor page uses the project component library wrappers such as `PageScaffold`, `PageHeader`, `Tabs`, `SurfaceCard`, `Button`, and `StatusBadge`, with focused child tab components under `Pages/Components`.
- API access is hosted by the web app under `/api/cognitive-memory`.
- Agent model access policy exists and can limit Cognitive Memory context contribution by provider profile or local/remote policy.
- Stale and failed projection records can be rebuilt explicitly through a service/API/UI path from durable memory records, claims, evidence anchors, source links, entities, and context-boundary policy metadata.
- Automation settings now have an explicit runner/API/UI path that honors schedule mode and produces ingestion/consolidation summary output.
- Process-critical MAF execution modes now fail predictably when memory context is required but unavailable.

## What Is Not Yet True

- Cognitive Memory is not an autonomous memory daemon. Scheduled settings and an explicit runner exist, but a dedicated hosted scheduler/worker is intentionally absent until the product defines unattended scope, retry, idempotency, and audit behavior.
- Qdrant is not authoritative memory. It is an optional projection target behind `IRagDriver`.
- Model execution profiles do not by themselves mean every cognitive-memory task calls a chat model. Current consolidation fact extraction is rule-based and source-backed.
- Vector recall is not guaranteed. It requires projection collection/profile/embedding profile inputs and a configured provider with typed filters.
- Cross-project and distributed compute are represented, but they should be treated as alpha control surfaces until exercised by product workflows.
- The API is useful for local agents, but it is not yet a versioned stable external contract.
- P0 closed the child-tab split, but some parent-owned render fragments remain and older broad services still need incremental decomposition.

## Senior Risks

- **UI size risk:** the page is split into child tabs, but the parent still owns several render fragments. Broader browser-facing workflows need proof before beta.
- **Projection provider risk:** rebuild orchestration and adapter-backed proof exist, but live Qdrant/provider integration and failure UX still need beta-grade validation.
- **Automation truth risk:** schedule settings are executable through explicit API/UI runners, and the no-hosted-worker decision is closed for P0. Future autonomous scheduling must be designed explicitly.
- **Provider semantics risk:** recall can still degrade when vector/semantic providers are unavailable. Process-critical MAF context is stricter now, but broader workflow callers need the same clarity.
- **API contract risk:** endpoint grouping improved maintainability, but the contract is still unversioned.
- **Diagnostic payload risk:** recall returns rich trace/candidate data. Agent-facing context now has a separate package, but future endpoints must preserve that separation.

## Validation Evidence

The current source tree contains:

- 17 unit-test files for Cognitive Memory.
- 12 integration-test files for Cognitive Memory persistence and behavior.
- 1 component test file for the Blazor page.
- 1 Playwright file for Cognitive Memory review UI.
- 2 support/fake files for Cognitive Memory tests.

Current P0 validation evidence records:

- Unit Cognitive Memory and agent-context tests: 136/136 passed.
- Integration Cognitive Memory tests: 25/25 passed.
- Component Cognitive Memory tests: 1/1 passed.
- Web project build: passed with 0 warnings and 0 errors.
- Browser proof: `/cognitive-memory` settings tab on `http://127.0.0.1:5289`, 1440x1000 and 390x900 viewports, operational controls present, no narrow horizontal overflow, no console errors beyond normal Blazor connection logs.

Use [validation and testing](../operations/validation-and-testing.md) for the current commands.

