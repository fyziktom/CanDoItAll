# Assumptions And Risks

## Assumptions

- CanvasLib component namespaces can remain stable during folder moves because the project-level `_Imports.razor` already sets `@namespace CanDoItAll.Components.CanvasLib`.
- Public static asset paths under `_content/CanDoItAll.Components.CanvasLib/...` should remain unchanged even if source-control layout changes.
- `CanDoItAll.ComponentKit` can be retired if build, source-reference, and browser validation show it is not part of the active product surface.

## Critical Path Risks

- `01 Asset ownership and duplicate retirement` is the main foundation. If the asset-source strategy is wrong, every later validation run can produce misleading browser results.
- `02 CanvasLib component topology reorganization` and `03 Canvas graph and contracts decomposition` both affect the shared workbench surface. Weak proof in either phase would make the final UI closure untrustworthy.
- If `ComponentKit` retirement is attempted without proving it is unused, the bundle could create avoidable repo churn or hidden tooling breakage.

## Validation Risks

- Browser validation is required because static-asset regressions may compile cleanly while failing only at runtime.
- Namespace and component discovery regressions may appear only when the consuming modules render shared CanvasLib components.
- Duplicate-audit claims can be weak if they look only at filenames and ignore unreferenced legacy projects, so the execution report must record the audit scope clearly.

## Reopen Triggers

- Reopen subbundle `01` if any shared canvas route loads without expected JS or CSS after the asset cleanup.
- Reopen subbundle `02` if component folder moves require consumer namespace edits beyond the planned minimal compatibility work.
- Reopen subbundle `03` if JSON state parsing, action contracts, or event contracts change behavior after the file split.
- Reopen the legacy duplicate decision if any previously unseen solution, test, or runtime path proves that `CanDoItAll.ComponentKit` is still active.
