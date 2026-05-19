# Cognitive Memory Stage Assessment

## Decision

The honest stage is **validation-grade alpha**.

It is past a prototype: the module has durable EF models, migrations for SQLite and PostgreSQL, production-shaped services, a large HTTP API, a Blazor operator route, source ingestion, consolidation, recall, review approval, probes, self-regulation, and targeted tests. It is not beta because several important surfaces still behave like an implementation checkpoint rather than a stable product contract.

## Stage Matrix

| Area | Current stage | Evidence | What still blocks beta |
| --- | --- | --- | --- |
| Module registration | Done | `AddCognitiveMemoryModule()` is wired from `AddCanDoItAllRuntimeModules()`. | None beyond normal startup tests. |
| Durable schema | Alpha complete | 109 cognitive-memory entity records and 15 SQLite plus 15 PostgreSQL migrations exist. | Schema needs stabilization review before treating it as long-lived external contract. |
| Source ingestion | Alpha complete | Workbench, process, workflow, external file, and web-link ingestion paths exist. | More source-provider coverage and operational retry/diagnostic UX. |
| Consolidation | Alpha | `CognitiveMemoryConsolidationEngine` creates candidates, mutation commands, review rows, and can materialize memory records through the candidate applicator. | Current fact extraction is deterministic/rule-based; model execution profiles are settings, not a fully wired model-assisted consolidation pipeline. |
| Human review | Alpha complete | Review decisions apply or reject consolidation candidates with concurrency checking. | Need audit UX and bulk review ergonomics before production use. |
| Recall | Alpha complete | `CognitiveMemoryRecallOrchestrator` persists traces, candidates, context packs, source refs, and warnings. | Vector channel is optional and skipped/unavailable unless projection collection/profile/embedding profile and provider support are present. |
| Projection/RAG | Alpha boundary | RAG adapter and projection lifecycle exist; consolidation marks stale projections. | No complete rebuild worker/API loop is wired as the normal product path. Qdrant remains rebuildable projection only. |
| MAF context integration | Alpha | `CognitiveMemoryAgentContextContributor` contributes recall context when provider policy and project scope allow it. | It skips on unavailable memory; process-critical modes may need explicit fail/skip policy instead of generic optional context behavior. |
| Operator UI | Alpha | `/cognitive-memory` and `/memory` render dashboard, probe workbench, settings, sources, memory, review queue, traces, health, self-regulation, and scale tabs. | Large page/code-behind should be split; more browser proof is needed after UI refactors. |
| API surface | Alpha | `CognitiveMemoryApi` exposes 31 endpoints under `/api/cognitive-memory`. | DTOs are large and co-located; public contract needs versioning, stricter OpenAPI examples, and agent-output DTO separation. |
| Probing and self-regulation | Alpha | Probe sessions, feedback, self-model, calibration, answer gate, professor review, learning proposals, cross-project promotions, and distributed jobs are represented. | Some behaviors are policy/control ledgers rather than mature workflow automation. |
| Automation scheduling | Settings only | `CognitiveMemoryAutomationSettings` persists schedule and ingest/consolidate flags. | No dedicated cognitive-memory background scheduler was found. Manual/API execution is the actual path today. |
| Validation | Strong alpha | 33 Cognitive Memory related test files exist across unit, integration, component, Playwright, and support projects. Prior bundles record 117 unit, 25 integration, 1 component, and build passing after repairs. | Add end-to-end scheduled automation, projection rebuild, and provider-failure tests. |
| Maintainability | Needs refactor | Largest files: recall service 2861 lines, advanced services 2370, page code-behind 1642, Razor page 1378, review UI service 1035. | Split by responsibility before adding new behavior. |

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

## What Is Not Yet True

- Cognitive Memory is not an autonomous memory daemon. Scheduled settings exist, but a dedicated scheduler/worker was not found.
- Qdrant is not authoritative memory. It is an optional projection target behind `IRagDriver`.
- Model execution profiles do not by themselves mean every cognitive-memory task calls a chat model. Current consolidation fact extraction is rule-based and source-backed.
- Vector recall is not guaranteed. It requires projection collection/profile/embedding profile inputs and a configured provider with typed filters.
- Cross-project and distributed compute are represented, but they should be treated as alpha control surfaces until exercised by product workflows.
- The API is useful for local agents, but it is not yet a versioned stable external contract.

## Senior Risks

- **Service size risk:** recall, advanced services, review UI, settings, ingestion, and consolidation are too large. New features will keep raising regression cost until they are split around stable use cases.
- **Projection gap risk:** projection lifecycle and RAG adapters exist, but invalidation without an obvious rebuild operation creates stale-vector risk.
- **Automation truth risk:** UI/settings labels imply scheduled automation, while the actual execution path is still manual/API driven.
- **Provider semantics risk:** unavailable semantic/vector providers degrade to skipped or unavailable channels. That is acceptable for local alpha smoke, but process-critical agent memory should make skip/fail policy explicit.
- **API shape risk:** endpoint DTOs live in one large `CognitiveMemoryApi.cs` file. This is workable for alpha but brittle for long-term API evolution.
- **Diagnostic payload risk:** recall returns rich trace/candidate data. Agent-facing context should stay separated from diagnostic payloads so callers cannot accidentally treat diagnostics as answer context.

## Validation Evidence

The current source tree contains:

- 17 unit-test files for Cognitive Memory.
- 12 integration-test files for Cognitive Memory persistence and behavior.
- 1 component test file for the Blazor page.
- 1 Playwright file for Cognitive Memory review UI.
- 2 support/fake files for Cognitive Memory tests.

Historical bundle evidence records the latest full Cognitive Memory validation as:

- Unit Cognitive Memory tests: 117/117 passed.
- Integration Cognitive Memory tests: 25/25 passed.
- Component Cognitive Memory tests: 1/1 passed.
- Serial solution build passed with unrelated existing `Google.Protobuf` warnings.

Use [validation and testing](../operations/validation-and-testing.md) for the current commands.

