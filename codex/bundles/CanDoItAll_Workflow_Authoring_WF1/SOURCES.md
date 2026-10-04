# Reviewed primary sources

All entries refer to the exact reviewed main revision, not the default branch returned by code search.
The inspection scope is stated explicitly. Paths not read in full are not represented as full audits.
A repository report is an implementer claim supported here by source inspection, not an independently
replayed TRX or container run. Commit references are review provenance, not checkout instructions.

## S01 — `docs/architecture/agent-authoring-ui-ca1.md`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/docs/architecture/agent-authoring-ui-ca1.md)

Git blob: `bde2c1833cd298a1e3ef6c2da08fb9c0f838183f`. Full CA1 implementation report; recorded results, not independently replayed artifacts.

## S02 — `src/UI/CanDoItAll.AgentFramework.CapabilityAuthoring.UI/CapabilityAuthoringSession.cs`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/src/UI/CanDoItAll.AgentFramework.CapabilityAuthoring.UI/CapabilityAuthoringSession.cs)

Git blob: `bb84a5931570bcf2afe6f4a5164d134b4f400b17`. Full session; sequential setup-result carry-over finding.

## S03 — `src/UI/CanDoItAll.AgentFramework.CapabilityAuthoring.UI/CapabilitySetupPanel.razor`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/src/UI/CanDoItAll.AgentFramework.CapabilityAuthoring.UI/CapabilitySetupPanel.razor)

Git blob: `f6d4440834b6b3243da53b8964119d6e4eccf792`. Full renderer; diagnostic/tool lists render independently of success.

## S04 — `tests/Components/CanDoItAll.AgentFramework.CapabilityAuthoring.UI.Tests/CapabilityAuthoringStateTests.cs`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/tests/Components/CanDoItAll.AgentFramework.CapabilityAuthoring.UI.Tests/CapabilityAuthoringStateTests.cs)

Git blob: `55dc7a32ba37d6b0171122595fdd2418e58af07d`. Full state tests; immutable submissions and historical result coverage.

## S05 — `src/UI/CanDoItAll.AgentFramework.CapabilityAuthoring.UI/CapabilityAuthoringDraft.cs`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/src/UI/CanDoItAll.AgentFramework.CapabilityAuthoring.UI/CapabilityAuthoringDraft.cs)

Git blob: `6803a8ceb1d7c214a7308e06210aeb8490273bfd`. Full draft; field revisions, raw repair and immutable preparation.

## S06 — `src/UI/CanDoItAll.AgentFramework.CapabilityAuthoring.UI/CapabilityAuthoringForm.razor.cs`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/src/UI/CanDoItAll.AgentFramework.CapabilityAuthoring.UI/CapabilityAuthoringForm.razor.cs)

Git blob: `673dae9c9984701ab81d4970acebef574fb8f2a8`. Full component lifetime and completion handling.

## S07 — `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/NativeCapabilityAuthoringHost.razor`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/NativeCapabilityAuthoringHost.razor)

Git blob: `d337fc81ecefaa95e7c03befb74144338f546cc2`. Full native host and profile retirement.

## S08 — `src/UI/CanDoItAll.AgentFramework.UI/Teams/TeamMetadataEditor.razor`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/src/UI/CanDoItAll.AgentFramework.UI/Teams/TeamMetadataEditor.razor)

Git blob: `53b867b54919c0f103ccf69d7491b331c58f82a2`. Full metadata renderer and owned draft.

## S09 — `src/MAF/Common/CanDoItAll.AgentFramework.Core/Catalog/AgentFrameworkWorkspaceCatalogService.Teams.cs`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/src/MAF/Common/CanDoItAll.AgentFramework.Core/Catalog/AgentFrameworkWorkspaceCatalogService.Teams.cs)

Git blob: `1fa265c014a481fd51dfc4b1f4a6cfae6f0c30b1`. Full native metadata/member coordination; legacy full upsert preserved.

## S10 — `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Admission/MafToolProtocolCodec.cs`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Admission/MafToolProtocolCodec.cs)

Git blob: `80ee9ccc56cc21f1d716e3b0fad3684bf28b35a3`. Full installed-SDK raw-model codec including bounded MCP support.

## S11 — `tests/Unit/CanDoItAll.Tests.Unit/MafMcpProtocolCheckpointTests.cs`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/tests/Unit/CanDoItAll.Tests.Unit/MafMcpProtocolCheckpointTests.cs)

Git blob: `1d88a74dce11e9321ce8975ace8d01787b7c9e3c`. Full round-trip and unknown/foreign-shape rejection tests.

## S12 — `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.UI/CanDoItAll.AgentFramework.Workflows.UI.csproj`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.UI/CanDoItAll.AgentFramework.Workflows.UI.csproj)

Git blob: `b277df99ad788938864666859adde3575f530509`. Full existing service-free shell rendering project.

## S13 — `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.UI/README.md`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.UI/README.md)

Git blob: `3e4b89a0c9d01108fd1026437de0c5c57e175b94`. Full existing shell/catalog/history/template/overview/analytics boundary.

## S14 — `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor)

Git blob: `339e8143dca6ee8a9caa17abfbbcf8e6ecad9185`. Page composition and remaining overlays; inspected main body and lines 360-440.

## S15 — `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs)

Git blob: `72d3500b49680a838e26fa544fa4d8dfe964a324`. Inspected lines 1-260 and 2000-2380; query targets, version-keyed editor, native context and ownership.

## S16 — `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor)

Git blob: `17134be59b2fc1d3c995350938b5e27594862a22`. Inspected lines 1-200; real canvas, windows, toolbox and component/Prompt integration. Remaining sections require entry census.

## S17 — `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.cs`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.cs)

Git blob: `a9278909a4396ddb99f8cd95261a6928f922c3d6`. Inspected lines 1-300, 400-1210 and 1800-2170; Save, preview, Prompt binding, local edits, trust and progress observer.

## S18 — `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasModels.cs`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasModels.cs)

Git blob: `ec7b4229bebffaf2cbe5cf03d05d480b75b12efa`. Inspected lines 1-320; document, node and edge projections and ToDefinition/FromDefinition.

## S19 — `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowImageGenerationSettingsRenderer.razor`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowImageGenerationSettingsRenderer.razor)

Git blob: `eb588d36d52ff4d85794aa804965d07b37b1336d`. Full custom renderer, schema contract, native provider reads and fallback composition.

## S20 — `src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs)

Git blob: `4dacf5aba8a919d4ff826a9a717912557b7244e3`. Inspected lines 1-390; native head/version persistence, input snapshot, status and exchange entry points.

## S21 — `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Core/WorkflowCatalogServices.cs`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Core/WorkflowCatalogServices.cs)

Git blob: `01f5bf110ec09b824f3e9901ff98f24dcfe081d7`. Inspected lines 1-230; in-memory implementation is not a substitute for native PostgreSQL proof.

## S22 — `tests/Components/CanDoItAll.Tests.Components/WorkflowsPageTests.cs`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/tests/Components/CanDoItAll.Tests.Components/WorkflowsPageTests.cs)

Git blob: `f18544b02caae155f2a06c5bb23b66fe284c3758`. Inspected first 220 lines; real canvas preview, curator readiness and starter native owners.

## S23 — `docs/architecture/modules.md`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/docs/architecture/modules.md)

Git blob: `d4ee12c0c20673fe45dc54de5939f7e1da17b661`. Inspected current rendering census and remaining roadmap, lines 95-260.

## S24 — `AGENTS.md`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/AGENTS.md)

Git blob: `4d7ab165a035e032dcf327cf3e972316fb5b3d64`. Full canonical repository entry instructions.

## S25 — `.github/copilot-instructions.md`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/.github/copilot-instructions.md)

Git blob: `dde5c2fdbcb56b7b5f48dfa17ed74fda238c2e1a`. Full engineering, component reuse, ownership and validation rules.

## S26 — `.github/workflows/ci.yml`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/.github/workflows/ci.yml)

Git blob: `003a5160a25b6687d854cd1f3b25e19f73582d95`. Inspected first 85 lines; matching Components ref and FileTools pin.

## S27 — `docs/architecture/ui-component-seams.md`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/docs/architecture/ui-component-seams.md)

Git blob: `e1ca84e6eca20b51a4d1436d1962b1fb333d1c1e`. Inspected lines 1-210; authoritative UI separation and proof rules.

## S28 — `docs/testing.md`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/docs/testing.md)

Git blob: `0892788f6ee815711f687eb46ce62f82220ca71c`. Inspected lines 1-155; current isolated PostgreSQL and focused test/discovery rules.

## S29 — `src/UI/CanDoItAll.AgentFramework.CapabilityAuthoring.UI/CapabilityAuthoringForm.razor`

[Read source](https://github.com/fyziktom/CanDoItAll/blob/ccca2fd3a7c4239d2869e8143617fd9ab04c3723/src/UI/CanDoItAll.AgentFramework.CapabilityAuthoring.UI/CapabilityAuthoringForm.razor)

Git blob: `301bf45858ca94c7c3fe4ca4754afdbdabcb1bfc`. Full actual wizard/detail form; not a session-only surface.
