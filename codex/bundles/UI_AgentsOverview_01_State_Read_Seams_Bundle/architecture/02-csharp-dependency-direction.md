# Dependency direction

[Evaluated Module references](../inventory/module-references.json): 47 direct items in current live-sibling mode. This is a receipt, not a desired count. BaseLib, Charts and OverlayLib are also declared package dependencies resolved by repository source policy. [CodeAnalytics](../inventory/codeanalytics.json) has existing namespace/type cycles and incomplete parsed project references; it is not full evaluated graph proof.

Current UI has Models, Conversations.Components and BaseLib in its closure (11 evaluated projects in capability proof). Usage directly references only Models and owns pure contracts/aggregation over read ports. Charts source has no project references, requests Blazor-ApexCharts 6.1.0 and Components.Web 10.0.10. Its README's 10.0.4 is stale; use evaluated assets, not prose. Siblings remain read-only.

O01/O02 change Module ownership, not physical edges. Planned O03 allows UI -> existing Usage value contracts and real Charts, alongside current allowed dependencies. Reuse typed usage scope/rows instead of duplicating the entire model to avoid a small pure library. Surface/sandbox never inject, register or invoke ProviderUsageQueryService or its sources. Session/query execution remains Module. If Usage/Charts acquires runtime dependencies before O03, repair the move map before importing it.

Forbidden UI/sandbox edges: Module, Core, Persistence, provider runtime/ProviderManagement, Voice, AppComponents, broad AgentFramework.Components, workspace services/production composition. No universal Contracts project, runtime service locator or operation state in UI merely for a badge.

O03 must use real AddCanDoItAllCharts, ChartsHeadAssets, Apex JS/CSS and CdaChart. Verify registrations are presentation-only, existing package license/distribution and assets; no library upgrade. Do not stub charts in measurements. Preserve live Components/FileTools revisions and source mode.

Before/after graph proof requires evaluated transitive references/cycles, current CodeAnalytics with limits, direct consumer builds, real chart browser execution, public list tests and source removal from old owner. Actual shared public/asset changes may trigger broader validation; record why first. Empty parsed references/file counts do not prove isolation.
