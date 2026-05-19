# Cognitive Memory Stage Assessment

## Decision

The honest stage after the P0 pass is **P0-hardened validation-grade alpha**.

It is past a prototype: the module has durable EF models, migrations for SQLite and PostgreSQL, production-shaped services, an endpoint-grouped HTTP API, a Blazor operator route, source ingestion, consolidation, recall, review approval, projection rebuild, explicit automation execution, probes, self-regulation, MAF context contribution policy, and targeted tests. It is still not beta because the API is not versioned, background scheduling is explicit/API-triggered rather than hosted, and the Blazor surface still needs a real component split plus browser proof.

## Stage Matrix

| Area | Current stage | Evidence | What still blocks beta |
| --- | --- | --- | --- |
| Module registration | Done | `AddCognitiveMemoryModule()` is wired from `AddCanDoItAllRuntimeModules()`. | None beyond normal startup tests. |
| Durable schema | Alpha complete | 109 cognitive-memory entity records and 15 SQLite plus 15 PostgreSQL migrations exist. | Schema needs stabilization review before treating it as long-lived external contract. |
| Source ingestion | Alpha complete | Workbench, process, workflow, external file, and web-link ingestion paths exist. | More source-provider coverage and operational retry/diagnostic UX. |
| Consolidation | Alpha | `CognitiveMemoryConsolidationEngine` creates candidates, mutation commands, review rows, and can materialize memory records through the candidate applicator. | Current fact extraction is deterministic/rule-based; model execution profiles are settings, not a fully wired model-assisted consolidation pipeline. |
| Human review | Alpha complete | Review decisions apply or reject consolidation candidates with concurrency checking. | Need audit UX and bulk review ergonomics before production use. |
| Recall | Alpha complete | `CognitiveMemoryRecallOrchestrator` persists traces, candidates, context packs, source refs, and warnings. | Vector channel is optional and skipped/unavailable unless projection collection/profile/embedding profile and provider support are present. |
| Projection/RAG | P0 alpha | RAG adapter, projection lifecycle, and `ICognitiveMemoryProjectionRebuildService` exist. `/api/cognitive-memory/projections/rebuild` consumes stale/failed projection records and rebuilds from durable memory inputs. | Qdrant remains a rebuildable projection only. Provider-backed integration proof and operational failure UI are still needed before beta. |
| MAF context integration | P0 alpha | `CognitiveMemoryAgentContextContributor` now renders an agent-facing context package and explicitly fails process-critical modes when required memory context is unavailable. | Need broader workflow/A2A integration tests and API contract documentation for agent-safe context. |
| Operator UI | Alpha | `/cognitive-memory` and `/memory` render dashboard, probe workbench, settings, sources, memory, review queue, traces, health, self-regulation, and scale tabs. Rendering helpers were split from the code-behind. | Razor markup remains a large file. Focused child components and browser proof are still required after UI behavior changes. |
| API surface | P0 alpha | The API is split into endpoint groups and DTO files and exposes 33 endpoints under `/api/cognitive-memory`. | Public contract needs versioning, stricter OpenAPI examples, and explicit agent-output DTO contract documentation. |
| Probing and self-regulation | Alpha | Probe sessions, feedback, self-model, calibration, answer gate, professor review, learning proposals, cross-project promotions, and distributed jobs are represented. | Some behaviors are policy/control ledgers rather than mature workflow automation. |
| Automation scheduling | P0 explicit runner | `ICognitiveMemoryScheduledAutomationRunner` reads schedule settings and runs enabled ingestion/consolidation through `/api/cognitive-memory/automation/run`. | No dedicated hosted background scheduler was added. Scheduled moments are observable when explicitly triggered, not autonomous daemon work. |
| Validation | Strong alpha | Current P0 proof: 135 Cognitive Memory/agent-context unit tests, 25 Cognitive Memory integration tests, 1 Cognitive Memory component test, and web build passed. | Add provider-backed projection integration, hosted scheduler tests if a worker is introduced, API contract tests, and browser proof after component splits. |
| Maintainability | Improved P0 alpha | Advanced services, recall orchestration, Minimal API mapping, DTOs, and page rendering helpers were split by responsibility. | `CognitiveMemoryPage.razor`, `CognitiveMemoryPage.razor.cs`, recall channel/mapping files, and review UI still need further focused decomposition. |

## What Is Actually Done

- The module is in the solution and loaded by the runtime composition root.
- EF configuration discovery includes Cognitive Memory module assemblies through `AppDbContextModelRegistry`.
- Durable memory state is not Qdrant. It lives in the active `AppDbContext` profile.
- Source truth is preserved through source manifests, source items, source links, evidence anchors, and claim/evidence links.
- Review-approved consolidation can create canonical memory records and claims.
- Recall persists detailed trace state and renders context packs with included/excluded source refs.
- The Blazor page uses the project component library wrappers such as `PageScaffold`, `PageHeader`, `Tabs`, `SurfaceCard`, `Button`, and `StatusBadge`.
- API access is hosted by the web app under `/api/cognitive-memory`.
- Agent model access policy exists and can limit Cognitive Memory context contribution by provider profile or local/remote policy.
- Stale and failed projection records can be rebuilt explicitly through a service/API path from durable memory records, claims, evidence anchors, and source links.
- Automation settings now have an explicit runner/API path that honors schedule mode and produces ingestion/consolidation summary output.
- Process-critical MAF execution modes now fail predictably when memory context is required but unavailable.

## What Is Not Yet True

- Cognitive Memory is not an autonomous memory daemon. Scheduled settings and an explicit runner exist, but a dedicated hosted scheduler/worker is still absent.
- Qdrant is not authoritative memory. It is an optional projection target behind `IRagDriver`.
- Model execution profiles do not by themselves mean every cognitive-memory task calls a chat model. Current consolidation fact extraction is rule-based and source-backed.
- Vector recall is not guaranteed. It requires projection collection/profile/embedding profile inputs and a configured provider with typed filters.
- Cross-project and distributed compute are represented, but they should be treated as alpha control surfaces until exercised by product workflows.
- The API is useful for local agents, but it is not yet a versioned stable external contract.
- The P0 split improved service shape, but it did not complete the full Blazor child-component decomposition.

## Senior Risks

- **UI size risk:** the page markup and code-behind remain large. Browser-facing refactors still need focused component extraction and proof.
- **Projection provider risk:** rebuild orchestration exists, but provider-backed Qdrant/RAG integration and failure UX still need beta-grade validation.
- **Automation truth risk:** schedule settings are now executable through an explicit API runner, but there is still no autonomous background scheduler.
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

- Unit Cognitive Memory and agent-context tests: 135/135 passed.
- Integration Cognitive Memory tests: 25/25 passed.
- Component Cognitive Memory tests: 1/1 passed.
- Web project build: passed with 0 warnings and 0 errors.

Use [validation and testing](../operations/validation-and-testing.md) for the current commands.

