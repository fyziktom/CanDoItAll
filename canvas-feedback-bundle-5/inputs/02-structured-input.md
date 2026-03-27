# Structured Input

## Extracted Notes

- `N001` The blocks explorer is missing the standard minimize and hide controls.
- `N002` Clicking a section should open the items underneath it and behave like an accordion.
- `N003` The explorer should use the shared in-canvas floating window behavior, including drag and movement, while staying in dark mode.
- `N004` Search results must remain scrollable and readable inside the window, and the final state must be validated with screenshots.

## Working Assumptions

- The `Blocks` window on `ProjectStructurePage` is the `Blocks explorer` named in the feedback.
- The correct implementation path is to reuse `CanvasFloatingWindow` with its shared chrome instead of inventing page-local minimize, hide, or drag behaviors.
- Outside of active search, the blocks explorer should keep one expanded section at a time so it behaves like an intentional accordion instead of a permanently open catalog.

## Validation Expectations

- run focused component coverage for the toolbox window chrome and accordion state
- run browser validation with screenshots for the default toolbox and for filtered or scrolled search results
- confirm visible search results keep readable labels and icons after scrolling
- this repo currently uses xUnit with `Microsoft.NET.Test.Sdk`, so `mtp-hot-reload` is not available for this feedback pass
