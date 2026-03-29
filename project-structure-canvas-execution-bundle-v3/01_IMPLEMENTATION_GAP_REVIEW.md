# Implementation gap review

This review compares the **intent** of the previous work with the **current state** of the repository snapshot.

| Area | Current status | What is good | Remaining gap | Next task(s) |
| --- | --- | --- | --- | --- |
| Move persistence batching | Implemented | Good improvement | Still followed by full ReloadSurfaceAsync in ProjectStructurePage.razor:952-965 | T03 |
| Retained rendering / culling | Partially implemented | Visible in current JS runtime | Runtime scene is still DOM/SVG; retained patching does not change the fundamental renderer cost model | T10-T15 |
| Floating window JS isolation | Implemented | Better than before | Still needs stronger overlay ownership contract for wheel/scene isolation | T01 |
| Persisted UI state normalization | Implemented | ManualPositions are stripped before persistence | Persistence itself is still too eager and too often on hot paths | T02 |
| Selection panel extraction | Partially implemented | Some code moved into a partial class | Main page markup is still very large and should become child components | T08 |
| Toolbox browser reliability | Not complete | Markup and state exist | Current accordion logic cannot toggle closed on second click; browser proof is missing | T00/T04 |
| Toolbox UX parity | Not complete | Search and grouping exist | Rows are still two-line; no proper tooltip model; not VS-like compact layout | T05 |
| Real runtime canvas | Not implemented | Only export path and legacy module-specific renderer use real canvas | Current runtime still builds div/svg layers | T10-T15 |
| Shared asset management | Not complete | CanvasLib scripts exist | App entrypoints still hard-code long duplicated lists | T07 |
| CanvasLib maintainability | Not complete | Some partial splitting already happened | Runtime, preview, and legacy concerns are still mixed and file sizes remain too large | T06/T08/T09 |
| PromptFactory shared-canvas parity | At risk | Still compiles conceptually | PromptFactory also uses eager persistence and preview-boundary components; shared changes can regress it | T02/T09/T16 |

## Key takeaways

### The previous work was useful, but it stopped in the middle
The repository is no longer at the earliest, most naive DOM rebuild stage. There is now evidence of:
- batched node move persistence,
- retained element maps,
- viewport filtering,
- better floating-window JS,
- some feature-area partial classes.

That matters. It means the next step should **build on the existing work**, not throw it away blindly.

### The main architectural bottleneck is still unchanged
The runtime workbench scene is still assembled from:
- `div` layers,
- `svg` links,
- `div` nodes,
- `div` frame layers,
- `svg` minimap visuals.

That means the runtime still pays the cost of:
- large DOM trees,
- layout/reflow,
- SVG path updates,
- style recalculation.

### The toolbox still needs product-level repair
The toolbox bug is not only a visual issue. It is a sign that:
- overlay ownership is not fully reliable,
- browser-level validation has blind spots,
- the page still carries too much inline UI complexity.

### The next bundle must do two things at once
1. finish the architectural performance work,
2. make the codebase easier to manage.

Doing only one of those would leave the project in a fragile state.
