# Target Solution

## Window Chrome

- Use the standard `CanvasFloatingWindow` header for the blocks explorer so the window gets:
  - minimize
  - reset or normalize
  - hide
  - drag
- Restyle the toolbox-specific floating window through page-level CSS so the shared shell reads as a dark in-canvas explorer instead of a light inspector card.

## Explorer Layout

- Promote the shared window title and summary into the standard header.
- Reduce the repeated inner toolbox header so the body focuses on:
  - selected source
  - item count
  - search
  - accordion sections
- Keep the body as a single-column toolbox list rather than falling back to the old inspector stack.

## Accordion Behavior

- Keep one expanded section at a time when no search is active.
- Expand every matching section while search is active so filtering does not hide valid results.
- Make the browser-visible section body the proof target, not only the aria-expanded attribute.

## Search Validation

- Keep the toolbox results container scrollable inside the visible window height after the shared header returns.
- Extend browser coverage so the screenshots prove that labels and icons remain visible and readable in both the unfiltered and filtered search states.

## Boundaries

- Keep the blocks explorer logic page-owned.
- Reuse shared floating-window behavior instead of editing `canvas-floating-window.js` unless the current shared behavior fails the feedback in browser validation.
- Keep create action ids, selected source placement, and the existing dark toolbox item styling intact.
