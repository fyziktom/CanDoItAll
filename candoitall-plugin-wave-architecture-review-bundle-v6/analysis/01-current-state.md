# Current State

## What improved compared with the earlier state

- The cross-module public boundary now has `ProjectNodeReference`, which is healthier than raw string-only bridge contracts.
- CRM/HR node-scoped assignment ownership direction is better than before.
- Persisted SyncGraph rows were replaced by projection assembly contributors and projection layout overrides.
- Foreign ids and transport bindings were moved out of canonical node metadata into binding tables and typed reference rows.
- Node reclassification now writes durable lifecycle history.
- Connector/provider/resource registration now goes through manifest and registry contracts.
- Hierarchy cycle protection and the explicit ban on user-authored hierarchy-like generic links now exist.
- Delete/move compensation paths are covered by integration tests.
- ADR guardrails were added to the repo.
- Editable hierarchy no longer persists duplicate `Contains` / `BelongsTo` truth for canonical nodes.
- Node-role capability rules now come from the Workbench node-kind registry instead of page-local switches.
- Projection-only versus canonical node scope is now enforced through the CRM/HR assignment policy boundary.
- The CRM/HR assignment hotspot was reduced by extracting `ProjectPartyAssignmentNodePolicy`.
- Final readiness proof was rerun with real build, test, and browser evidence.

## What still needs discipline

The major phase-6 blockers are closed. The remaining cautions are now operational rather than architectural:

- `CrmHrServices.cs` is still a large file and will need continued decomposition as new CRM/HR features land.
- Cross-module move/delete safety still relies on durable compensation rather than true atomicity.
- Browser proof is targeted to the changed routes, not a full UI regression matrix.

## Static inventory snapshot

- Projects (`*.csproj`): `42`
- C# files: `603`
- Razor files: `338`

## Architectural hotspots

- `4503` LOC — `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs`
- `1024` LOC — `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `1951` LOC — `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `1292` LOC — `src/CanDoItAll.Modules.CrmHr/Pages/CrmHrDirectoryPage.razor`

## Bottom line

Phase 5 closed the large architectural gaps. Phase 6 closed the remaining plugin-wave blockers and converted the bundle from a `NO-GO` review into a guarded `GO`.
