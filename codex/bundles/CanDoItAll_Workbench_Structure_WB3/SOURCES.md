# Review source register

All entries use exact reviewed refs, not the search index's default-branch locations.
The executor must inspect current source and differences before changing it. Ranges
explicitly identify partial reads. A listed test is source inspected, not executed
in this review. Reported counts come from the implementer's report (S02).
No private TRX/log/media was available and no product build was run here.

## S01 — fyziktom/CanDoItAll commit

- Repository/ref: `fyziktom/CanDoItAll` / `bbd9e8de96dc7895abdecc04406766f7aea94c8e`.
- Inspected: Commit metadata and current components-decoupling ref.
- [Exact source](https://api.github.com/repos/fyziktom/CanDoItAll/git/commits/bbd9e8de96dc7895abdecc04406766f7aea94c8e).
- Exact branch/ref, final documentation commit and verified signature.

## S02 — docs/architecture/workbench-insights-wb2.md

- Repository/ref: `fyziktom/CanDoItAll` / `bbd9e8de96dc7895abdecc04406766f7aea94c8e`.
- Inspected: 1-453.
- Git blob SHA: `9305a5d8ac9402d4f0b0812ea8b5cf4ba2f4de82`.
- [Exact source](https://github.com/fyziktom/CanDoItAll/blob/bbd9e8de96dc7895abdecc04406766f7aea94c8e/docs/architecture/workbench-insights-wb2.md).
- Full implementer report. Results were not rerun; local-only Components note is historical.

## S03 — fyziktom/CanDoItAll.Components commit

- Repository/ref: `fyziktom/CanDoItAll.Components` / `24d182c664d0b1f293098643e52caed7384a5d50`.
- Inspected: Commit diff including GanttChart, gantt runtime and GanttCleanupTests.
- [Exact source](https://github.com/fyziktom/CanDoItAll.Components/commit/24d182c664d0b1f293098643e52caed7384a5d50).
- Actual independent cleanup and per-instance JS owner change; published commit now readable.

## S04 — src/UI/CanDoItAll.Workbench.Insights.UI/Reporting/ManagerSummarySession.cs

- Repository/ref: `fyziktom/CanDoItAll` / `bbd9e8de96dc7895abdecc04406766f7aea94c8e`.
- Inspected: full.
- Git blob SHA: `28530402944cc7c28867b1d61120acc0104daca1`.
- [Exact source](https://github.com/fyziktom/CanDoItAll/blob/bbd9e8de96dc7895abdecc04406766f7aea94c8e/src/UI/CanDoItAll.Workbench.Insights.UI/Reporting/ManagerSummarySession.cs).
- Options/report separation and originating read lifetime.

## S05 — src/UI/CanDoItAll.Workbench.Insights.UI/Reporting/ManagerActivitySession.cs

- Repository/ref: `fyziktom/CanDoItAll` / `bbd9e8de96dc7895abdecc04406766f7aea94c8e`.
- Inspected: full.
- Git blob SHA: `d3c22ffb6daa07bc7e01979d53348d6d7a643af3`.
- [Exact source](https://github.com/fyziktom/CanDoItAll/blob/bbd9e8de96dc7895abdecc04406766f7aea94c8e/src/UI/CanDoItAll.Workbench.Insights.UI/Reporting/ManagerActivitySession.cs).
- Per-opening Activity query and CTS ownership.

## S06 — src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectManagerSummarySource.cs

- Repository/ref: `fyziktom/CanDoItAll` / `bbd9e8de96dc7895abdecc04406766f7aea94c8e`.
- Inspected: full.
- Git blob SHA: `df06b58a7c8a32f4d7527b2c3e91207770216962`.
- [Exact source](https://github.com/fyziktom/CanDoItAll/blob/bbd9e8de96dc7895abdecc04406766f7aea94c8e/src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectManagerSummarySource.cs).
- Native root/descendant admission, report preparation and retained snapshot.

## S07 — src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectManagerActivitySource.cs

- Repository/ref: `fyziktom/CanDoItAll` / `bbd9e8de96dc7895abdecc04406766f7aea94c8e`.
- Inspected: full.
- Git blob SHA: `916fa74a8ff79c0fe4da920e7441c7a9c172fc40`.
- [Exact source](https://github.com/fyziktom/CanDoItAll/blob/bbd9e8de96dc7895abdecc04406766f7aea94c8e/src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectManagerActivitySource.cs).
- Native report cutoff, cursor/aggregate provenance and request retirement.

## S08 — src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectManagerSummaryPanel.razor

- Repository/ref: `fyziktom/CanDoItAll` / `bbd9e8de96dc7895abdecc04406766f7aea94c8e`.
- Inspected: full.
- Git blob SHA: `d7f068fa31eb399b778a3ba2e1eea911d649b5ff`.
- [Exact source](https://github.com/fyziktom/CanDoItAll/blob/bbd9e8de96dc7895abdecc04406766f7aea94c8e/src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectManagerSummaryPanel.razor).
- Actual host profile/actor/parameter lifetime and session composition.

## S09 — src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.Insights.cs

- Repository/ref: `fyziktom/CanDoItAll` / `bbd9e8de96dc7895abdecc04406766f7aea94c8e`.
- Inspected: 1-326.
- [Exact source](https://github.com/fyziktom/CanDoItAll/blob/bbd9e8de96dc7895abdecc04406766f7aea94c8e/src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.Insights.cs).
- Native captured selection/menu intents and known results; tool display truncation, no blob hash recorded.

## S10 — src/UI/CanDoItAll.Workbench.Insights.UI/CanDoItAll.Workbench.Insights.UI.csproj

- Repository/ref: `fyziktom/CanDoItAll` / `bbd9e8de96dc7895abdecc04406766f7aea94c8e`.
- Inspected: full.
- Git blob SHA: `bf396c5e1017cc6bec88e358b2441f383edb438c`.
- [Exact source](https://github.com/fyziktom/CanDoItAll/blob/bbd9e8de96dc7895abdecc04406766f7aea94c8e/src/UI/CanDoItAll.Workbench.Insights.UI/CanDoItAll.Workbench.Insights.UI.csproj).
- Declared UI project/package references, not a newly executed MSBuild evaluation.

## S11 — tests/Components/CanDoItAll.Tests.Components/ProjectStructurePageInsightsTests.cs

- Repository/ref: `fyziktom/CanDoItAll` / `bbd9e8de96dc7895abdecc04406766f7aea94c8e`.
- Inspected: full.
- Git blob SHA: `c1cae40cbe673e5ddf0adb6168d8d917181e376f`.
- [Exact source](https://github.com/fyziktom/CanDoItAll/blob/bbd9e8de96dc7895abdecc04406766f7aea94c8e/tests/Components/CanDoItAll.Tests.Components/ProjectStructurePageInsightsTests.cs).
- Native test source for precise effects, ABA/recreation, deletion and grouping; not run here.

## S12 — src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor

- Repository/ref: `fyziktom/CanDoItAll` / `bbd9e8de96dc7895abdecc04406766f7aea94c8e`.
- Inspected: 1-495; 580-840.
- Git blob SHA: `a019fa8ff06a3445dbb8bd2c9bed23ef552b97fd`.
- [Exact source](https://github.com/fyziktom/CanDoItAll/blob/bbd9e8de96dc7895abdecc04406766f7aea94c8e/src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor).
- Actual canvas/toolbar/slots, tabs, specialized native panes and core fields. Not the complete large file.

## S13 — src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.NodeEditing.cs

- Repository/ref: `fyziktom/CanDoItAll` / `bbd9e8de96dc7895abdecc04406766f7aea94c8e`.
- Inspected: 1-300; 370-660.
- Git blob SHA: `1911ff78156bf187345007216ec077223f30c4e7`.
- [Exact source](https://github.com/fyziktom/CanDoItAll/blob/bbd9e8de96dc7895abdecc04406766f7aea94c8e/src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.NodeEditing.cs).
- Actual composer path, native action dispatch, edit model and current-surface lookup.

## S14 — src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureNodeEditor.cs

- Repository/ref: `fyziktom/CanDoItAll` / `bbd9e8de96dc7895abdecc04406766f7aea94c8e`.
- Inspected: 1-225.
- Git blob SHA: `3da04772d284ffb934a23f3d8894e0245a8f2063`.
- [Exact source](https://github.com/fyziktom/CanDoItAll/blob/bbd9e8de96dc7895abdecc04406766f7aea94c8e/src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureNodeEditor.cs).
- Typed native metadata merge and editing exclusions; remaining implementation requires executor audit.

## S15 — src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.Clipboard.cs

- Repository/ref: `fyziktom/CanDoItAll` / `bbd9e8de96dc7895abdecc04406766f7aea94c8e`.
- Inspected: 1-235.
- Git blob SHA: `29061f66b5bff65ea8953d56d3a9e8afb899020e`.
- [Exact source](https://github.com/fyziktom/CanDoItAll/blob/bbd9e8de96dc7895abdecc04406766f7aea94c8e/src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.Clipboard.cs).
- Clipboard capture and preconditions; remaining mutation implementation requires executor audit.

## S16 — src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureCanvasDialogs.razor

- Repository/ref: `fyziktom/CanDoItAll` / `bbd9e8de96dc7895abdecc04406766f7aea94c8e`.
- Inspected: 1-220.
- Git blob SHA: `8ce65a1eb8ea39d85955431958a3435ac179c800`.
- [Exact source](https://github.com/fyziktom/CanDoItAll/blob/bbd9e8de96dc7895abdecc04406766f7aea94c8e/src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureCanvasDialogs.razor).
- Hierarchy/conversion/transfer actual renderer; later mixed runtime sections intentionally deferred.

## S17 — src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ProjectHierarchy.cs

- Repository/ref: `fyziktom/CanDoItAll` / `bbd9e8de96dc7895abdecc04406766f7aea94c8e`.
- Inspected: full.
- Git blob SHA: `038f65cf3cb37f4a5538dffa7c48804ca8b93190`.
- [Exact source](https://github.com/fyziktom/CanDoItAll/blob/bbd9e8de96dc7895abdecc04406766f7aea94c8e/src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ProjectHierarchy.cs).
- WB3-H1 mutable dialog read after native awaits, opening/choice handling.

## S18 — src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.NodeMutations.cs

- Repository/ref: `fyziktom/CanDoItAll` / `bbd9e8de96dc7895abdecc04406766f7aea94c8e`.
- Inspected: 1-300.
- Git blob SHA: `0a281d868da4e9092960298bb7734ce0c3b68bd8`.
- [Exact source](https://github.com/fyziktom/CanDoItAll/blob/bbd9e8de96dc7895abdecc04406766f7aea94c8e/src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.NodeMutations.cs).
- Sibling conversion and descendant-transfer UI lifetimes and partial outcomes.

## S19 — src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureSubprojectTransferCoordinator.cs

- Repository/ref: `fyziktom/CanDoItAll` / `bbd9e8de96dc7895abdecc04406766f7aea94c8e`.
- Inspected: 1-220.
- Git blob SHA: `bab7081d8c401cbb6d9d2bcc16f6975afb571127`.
- [Exact source](https://github.com/fyziktom/CanDoItAll/blob/bbd9e8de96dc7895abdecc04406766f7aea94c8e/src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureSubprojectTransferCoordinator.cs).
- Actual source/target reservations, native creation receipt, transfer/compensation entry boundaries.

## S20 — src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.CreateCatalog.cs

- Repository/ref: `fyziktom/CanDoItAll` / `bbd9e8de96dc7895abdecc04406766f7aea94c8e`.
- Inspected: full.
- Git blob SHA: `8a0b0fbfe89f76046ee2fb5a1bbfdbd75ecc868c`.
- [Exact source](https://github.com/fyziktom/CanDoItAll/blob/bbd9e8de96dc7895abdecc04406766f7aea94c8e/src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.CreateCatalog.cs).
- Native dynamic choices and special task/secret/text/media composer paths.

## S21 — src/CanDoItAll.Components.CanvasLib/README.md

- Repository/ref: `fyziktom/CanDoItAll.Components` / `24d182c664d0b1f293098643e52caed7384a5d50`.
- Inspected: full.
- Git blob SHA: `6e7fb5fc18c2635dc920feefc1981dc64946b3ea`.
- [Exact source](https://github.com/fyziktom/CanDoItAll.Components/blob/24d182c664d0b1f293098643e52caed7384a5d50/src/CanDoItAll.Components.CanvasLib/README.md).
- Actual shared CanvasWorkbench/CanvasFloatingWindow contracts and asset ownership.

## S22 — tests/Components/CanDoItAll.Workbench.Insights.UI.Tests/ReportingSessionTests.cs

- Repository/ref: `fyziktom/CanDoItAll` / `bbd9e8de96dc7895abdecc04406766f7aea94c8e`.
- Inspected: full.
- Git blob SHA: `c308446512a9168e498199ac2ee31fb16e78e1fe`.
- [Exact source](https://github.com/fyziktom/CanDoItAll/blob/bbd9e8de96dc7895abdecc04406766f7aea94c8e/tests/Components/CanDoItAll.Workbench.Insights.UI.Tests/ReportingSessionTests.cs).
- Source of lazy/stale/independent report and Activity tests; not run here.

## S23 — AGENTS.md

- Repository/ref: `fyziktom/CanDoItAll` / `bbd9e8de96dc7895abdecc04406766f7aea94c8e`.
- Inspected: full.
- Git blob SHA: `4d7ab165a035e032dcf327cf3e972316fb5b3d64`.
- [Exact source](https://github.com/fyziktom/CanDoItAll/blob/bbd9e8de96dc7895abdecc04406766f7aea94c8e/AGENTS.md).
- Current repository validation and canonical UI seam instruction entry.

## S24 — docs/testing.md

- Repository/ref: `fyziktom/CanDoItAll` / `bbd9e8de96dc7895abdecc04406766f7aea94c8e`.
- Inspected: 1-170.
- Git blob SHA: `f5ca92c16d64af67665e4ad29ca8e1703272371c`.
- [Exact source](https://github.com/fyziktom/CanDoItAll/blob/bbd9e8de96dc7895abdecc04406766f7aea94c8e/docs/testing.md).
- Isolated PostgreSQL18, exact discovery, focused builds and renderer-event/disposal guidance.

## S25 — docs/architecture/ui-component-seams.md

- Repository/ref: `fyziktom/CanDoItAll` / `bbd9e8de96dc7895abdecc04406766f7aea94c8e`.
- Inspected: 1-210.
- Git blob SHA: `e1ca84e6eca20b51a4d1436d1962b1fb333d1c1e`.
- [Exact source](https://github.com/fyziktom/CanDoItAll/blob/bbd9e8de96dc7895abdecc04406766f7aea94c8e/docs/architecture/ui-component-seams.md).
- Canonical boundaries, state/effect/commit semantics, proof layers and anti-patterns.
