# SB06 Semantic Invariants

## Typed aggregate ownership

- Invariant ID: SB06-ANALYTICS-TYPED-PROJECTION
- Expected behavior: the analytics panel renders `WorkflowAnalyticsSnapshot` returned by `IWorkflowAnalyticsQueryService`.
- Disallowed shallow implementation: sum the eight recent rows, count the current History page, deserialize workflow event payload JSON, or query stores/runtime directly from the component.
- Production assertions: aggregate cards use snapshot totals; recent runs use only `RecentRuns`; source gates reject `WorkflowEvent`, `PayloadJson`, `JsonDocument`, and `HistoryRunPageSize` dependencies.

## Honest usage and pricing presentation

- Invariant ID: SB06-ANALYTICS-PRICING
- Expected behavior: total/input/cached/output/reasoning tokens, observation completeness, known cost, known-priced observations, and unknown-pricing observations remain distinct.
- Disallowed shallow implementation: treat zero known cost as missing, treat unknown pricing as free, omit cached/reasoning tokens, or hide unknown usage.
- Production assertions: a fixture with known-free observations plus two unknown-price observations displays `$0.000000` and `2` separately.

## Lazy typed scope and refresh

- Invariant ID: SB06-ANALYTICS-SCOPE
- Expected behavior: the inactive panel performs no query; first activation defaults to all workflows; SelectedWorkflow sends a typed workflow ID; relevant page mutations advance a refresh version.
- Disallowed shallow implementation: eagerly query during page initialization, infer scope from the selected History page, or use string identifiers for All versus SelectedWorkflow.
- Production assertions: the scope is an enum plus typed `WorkflowId`; direct component tests capture exact queries and page source gates prove Analytics-tab activation.

## Failure and concurrency honesty

- Invariant ID: SB06-ANALYTICS-QUERY-SAFETY
- Expected behavior: failures show a fixed safe message and log only actionable scope identifiers; a newer query cancels the previous query and owns the presentation through a monotonically increasing request gate.
- Disallowed shallow implementation: render `exception.Message`, log provider/event payloads, or let a late older result replace a newer scope/refresh result.
- Production assertions: error and deliberately out-of-order completion tests prove both behaviors.

## Large-screen component-library composition

- Invariant ID: SB06-ANALYTICS-LARGE-SCREEN
- Expected behavior: layout uses existing BaseLib wrappers and only large/extra-large column declarations.
- Disallowed shallow implementation: add raw structural HTML elements, page-local CSS, a parallel component library, or small/medium responsive work.
- Production assertions: the panel uses `Stack`, `Cluster`, `Grid`, `SurfaceCard`, `SectionCard`, `MetricCard`, `FormField`, `DataGrid`, `StatusBadge`, `LoadingState`, and `EmptyState`; anti-stub search finds no raw structural elements or `ColumnsSm`/`ColumnsMd`.

## Schema-driven executor settings ownership

- Invariant ID: SB06-SETTINGS-SCHEMA
- Expected behavior: every built-in and plugin descriptor projects and edits all fields from its authoritative `ConfigurationSchema` through one mapper/host path.
- Disallowed shallow implementation: executor-ID switches for StorageFile, HttpFetch, Spreadsheet, ProjectStructure, ImageGeneration, or newly added executors; duplicated typed settings serializers in the editor.
- Production assertions: the editor source audit rejects all five legacy executor-ID branches and their update helpers; catalog tests enumerate every field for the new document/image/storage/spreadsheet descriptors.

## Trusted capability renderer activation

- Invariant ID: SB06-SETTINGS-TRUST
- Expected behavior: only an explicit `CustomRenderer` descriptor makes a renderer claim; activation requires an exact renderer key, application/bundled-plugin trust, owner ID, and schema version; explicit `Schema` mode uses the declarative schema path.
- Disallowed shallow implementation: silently schema-fallback a broken custom-renderer claim, infer intent from a non-empty setup key, manifest type-name activation, service location, or owner/trust fallback.
- Production assertions: visible failure tests cover incomplete/missing/trust/owner/schema mismatches; an empty-schema custom renderer still reaches the host; plugin manifest validation requires a matching bundled renderer and capability; runtime parity rejects mode drift; legacy JSON defaults to Schema.

## Image provider capability honesty

- Invariant ID: SB06-SETTINGS-IMAGE-PROVIDER
- Expected behavior: the trusted image renderer lists only image-generation provider profiles, prevents disabled selection, does not flag a saved provider before async discovery completes, and preserves explicit unavailable-provider feedback afterward.
- Disallowed shallow implementation: raw GUID-only editing, chat-provider leakage, selectable disabled providers, transient false warnings, or exception-message logging.
- Production assertions: three focused renderer tests cover field completeness, capability filtering, disabled option state, trusted registration, and deferred provider loading; logging contains only a safe failure-type dimension.

## Evidence Contract

- Source raw note: all new executors must be desktop-discoverable and render capability-appropriate settings, while plugins may contribute their own schema and trusted renderer claims.
- Failing-first test: the analytics component absence and subsequent semantic failures are recorded in `bundle://proof/SB06/failing-analytics.txt`; the executor settings slice retained source and component gates that reject the legacy hard-coded branch.
- Passing test: analytics, settings mapper/renderer, plugin parity, and catalog tests are recorded in `bundle://proof/SB06/passing-analytics.txt` and `bundle://proof/SB06/passing-settings.txt`; the production interaction pass is `bundle://proof/SB06/browser-validation.md`.
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowAnalyticsPanel.razor` (`1f99a4e11226060f317303880a36cfa56afa07e38fd10519b505561c134813e0`), `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowImageGenerationSettingsRenderer.razor` (`b8b4b8eba7f9080aa9864bfa8cd44b9d88a1986f1286419a92fbab1a9c533359`), and `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowSettingsRendererSource.cs` (`b3af6684776714e940021e11c11bee3359da1a321ec470e746ec5ef43f29e3be`).
- Red-team negative case: broken trust/owner/version claims fail visibly, custom empty-schema renderers still activate, disabled/non-image providers cannot be selected, stale analytics queries cannot overwrite newer results, and dialogs cannot be hidden behind floating desktop windows.
- Downstream dependency check: SB07 exercised production catalog, image custom settings, Gmail plugin schema settings, analytics, screenshot inspection, and console review at 1600x1000.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Typed workflow analytics snapshot | `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Core/WorkflowAnalyticsQueryService.cs` | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowAnalyticsPanel.razor` | lazy activation, refresh, and scope proof in `bundle://proof/SB06/passing-analytics.txt` | stale-query, safe-failure, recent-window, and unknown-pricing negatives in `bundle://proof/SB06/semantic-invariants.md` |
| Trusted executor settings presentation claim | authoritative executor/plugin descriptors proven in `bundle://proof/SB06/settings-architecture.md` | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowSettingsRendererSource.cs` and `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowImageGenerationSettingsRenderer.razor` | inspector, Node setup, and Gmail dialog proof in `bundle://proof/SB06/browser-validation.md` | trust/owner/version/key/empty-schema negatives in `bundle://proof/SB06/passing-settings.txt` |
