# Normalized Requirements

## Functional And Structural Requirements

- `R01` The active shipped `canvasWorkbenchInterop` runtime must belong only to `CanDoItAll.Components.CanvasLib`. The legacy `ComponentKit` copy must no longer remain an active shipped duplicate.
- `R02` `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js-src\runtime\canvasWorkbenchInterop.js` must be split into logical workbench-focused source files organized into deeper folders by concern.
- `R03` The generated public CanvasLib runtime output must also be split so no generated CanvasLib file remains above 2000 lines.
- `R04` `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\css-src\workbench\canvas-workbench.css` must be split into logical stylesheet files organized into deeper folders by concern.
- `R05` The generated public CanvasLib stylesheet output must also be split so no generated CanvasLib CSS file remains above 2000 lines.
- `R06` `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js-src\calendar\zy-canvas-calendar.js` must be split into logical calendar-focused source files organized into deeper folders by concern.
- `R07` The generated public CanvasLib calendar output must also be split so no generated CanvasLib file remains above 2000 lines.
- `R08` `C:\repositories\CanDoItAll\tools\canvaslib\asset-manifest.json` and generated include components must become the single ordered source of truth for the new split asset list.
- `R09` The refactor must preserve CanvasLib runtime behavior for the structure canvas route and calendar route.
- `R10` The final validation must include a senior QA style audit that the CanvasLib folder structure is logical and that no file anywhere under `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib` exceeds 2000 lines.

## Non-Goals

- `N01` Do not redesign the CanvasLib APIs or re-implement workbench behavior.
- `N02` Do not refactor preview assets unless needed for manifest or include ordering.
- `N03` Do not run a parallel feature refactor inside `CanDoItAll.ComponentKit`; only retire or disable the duplicate static asset surface that conflicts with CanvasLib ownership.

## Observable Acceptance Conditions

- `A01` Source and generated asset trees are organized into logical folders that a maintainer can navigate by responsibility.
- `A02` The CanvasLib include components load the new split assets in a stable order with no missing dependencies.
- `A03` Builds and targeted tests pass after the reorganization.
- `A04` Browser proof shows the structure canvas and project calendar routes loading successfully after the asset split.
- `A05` A final automated line-count audit proves there are no CanvasLib files above 2000 lines.
