# Requirement Traceability

## Raw Note Closure Matrix

| Raw note | Exact wording | Normalized requirements | Impacted surface | Planned proof method | Owning subbundle | Prerequisite or sequencing signal | Exception status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `N001` | `we still have some trouble with showPopover in canvases.` | `R001`, `R006` | Shared `CanvasWorkbench` JS runtime | Code inspection, targeted validation, browser hover proof | `01-hover-and-popover-state-invariants` | Must land before any broader hardening proof is trusted | `None` |
| `N002` | `it happened in workbench canvas already multiple time. Usually when I click on some node.` | `R002`, `R003`, `R005` | Workbench annotation hover and click flows | Browser hover and click proof on shared canvas and workbench route when available | `02-canvas-runtime-hardening-across-node-interactions` | Depends on subbundle 01 clearing the base crash path | `None` |
| `N003` | `Explore that mechanism and make it more robust.` | `R003`, `R004`, `R006` | Popover show and hide contract, canvas hover state | Code diff review plus browser proof across rerender paths | `01-hover-and-popover-state-invariants` and `02-canvas-runtime-hardening-across-node-interactions` | Foundation first, then adjacent paths | `None` |
| `N004` | `analyze it for all nodes and sittuaionts.` | `R002`, `R005`, `R006` | Shared annotation-bearing node render paths and nearby interaction flows | Source audit plus browser smoke on shared canvas | `02-canvas-runtime-hardening-across-node-interactions` | Requires the common popover entry path to be fixed first | `None` |
| `N005` | `Check for those "anipaterns" or troubles in JavaScript and improve/robust our JavaScript codes around canvas. You must presever all functionalities.` | `R004`, `R005`, `R006`, `R007` | Shared canvas JS runtime, proof discipline | Targeted validation, browser proof, raw-note closure | `02-canvas-runtime-hardening-across-node-interactions` and `03-browser-proof-and-closure` | Do not close until preservation and proof are explicit | `None` |

## Requirements To Bundle Assets

| Requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| `R001` | `analysis/01-current-state.md`, `architecture/01-target-solution.md` | `01-hover-and-popover-state-invariants` | `Targeted validation + shared-canvas browser hover proof` | `Critical foundation` |
| `R002` | `analysis/01-current-state.md`, `plan/01-phase-plan.md` | `02-canvas-runtime-hardening-across-node-interactions` | `Shared-canvas multi-node hover/click browser proof` | `Must follow R001` |
| `R003` | `architecture/01-target-solution.md`, `subbundles/01-hover-and-popover-state-invariants/README.md` | `01-hover-and-popover-state-invariants` | `Code proof + browser re-hover check after click or refresh` | `State contract` |
| `R004` | `architecture/01-target-solution.md`, `subbundles/02-canvas-runtime-hardening-across-node-interactions/README.md` | `01-hover-and-popover-state-invariants` and `02-canvas-runtime-hardening-across-node-interactions` | `Console-clean browser proof` | `Guard against null or disconnected chrome` |
| `R005` | `requirements/01-normalized-requirements.md`, `subbundles/03-browser-proof-and-closure/README.md` | `02-canvas-runtime-hardening-across-node-interactions` and `03-browser-proof-and-closure` | `Targeted validation + workbench smoke` | `Preserve behavior` |
| `R006` | `analysis/01-current-state.md`, `subbundles/02-canvas-runtime-hardening-across-node-interactions/README.md` | `02-canvas-runtime-hardening-across-node-interactions` | `Source audit + browser confirmation` | `Nearby JS anti-pattern sweep` |
| `R007` | `plan/01-phase-plan.md`, `reviews/01-execution-report.md` | `03-browser-proof-and-closure` | `Completed-stage validator + raw-note closure` | `Closure gate` |
