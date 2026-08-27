# Authoritative catalog and pricing refresh

## Status

- Completed
- Catalog/pricing, image boundaries, shared model compatibility and approved
  context ordering pass focused regressions and the final real build6 UI flow.
- Architecture and downstream gates: Pass. Exact test scopes and historical failing-first
  evidence are in proof/SB01/manifest.md and reviews/01-execution-report.md.
- Final approval/context scope: 100/100. All three build6 UI tests pass. Eight complete
  successful source invocations, one real image, completed approval continuation and vision
  are verified by proof/SB02/transcripts/real-runtime-evidence.txt.
- Earlier runtime reopens are preserved as failures in transcripts, not relabeled passes.

## Objective

Replace contaminated catalogs with upstream facts and stop implicit invention of models/prices.

## Covered Inputs

- N001/R1, N002/R2, N003/R3 code foundation; N004/R4 real-runtime boundary reopens.

## Prerequisites

- Current-state inspection, architecture entry review, prepared gate; no earlier code phase.
- Preserve pre-edit dirty baseline hashes.

## Exact Source References

- repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs
- repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/Administration/ProviderAdministrationService.cs
- repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/Administration/ProviderModelCatalogPolicy.cs
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentProviderProfilesPanel.razor.cs
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/ProviderModelPricingEditor.razor

## Deliverables

- Explicit kind transition; authoritative catalog/pricing refresh; no implicit price seeding.
- Genuine OpenAI known-price corrections, retained exact shared metadata.
- Focused unit/component/integration regressions and protected fixture harness.

## Dependency Impact

- Critical foundation for SB02. Save/projection/publication consumers require empty-price
  and exact membership proof before live deployment is trusted.

## Validation Depth

- Proof tier: Governed.
- Test projects: CanDoItAll.UnitTests, CanDoItAll.ComponentTests, CanDoItAll.IntegrationTests.
- Filters: ProviderPricingTests, SharedProviderPublicationAndCatalogTests,
  ProviderFeatureMatrixTests, AgentProviderProfilesPanelPricingTests,
  ProviderModelPricingEditorTests, SharedProviderCatalogApiIntegrationTests,
  SharedProviderSourceSyncIntegrationTests, SharedProviderRuntimeProjectionIntegrationTests.
- Selection reason: changed normalization/catalog/editor/discovery and shared consumers.
- Expected discovered tests: named existing classes above plus new authority regressions;
  freeze actual --list-tests names/count before execution; zero or missing names fails.
- Invalidation keys: pricing normalization, catalog policy, refresh result, editor transitions,
  shared publication/projection. Rerun only impacted classes after subsequent changes.
- Reopened scope: ImageGenerationAgentRuntimeToolProviderTests, SharedProviderImageResponseTests,
  SharedProviderRelayPolicyTests, SharedProviderOpenAiCompatibilityIntegrationTests,
  OpenAiChatCompletionsRealClientWireTests, OpenAiChatCompletionsCompatibilityChatClientTests,
  OpenAiRequestCompatibilityPolicyTests and the Maf approval/context classes.
- Added invalidation keys: image-name mapping, response/request schema, semantic model
  resolution and approval/context ordering. Final frozen discovery is in each transcript;
  the final approval group discovers exactly 100 tests and each real UI filter exactly one.
- Broad-gate decision: Not required. No cross-cutting project/DI/runtime contract redesign.

## Implementation Steps

1. Add failing-first regression cases and capture pre-edit hashes.
2. Repair existing owners with typed results and explicit UI events.
3. Build/test selected scope, inspect diff and architecture, capture semantic proof.
4. Verify save/publish/sync dependent flow; release SB02.

## Acceptance Checklist

- Refresh removes stale names/rates; an unknown rate stays absent after round-trip.
- Kind switch cannot retain old vendor defaults, tags or credentials.
- Explicit catalogs and shared copies cannot receive built-ins from transport kind.
- Failed/empty/malformed discovery reports an error and does not replace the editor.
- Tests assert full membership and rate values, not only counts.

## Proof Required

- proof/SB01/manifest.md, semantic-invariants.md, failing/passing transcripts and source hashes.
- Affected builds, nonzero discovery, targeted tests and anti-stub/diff review.
- Desktop screenshot review is finalized with SB02 using the same code build.

## Browser Validation Logging

Record source/client provider Prices and agent Runtime dropdown at 1920x1080 in SB02.
Review normal and open-overlay states, clipping, table/editor scroll ownership and names.

## Progression Gate

- Code and targeted tests pass, exact shared round-trip is demonstrated, and review finds
  no new boundary/dependency violations. UI closure remains coupled to SB02.

## UI Composition Contract

Keep existing desktop split list/editor with Connection, Prices, Runtime and Sharing tabs.
Primary content is editable connection/catalog or readonly mirrored metadata. Counts remain
compact badges, not a dashboard. No new textareas/dialog sizes. At 1920x1080 identity and
primary actions remain discoverable; editor/dialog owns vertical scroll, table horizontal
scroll. Inspect normal pricing and agent model dropdown/overlay states.

## Do Not Do

No alias abstraction, fixture-only live proof, model-name blacklist, new project references,
unrelated cleanup, 5032 mutation, secret logging or full-suite run.

## Reopen Triggers

Stale rows after refresh/save/sync or mismatched nondefault names reopen SB01 and SB02.
Fixture endpoint or missing real execution/usage reopens SB02.
