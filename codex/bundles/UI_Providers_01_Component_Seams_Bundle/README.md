# Provider profiles: state and reads

Reference: **CDA-UI-SEAMS-PROVIDERS-01**. Owner authorized preparation, implementation and testing on 2026-09-05 following review of d3ba280a431bfe74ce03a72638ac06dff47de660.

Status: implementation and scoped proof complete on 2026-09-05; repository-wide documentation gate remains blocked by 118 unchanged historical tracked logs. See [closure](reviews/closure.md). Follow [scope](requirements.md), [boundaries](architecture/01-csharp-boundary-map.md), [checkpoints](plan/architecture-checkpoints.md), and [proof contract](architecture/04-csharp-testability-plan.md). Shared guidance: [CDA-UI-SEAMS-BASE](../UI_Component_Seams_Shared_Architecture_Bundle/README.md).

Prerequisite: Agents SB09 build and all eight new regressions passed. Provider state/read proof, direct builds, final portability enforcement and large-desktop browser checks passed. The broader stable checkpoint executed 9,462 cases; one unchanged workflow test timed out and then passed its exact isolated rerun. The original failure remains in the evidence.

This compact child owns only AgentProviderProfilesPanel state, selection, per-instance session and reads. It does not execute PROVIDERS-02. This is an in-place architectural prerequisite, with no claim of faster dotnet watch.

Next sequence: PROVIDERS-02 commands/effects after examining the registry's actual commit boundary; then AgentCatalogPanel light assembly/catalog sandbox/watch measurement; then PROVIDER-HISTORY-01. No routing, history internals, shared-provider backend or physical component moves here.
