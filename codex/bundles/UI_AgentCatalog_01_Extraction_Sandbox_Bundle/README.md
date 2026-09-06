# Catalog UI extraction and measured iteration

Reference: **CDA-UI-SEAMS-CATALOG-01**. Current status: SB00 through SB03 closed for bounded acceptance. See the [final handoff](reviews/closure.md); whole-repository documentation debt remains. The latest owner request authorized execution after [Providers-02D closure](../UI_Providers_02D_Recovery_Bundle/README.md). Original preparation was completed after Providers-02 on 2026-09-05; it did not itself execute this child.

Outcome: the production AgentCatalogPanel and real AgentSelectionCard render from a lightweight Razor class library; a small browser catalog host uses the same rendering and asset pipeline; reproducible same-machine full-app/sandbox measurements determine whether warm edits improve.

Read [owner scope](inputs/mission.md), [requirements](requirements.md), [source/dependency decisions](architecture/01-boundaries.md), [asset/UI contract](architecture/02-assets-and-composition.md), [measurement protocol](plan/02-measurements.md), [test plan](plan/01-validation.md), [sequential units](plan/00-sequence.md), and [readiness](reviews/readiness.md).

Implemented projects: src/UI/CanDoItAll.AgentFramework.UI and src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox. AgentCatalogHost and all persistence/dialog/chat effects stay in the module. No provider/editor/history extraction, canonical routing, broad Components/FileTools refactor, visual redesign or performance promise.

Compatible compact bundle with manual semantic readiness gate. The current baseline is the uncommitted provider-closed working tree on components-decoupling, not only its unchanged HEAD. Reconfirm remote, working-tree hashes, SDK and live sibling revisions before execution. Do not pin the observed SHA or overwrite newer work.

Current evidence: [valid pre-extraction baseline](proof/SB00/closure.md), [production extraction](proof/SB01/closure.md), [independent browser host](proof/SB02/closure.md). The exact pre-trial edits are in plan/frozen-edits.json; post-extraction source and asset hashes are in plan/SB03-source-freeze.json. The completed comparison has 81 primary warm trials and nine cold starts. Sandbox startup and observed CSS improve in this run; observed Razor/C# warm medians do not. See proof/SB03/closure.md for the measured limits and separate SDK appendix.
