# Catalog UI extraction and measured iteration

Reference: **CDA-UI-SEAMS-CATALOG-01**. Status: prepared after [Providers-02 closure](../UI_Providers_02_Component_Seams_Bundle/reviews/closure.md), 2026-09-05. **Preparation only: no implementation or measurements executed in this run.** Separate owner authorization is required to execute this child.

Outcome: the production AgentCatalogPanel and real AgentSelectionCard render from a lightweight Razor class library; a small browser catalog host uses the same rendering and asset pipeline; reproducible same-machine full-app/sandbox measurements determine whether warm edits improve.

Read [owner scope](inputs/mission.md), [requirements](requirements.md), [source/dependency decisions](architecture/01-boundaries.md), [asset/UI contract](architecture/02-assets-and-composition.md), [measurement protocol](plan/02-measurements.md), [test plan](plan/01-validation.md), [sequential units](plan/00-sequence.md), and [readiness](reviews/readiness.md).

Expected projects (not created): src/UI/CanDoItAll.AgentFramework.UI and src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox. AgentCatalogHost and all persistence/dialog/chat effects stay in the module. No provider/editor/history extraction, canonical routing, broad Components/FileTools refactor, visual redesign or performance promise.

Compatible compact bundle with manual semantic readiness gate. The current baseline is the uncommitted provider-closed working tree on components-decoupling, not only its unchanged HEAD. Reconfirm remote, working-tree hashes, SDK and live sibling revisions before execution. Do not pin the observed SHA or overwrite newer work.
