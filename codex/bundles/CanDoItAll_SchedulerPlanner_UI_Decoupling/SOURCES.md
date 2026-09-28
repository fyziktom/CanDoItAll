# Source register

Repository: `fyziktom/CanDoItAll` · branch: `components-decoupling` · review SHA: `0e176a3b99270cdc9a86a57d6276d05979d7352e`.

Observed Git object IDs identify the content returned by the connected GitHub service. They are not
execution pins, local clone checks, build results or a guarantee that future HEAD has no drift.
Coverage below distinguishes whole returned files, partial inspection and metadata.

The root [sources.json](sources.json) is current for this module. The unchanged shared v3 register
is historical foundation provenance. References in the new module documents use EV / PL / SC IDs.

## Current observations

### EV01 — Observed current branch HEAD and GitHub commit signature verification; dynamic URL, observation pinned in this register.

[Pinned source or observed API endpoint](https://api.github.com/repos/fyziktom/CanDoItAll/branches/components-decoupling)

**Purpose:** Observed current branch HEAD and GitHub commit signature verification; dynamic URL, observation pinned in this register.

**Coverage:** Returned GitHub metadata inspected.

### EV02 — docs/architecture/plugins-ui-boundary.md

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/docs/architecture/plugins-ui-boundary.md)

**Purpose:** Implementer-reported closure and test totals, not reviewer product execution.

**Coverage:** Architecture, stage/behavior, reported test/graph and final closure sections inspected; local raw execution evidence not available; benchmark portion not independently verified.

Observed blob: `cb8581584f416a02c49bf9e97d6ef768fbfcb78f`.

### EV03 — Compared baseline and implementation change inventory.

[Pinned source or observed API endpoint](https://api.github.com/repos/fyziktom/CanDoItAll/compare/dd050d5a1489537207e073cac0838f40cde4340f...0e176a3b99270cdc9a86a57d6276d05979d7352e)

**Purpose:** Compared baseline and implementation change inventory.

**Coverage:** Returned GitHub metadata inspected.

### EV04 — No matching GitHub Actions runs available for review.

[Pinned source or observed API endpoint](https://api.github.com/repos/fyziktom/CanDoItAll/actions/runs?head_sha=0e176a3b99270cdc9a86a57d6276d05979d7352e&per_page=10)

**Purpose:** No matching GitHub Actions runs available for review.

**Coverage:** Returned GitHub metadata inspected.

### EV05 — src/UI/README.md

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/src/UI/README.md)

**Purpose:** Current application UI index; context for next module selection.

**Coverage:** Complete returned file.

Observed blob: `6dc4a076bd048fbf8c25cfaec5582c473604f461`.

### PL01 — src/Modules/CanDoItAll.Modules.Plugins.Presentation/PluginsWorkspace.cs

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/src/Modules/CanDoItAll.Modules.Plugins.Presentation/PluginsWorkspace.cs)

**Purpose:** Production/scenario shared workspace, exact submission and effect ownership.

**Coverage:** Complete returned file.

Observed blob: `d5419bce1f15754117d85ee3938d11d6dbe05fad`.

### PL02 — src/Modules/CanDoItAll.Modules.Plugins.Presentation/PluginWorkspaceOperations.cs

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/src/Modules/CanDoItAll.Modules.Plugins.Presentation/PluginWorkspaceOperations.cs)

**Purpose:** Receipt application before refresh, status/replay and completed-operation retention.

**Coverage:** Complete returned file.

Observed blob: `102c10f58b7cdea7575bf2a1309bc4dad06cf6a2`.

### PL03 — src/Modules/CanDoItAll.Modules.Plugins.Presentation/PluginDraftRegistry.cs

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/src/Modules/CanDoItAll.Modules.Plugins.Presentation/PluginDraftRegistry.cs)

**Purpose:** Bounded draft reconciliation, exact reset and origin lifetime.

**Coverage:** Complete returned file.

Observed blob: `ebc3965b1ccbc9df1eb4753dddf67c11a3a3c01a`.

### PL04 — src/Modules/CanDoItAll.Modules.Plugins.Presentation/PluginWorkspaceReads.cs

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/src/Modules/CanDoItAll.Modules.Plugins.Presentation/PluginWorkspaceReads.cs)

**Purpose:** Catalog selection and independent reads; PL-R1.

**Coverage:** Complete returned file.

Observed blob: `66c0e60678661e75e621d140e9445dab4a8a01e0`.

### PL05 — src/Modules/CanDoItAll.Modules.Plugins.Presentation/PluginReadLane.cs

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/src/Modules/CanDoItAll.Modules.Plugins.Presentation/PluginReadLane.cs)

**Purpose:** Read generation/cancellation and current-state acceptance.

**Coverage:** Complete returned file.

Observed blob: `15b0a11925cf81d86dee603b5e7e6f2dc9e3fd08`.

### PL06 — src/Modules/CanDoItAll.Modules.Plugins.Presentation/PluginWorkspaceOwner.cs

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/src/Modules/CanDoItAll.Modules.Plugins.Presentation/PluginWorkspaceOwner.cs)

**Purpose:** Light owner port and typed receipt semantics.

**Coverage:** Complete returned file.

Observed blob: `0366197e6c2ca98d493c66f3d20cf455b972473b`.

### PL07 — src/UI/CanDoItAll.Plugins.UI/PluginConnectionEditorState.cs

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/src/UI/CanDoItAll.Plugins.UI/PluginConnectionEditorState.cs)

**Purpose:** Frozen values, ID/token acceptance and field-aware later-edit reconciliation.

**Coverage:** Complete returned file.

Observed blob: `50cb6134e916c869dd9fa98f9bfa4f78e7ab4109`.

### PL08 — src/UI/CanDoItAll.Plugins.UI/PluginWorkspaceContracts.cs

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/src/UI/CanDoItAll.Plugins.UI/PluginWorkspaceContracts.cs)

**Purpose:** SelectedPlugin lookup, target identities and operation replay policy.

**Coverage:** Complete returned file.

Observed blob: `e1a24bb4a95c8c0ba82fc9701df9b865c5dfb81b`.

### PL09 — src/UI/CanDoItAll.Plugins.UI/PluginSettingsTab.razor

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/src/UI/CanDoItAll.Plugins.UI/PluginSettingsTab.razor)

**Purpose:** Actual oninput and explicit target/reset controls.

**Coverage:** Complete returned file.

Observed blob: `0bc4831b0418ffa46a246e164332fc5a73f69760`.

### PL10 — src/UI/CanDoItAll.Plugins.UI/PluginConnectionsTab.razor

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/src/UI/CanDoItAll.Plugins.UI/PluginConnectionsTab.razor)

**Purpose:** Current OAuth section and owner effect intents.

**Coverage:** Complete returned file.

Observed blob: `5457a7417e09656c4a3c0c00bfa8fbd7b6db71f2`.

### PL11 — src/UI/CanDoItAll.Plugins.UI/PluginDetails.razor

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/src/UI/CanDoItAll.Plugins.UI/PluginDetails.razor)

**Purpose:** Six sections and identity-matched settings composition.

**Coverage:** Complete returned file.

Observed blob: `5e0cddc9b97bad6797b2fbc9134889634efcc0b8`.

### PL12 — src/UI/CanDoItAll.Plugins.UI/PluginsWorkspaceSurface.razor

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/src/UI/CanDoItAll.Plugins.UI/PluginsWorkspaceSurface.razor)

**Purpose:** Real top-level list conditional, InputFile, package/restart state; PL-R1.

**Coverage:** Complete returned file.

Observed blob: `8cde19d2b3715d767ab11bd81e7dd00a8b1070b3`.

### PL13 — src/Modules/CanDoItAll.Modules.Plugins/Pages/PluginWorkspaceSession.cs

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/src/Modules/CanDoItAll.Modules.Plugins/Pages/PluginWorkspaceSession.cs)

**Purpose:** Real owner adapter, failure/result/stage mapping and safe diagnostics.

**Coverage:** Complete returned file.

Observed blob: `39ceb6832574b86e9e969f8c70b7caccb719e231`.

### PL14 — src/Modules/CanDoItAll.Modules.Plugins/Catalog/PluginCommittedException.cs

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/src/Modules/CanDoItAll.Modules.Plugins/Catalog/PluginCommittedException.cs)

**Purpose:** Committed exception and package-stage wrappers.

**Coverage:** Complete returned file.

Observed blob: `577d75d7cb1ce021841b8fe8ed6db99a88441a7f`.

### PL15 — src/Modules/CanDoItAll.Modules.Plugins/Catalog/PluginPackageServices.cs

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/src/Modules/CanDoItAll.Modules.Plugins/Catalog/PluginPackageServices.cs)

**Purpose:** PL-R2 double-fault control flow and preserved package stage semantics.

**Coverage:** Partial file: lines 1–650, including install result handling, extraction/replacement/cleanup and some manifest validation. Runtime loader tail not fully audited.

Observed blob: `0a52ea9de5f349fa7965387aee992e75f6d26980`.

### PL16 — tests/Components/CanDoItAll.Plugins.UI.Tests/PluginsWorkspaceTests.cs

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/tests/Components/CanDoItAll.Plugins.UI.Tests/PluginsWorkspaceTests.cs)

**Purpose:** Existing draft, read, receipt and scenario test source; not rerun.

**Coverage:** Complete returned file.

Observed blob: `5e99516c9008e77711fec2d0338670583156e054`.

### PL17 — tests/Components/CanDoItAll.Plugins.UI.Tests/PluginsEffectTests.cs

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/tests/Components/CanDoItAll.Plugins.UI.Tests/PluginsEffectTests.cs)

**Purpose:** Existing partial read/effect/scope/descriptor tests; not rerun.

**Coverage:** Complete returned file.

Observed blob: `2cb54cc7bce5e7d79039743e8a93daff1edb5a6d`.

### PL18 — tests/Components/CanDoItAll.Plugins.UI.Tests/PluginsBoundaryTests.cs

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/tests/Components/CanDoItAll.Plugins.UI.Tests/PluginsBoundaryTests.cs)

**Purpose:** Actual transitive/public-type guard source and its limits; not rerun.

**Coverage:** Complete returned file.

Observed blob: `bcc0a391dfd5a61c73c260732dfbb66ec936d1d8`.

### PL19 — tests/Integration/CanDoItAll.Tests.Integration/PluginsUiOwnerReceiptTests.cs

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/tests/Integration/CanDoItAll.Tests.Integration/PluginsUiOwnerReceiptTests.cs)

**Purpose:** Existing isolated invalid upload, stage logging, OAuth and wire receipt proofs in source; double-fault path not covered there.

**Coverage:** Returned integration tests and fixture tail read in two requests.

Observed blob: `ed19444595fea462be912ac758fca6596fbd0a1a`.

### PL20 — src/Modules/CanDoItAll.Modules.Plugins.Presentation/CanDoItAll.Modules.Plugins.Presentation.csproj

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/src/Modules/CanDoItAll.Modules.Plugins.Presentation/CanDoItAll.Modules.Plugins.Presentation.csproj)

**Purpose:** Declared Presentation dependency on UI; not a newly evaluated build graph.

**Coverage:** Complete returned file.

Observed blob: `6ab1a4476d82f7879ea2f980f9d63cfa8d803d6f`.

### PL21 — src/UI/CanDoItAll.Plugins.UI/CanDoItAll.Plugins.UI.csproj

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/src/UI/CanDoItAll.Plugins.UI/CanDoItAll.Plugins.UI.csproj)

**Purpose:** Declared UI Razor, BaseLib and Contracts boundary; not a newly evaluated build graph.

**Coverage:** Complete returned file.

Observed blob: `79c261198efe333d20a5c9bfca487e857cfb199c`.

### SC01 — src/Modules/CanDoItAll.Modules.SchedulerPlanner/Pages/SchedulerPlannerPage.razor

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/src/Modules/CanDoItAll.Modules.SchedulerPlanner/Pages/SchedulerPlannerPage.razor)

**Purpose:** Complete rendered surface and mutation/input/interop/lifecycle paths.

**Coverage:** Complete main page read in contiguous line-range requests 1–2700; final request extends beyond EOF.

Observed blob: `b7e653cbc2f88b7f795be5fe5cbd1a8a53a7a8e7`.

### SC02 — src/Modules/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerModels.cs

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/src/Modules/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerModels.cs)

**Purpose:** Contract extraction, authority separation, history cascade and retained fields.

**Coverage:** Returned entities, EF mappings, summaries, workspace, editor/query/schema and launch context/result definitions inspected.

Observed blob: `ed48e0bb10613fa2ae990b2932f956aecdd36679`.

### SC03 — src/Modules/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/src/Modules/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs)

**Purpose:** Durable mutation before projection; exact targets and bounded query/calendar semantics.

**Coverage:** Partial file: lines 1–650; interface/read/default/save/toggle/delete, targets/input validation, history and calendar projection. Later validation/description/other tail not fully audited.

Observed blob: `6aebd787021cb02362590f23f56f48e4cb1d648a`.

### SC04 — src/Modules/CanDoItAll.Modules.SchedulerPlanner/CanDoItAll.Modules.SchedulerPlanner.csproj

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/src/Modules/CanDoItAll.Modules.SchedulerPlanner/CanDoItAll.Modules.SchedulerPlanner.csproj)

**Purpose:** Declared current dependencies, including Quartz hosting and broad application/runtime references.

**Coverage:** Complete returned file.

Observed blob: `1b4d0348eb301e51e210ae2f9c371f240cbda2e3`.

### SC05 — src/Modules/CanDoItAll.Modules.SchedulerPlanner/wwwroot/js/schedulerPlannerCalendarInterop.js

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/src/Modules/CanDoItAll.Modules.SchedulerPlanner/wwwroot/js/schedulerPlannerCalendarInterop.js)

**Purpose:** Single global listener/binding and missing host containment; two-host regression obligation.

**Coverage:** Complete returned file.

Observed blob: `2debf8b393106d341c669f983e430e591e74b5a5`.

### SC06 — src/Modules/CanDoItAll.Modules.SchedulerPlanner/Pages/SchedulerPlannerPage.razor.css

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/src/Modules/CanDoItAll.Modules.SchedulerPlanner/Pages/SchedulerPlannerPage.razor.css)

**Purpose:** Asset/scoping preservation and applicable CSS watch measurement.

**Coverage:** Returned calendar, layout, form and dialog/card scoped CSS rules inspected; no browser/computed-style proof.

Observed blob: `a7cfc4a7d51a2cd6474b4c0a05ac3117eede0670`.

### SC07 — src/Modules/CanDoItAll.Modules.SchedulerPlanner/README.md

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/src/Modules/CanDoItAll.Modules.SchedulerPlanner/README.md)

**Purpose:** Current Workflow-only scheduling, retained admission and source-authority invariants; remaining wider obligations.

**Coverage:** Complete returned file.

Observed blob: `82574189527dad2b1b1e91f221eb52a4b630e643`.

### SC08 — tests/Components/CanDoItAll.Tests.Components/SchedulerPlannerPageTests.cs

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/tests/Components/CanDoItAll.Tests.Components/SchedulerPlannerPageTests.cs)

**Purpose:** Existing owning component tests and entry characterization; not a full test-suite audit.

**Coverage:** Partial file: first 280 lines; existing Agent, tabs/calendar, history, picker and typed input cases. Remaining cases require current discovery.

Observed blob: `56a0324ab7017037fcd152ad4247c777fb5eb31a`.

### SC09 — src/Modules/CanDoItAll.Modules.SchedulerPlanner/SchedulerAgentChatContextBuilder.cs

[Pinned source or observed API endpoint](https://github.com/fyziktom/CanDoItAll/blob/0e176a3b99270cdc9a86a57d6276d05979d7352e/src/Modules/CanDoItAll.Modules.SchedulerPlanner/SchedulerAgentChatContextBuilder.cs)

**Purpose:** Current Agent source/view/selection/overlay/context-access presentation contract.

**Coverage:** Complete returned file.

Observed blob: `d1919f0bc184f7b9e8ba89c77de07f0340992257`.

## Execution limitations

The two Plugins findings and Scheduler risk map are source-derived. No product code was
modified or tested by the reviewer. Implementer claims in EV02 retain their original scope;
EV04 only describes matching GitHub Actions runs. See [proof status](PROOF_STATUS.md).

Do not require a forced historical checkout. At implementation entry inspect changed registered
files and actual new/removed consumers, then record current provenance and test selection.
