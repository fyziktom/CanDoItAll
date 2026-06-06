# process-dispatch-projection-host-facet-boundary-v1

Status: Prepared for Codex implementation.
Created: 2026-06-06 01:27:01Z
Profile: initiative.

## Mission

Continue the safe `maf-processes-refactor` dispatcher decomposition without starting `CanDoItAll.Processes.Core` and without introducing production process-driver APIs. The previous bundle successfully split artifact projection source-family coordinators, but the new boundary still flows through a broad `IProcessArtifactProjectionHost` adapter. This bundle narrows that host into module-local projection facets and updates each projection coordinator to consume only the dependencies it actually needs.

## Why this bundle exists

The current branch shows that `ProjectExecutionArtifactsAsync` is now a thin source-family facade, which is good. However, the top-level coordinators still depend on a monolithic host interface, and the dispatcher has a 389-line adapter forwarding dozens of methods back into `ProcessRunAutomationDispatchService`. That is not Core-ready and not driver-ready yet. The next safe step is to turn this transitional host into smaller module-local dependency facets.

## Non-goals

- Do not create `CanDoItAll.Processes.Core`.
- Do not create `IProcessDriverPack`, `IProcessDriverRegistry`, driver packages, helper-driver APIs, or production driver registries.
- Do not move EF entities, public contracts, or DB writes into a new project.
- Do not change runtime behavior, projection source-family order, artifact identity, external reference keys, lineage, storage placement, or candidate mutation semantics.
- Do not touch UI/Razor/CSS/JS/TS. Browser validation remains `N/A` unless a prohibited UI diff appears; if it appears, stop and reopen the offending subbundle.

## Required implementation style

Perform SB01 through SB72 in order. Every critical gate must pass before dependent subbundles continue. If a gate fails, reopen the last source-moving subbundle instead of papering over the proof.
