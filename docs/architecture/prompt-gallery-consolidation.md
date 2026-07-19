# Prompt Gallery Consolidation

Status: implemented and validated on 2026-07-19.

Follow-up status: workflow execution overrides, favourite semantics, compact desktop search, and the managed Prompts Curator Agent were completed and validated on 2026-07-19.

## Decision

`CanDoItAll.Modules.Prompts` is the single owner of reusable full prompts and prompt parts. The Prompt Factory project, route, canvas/session behavior, and persistence model are retired. Its packaged prompt-library components are imported into the Gallery as canonical items. Workflow LLM components stop owning reusable instruction text and become workflow-specific bindings to immutable Gallery revisions.

The existing `PromptArtifact` identity and `PromptVersion` revision model are extended instead of introducing a fourth prompt entity. `PromptArtifact` owns the editable item, classification, tags, provenance, portable provider/model compatibility, and optional generation-parameter recommendations. `PromptVersion` remains immutable and is the content boundary used by workflows.

## Responsibility inventory

| Current owner | Current responsibility | Target owner | Disposition |
| --- | --- | --- | --- |
| `Modules.Factory` | Prompt block text, tags, groups, seed loading | `Modules.Prompts` | Move as Gallery items and an explicit idempotent seed importer. |
| `Modules.Factory` | Prompt build sessions, runs, canvas, recommendations, send/build orchestration | None | Remove; this is the unused Prompt Factory product. Existing deliberately saved `PromptArtifact` records remain. |
| `Modules.Prompts` | Prompt artifacts, versions, tags, usage, UI | `Modules.Prompts` | Keep and strengthen as the canonical module. |
| Workflow component store | Reusable instructions plus provider/model/workflow settings | Gallery + workflow binding adapter | Move reusable text to Gallery revisions. Keep only workflow-specific execution settings and revision identity in Agent Framework persistence. |
| Gallery page | In-memory filtering and private file-pack DTOs | Gallery query service and driver | Replace with server-side paging/filtering and the injected seed/catalog service. |
| Generic search index | Global free-text projection | Gallery projection coordinator | Keep as an optional downstream projection; it is not the canonical Gallery query API. |
| Workbench | Prompt Factory session creation/projection | Gallery item creation/projection | Bind project prompt nodes directly to `/prompt-gallery?promptId=...`. |
| Web API | Workflow component CRUD only | Prompt Gallery API | Add the canonical Gallery API. Existing workflow-component endpoints become compatibility adapters over Gallery-backed workflow bindings. |
| Agent Framework | No Gallery tools | Prompt Gallery runtime tool provider | Add read/search/get tools through the Gallery application service. |

## Invariants

1. A reusable prompt body has exactly one canonical `PromptArtifact` identity.
2. A workflow binding references an immutable `PromptVersion`; it does not persist another editable instruction body.
3. A saved workflow node keeps an explicit instruction snapshot. Runtime uses that snapshot, so later Gallery edits cannot mutate an existing workflow version.
4. Factory seed imports are idempotent by stable source key and never silently overwrite user-edited Gallery items.
5. Search projections are rebuildable derivatives. Canonical writes always go to the application database first.
6. Disabled projection drivers are explicit and report `Enabled = false`; there is no silent projection/search fallback.
7. Compatibility is evaluated against a typed consumer context and current provider/model. Suppression is keyed by item, consumer, and issue code; non-suppressible execution errors remain errors.
8. Gallery delete behavior is archival. Immutable revisions referenced by workflow bindings are retained.
9. Workflow-component persistence contains a text-free binding DTO and typed Gallery artifact/version IDs. Hydration fails when the immutable revision is missing or inconsistent.
10. PostgreSQL current-schema adoption derives the EF migration chain and refuses adoption when mapped schema requirements, Gallery search backfills, or Prompt Factory retirement requirements are incomplete.
11. Favourite state is a typed `PromptArtifact.IsFavorite` field exposed through Gallery contracts. Toggling it is an atomic Gallery operation; callers do not rewrite complete drafts, and search drivers return favourites first with stable secondary ordering.
12. Supported provider/model pairs on a Gallery item are portable compatibility guidance and at most one pair may be explicitly preferred. A workflow LLM node owns explicit execution provider/model overrides and the immutable prompt snapshot it will call; choosing an item only preloads those fields.
13. Prompt Gallery mutations exposed to agents are available only to the immutable managed Prompts Curator identity, require assigned capabilities, and remain approval-gated by the runtime tool policy.
14. Editable Gallery updates carry the last-read `UpdatedAtUtc`; stale writes fail at the application boundary and the EF concurrency token closes the read/save race. Draft saves return the exact database-precision token committed by that operation so save-and-version callers cannot publish an intervening writer's content.
15. Search projections contain only the current immutable revision of final, unarchived items. Projection upserts and removals validate canonical state while holding the driver mutation lock so delayed work cannot restore stale content.

## Boundary and dependency direction

Before:

```text
Web/Composition -> Factory -> Prompts
Workbench -> Factory -> Prompts
AgentFramework -> Workbench -> Factory -> Prompts
Workflow persistence -> Workflow component JSON containing prompt text
```

After:

```text
Web/Composition -> Prompts
Workbench -> Prompts
AgentFramework -> Workbench
AgentFramework -> Prompts
Workflow core/abstractions -> no Prompts module reference
Prompt-backed workflow adapter (AgentFramework module) -> Prompts + Workflow abstractions
```

`Modules.Prompts` must not reference Workbench, Agent Framework, workflow runtime, or provider SDK implementations. The outer Agent Framework module maps Gallery contracts to workflow models. This avoids a cycle and keeps lower MAF projects independent of Razor/EF module code.

## Pattern selection records

### Gallery query strategy

- Problem force: the current EF string search must later be replaceable by a RAG/vector implementation.
- Selected pattern: Strategy via `IPromptGallerySearchDriver`.
- Rejected simpler option: placing `Contains` logic directly in the page/service would hard-code the storage and prevent replacement.
- Test seam: driver contract tests cover stable ordering, paging, text, tag, kind, and provider/model filters without rendering Blazor.

### Search projection strategy

- Problem force: a future search-optimized store must be rebuilt from canonical data and may be disabled.
- Selected pattern: Strategy plus coordinator via `IPromptGalleryProjectionDriver` and `PromptGalleryProjectionCoordinator`.
- Rejected alternative: writing directly to RAG/global search from UI or domain entities would leak infrastructure and create dual writes without a repair path.
- Test seam: coordinator tests prove enabled/disabled behavior; a PostgreSQL integration test proves a failed rebuild rolls back to the last complete projection.

### Workflow adapter

- Problem force: workflow runtime requires `LlmCallComponent`, while reusable content belongs to Gallery revisions.
- Selected pattern: Adapter in `Modules.AgentFramework` that composes workflow components from Gallery revisions and workflow binding settings.
- Rejected alternative: making lower workflow projects reference the Razor/EF Gallery module would invert dependency direction. Keeping `Instructions` in `AgentFramework_WorkflowComponents` would retain the duplicate source of truth.
- Test seam: adapter tests verify text-free persistence, immutable revision hydration, model-parameter preload, and explicit missing-revision failures.

No factory, builder, service locator, new partial runtime class, or broad manager is introduced.

### Favourite representation

- Problem force: favourites must behave like agent favourites without leaking a system marker into user taxonomy or adding correlated tag probes to every paged query.
- Selected pattern: typed canonical `IsFavorite` state plus the atomic `SetFavoriteAsync` Gallery command and a paging index that begins with archive/favourite state.
- Rejected alternative: a reserved ordinary tag can be erased by authoritative draft-tag synchronization, leaks into editors/search projections, and makes favourite ordering depend on join-table probes.
- Test seam: query tests prove favourites-only filtering and favourites-first deterministic paging; service tests prove idempotent toggle behavior without rewriting ordinary tags.

### Managed Prompts Curator tools

- Problem force: ordinary agents may search reusable final prompts, while one managed agent must inspect drafts and create, update, and version canonical items.
- Selected pattern: a dedicated identity-gated runtime tool provider in the Agent Framework module. It calls the existing Gallery application service and does not introduce a second prompt repository.
- Rejected alternative: adding unrestricted mutations to the general Prompt Gallery provider would grant every tool-enabled agent canonical write access.
- Test seam: provider tests prove non-curator agents receive no curator tools, mutations require approval metadata, and create/update/version calls persist through the Gallery service.

## Data migration

1. Extend prompt artifacts with item kind, stable source key, summary, archive state, provenance, and optional recommended model parameters.
2. Add supported-model and compatibility-warning-preference tables with foreign keys to prompt artifacts.
3. Move embedded component/group JSON assets from Factory to Prompts.
4. Import all 111 packaged components idempotently, preserving component IDs, keys, content, tags, group, template tokens, and provenance.
5. Copy existing `Factory_PromptBlocks` into Gallery before retired Factory tables are dropped. Pack metadata is merged only when the existing item has not been user-modified.
6. Migrate each legacy workflow component instruction into a Gallery artifact/version and update the workflow record to reference that immutable version. Malformed legacy JSON fails explicitly and is not ignored.
7. Mark migrated component bindings and workflow definition snapshots with indexed schema versions. Normal startup queries only outdated batches; it does not deserialize the complete workflow catalog.
8. Backfill normalized Gallery search text/tag keys and add PostgreSQL trigram indexes for bounded substring search. The search driver remains replaceable.
9. Remove Factory entity configurations, registrations, project references, navigation, scripts, and project files.

## UI ownership

- `PromptGalleryPage`: large-screen routed management page using the shared BaseLib `DataGrid` with server-side paging/filter controls and item editor.
- `PromptGallerySearchList`: compact reusable search/list component.
- `PromptGalleryPickerDialog`: reusable BaseLib `Dialog` around the compact list, with select and edit actions.
- `PromptGalleryPickerButton`: button/icon wrapper that owns dialog state.
- Workflow and chat components consume the picker; they do not implement their own Gallery search.

The large-screen management surface uses one compact filter rail (text, kind, status, tags, provider, model, favourites, archive, clear) above a dense paged grid. The page header is the only Gallery title. Item names use the shared emphasized text style; the favourite affordance is an accessible star button whose pressed state is explicit. Search text/provider/model remain 250 ms debounced, while typed filters reload immediately.

The visual thesis is a restrained desktop operator surface: one hierarchy from the compact page header to the filter rail and results. The content hierarchy is page identity, bounded filters, then canonical items. Picker/editor transitions stay explicit, and no responsive work below the supported large-screen layout is added.

The workflow LLM editor has one Gallery selection affordance, a read-only preview of the exact prompt snapshot, and editable execution provider/model fields. It does not render the retired workflow-component dropdown.

The Agents catalog renders a fast-open action for the immutable Prompts Curator identity using the same floating-chat launcher pattern as the HR Agent.

The repository does not reference Radzen, so Radzen components are not introduced. The CanDoItAll Components MCP was unavailable in this session; the Components skill fallback was applied against existing in-repository BaseLib `PageScaffold`, `SectionCard`, `DataGrid`, `Dialog`, `Button`, `TextBox`, `Stack`, `Grid`, `Cluster`, `LoadingState`, `EmptyState`, and `StatusBadge` usage.

## Acceptance and testability gates

- Unit: EF query driver paging/filtering and deterministic order.
- Unit: compatibility outcomes and suppressible preference behavior.
- Unit: seed importer count, stable IDs, and non-overwrite behavior.
- Unit: projection coordinator disabled/enabled/rebuild behavior.
- Unit: workflow adapter resolves immutable revision content and fails on missing revisions.
- Negative: invalid page sizes/temperature/model entries are rejected; missing revisions and incompatible unsuppressible bindings do not fall back.
- Component: Gallery grid/picker/editor, compatibility warning actions, workflow selection, and chat insertion.
- Integration: Prompt Gallery API CRUD/search/projection status and DI composition.
- Migration: Factory component and legacy workflow-component content is preserved.
- Persistence: workflow component JSON does not contain prompt text; typed bindings hydrate from immutable Gallery versions.
- Architecture: no project, source, registration, route, or runtime reference to `CanDoItAll.Modules.Factory`; no project cycle.
- Runtime: full solution build/tests, large-screen browser smoke, then rebuild and restart the Web host on port 5032.
- Follow-up unit: atomic favourite tagging; favourites-only/favourites-first search; curator identity/capability/tool authorization; workflow provider/model override mapping.
- Follow-up component: single-row Gallery filter rail, bold item title and favourite toggle; workflow LLM editor has no legacy selector and shows prompt/provider/model; Curator fast-open launches floating chat.
- Follow-up runtime: browser-verify Curator fast-open, floating chat, and Gallery insertion. Curator search/create/update/version execution is covered through deterministic identity, approval, concurrency, and persistence tests; live external-model dispatch is excluded from validation when it would disclose canonical Gallery metadata to the configured provider. Rebuild and restart port 5032 after the final gates.

## Risks

- Existing Factory sessions/runs are operational history, not reusable prompt truth. They are intentionally retired rather than bulk-promoted. Existing linked Gallery artifacts survive.
- Workflow bindings need a one-time legacy migration. A malformed binding blocks migration with an actionable error rather than inventing prompt content.
- RAG itself is not implemented. The enabled/disabled projection and search-driver seams are implemented so a RAG driver can be added without changing Gallery callers.
