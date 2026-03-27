# Target solution

## Architecture decision

Keep the first-pass shared fixes, then finish the missed runtime behavior with the smallest additional shared changes plus page-local toolbox and preview corrections.

## Shared changes

1. Extend the shared icon catalog and load the Font Awesome stylesheet in the app shell so requested tokens and toolbox file icons render in the browser.
2. Keep floating-window action buttons icon-only and black with explicit `aria-label` and `title` attributes.
3. Let `CanvasFloatingWindow` run in a headerless mode and switch its grid to a body-only layout when the standard header is suppressed.

## Page-local changes

1. Render the project structure toolbox as a single owned dark surface with `ShowHeader="false"`, explicit accordion state, and a bounded height.
2. Open matching toolbox groups during search and render item icons through the shared icon pipeline instead of raw text tokens.
3. Resolve file-node palettes from subtype in the graph adapter.
4. Raise the preview backdrop above the maximized workbench shell.

## Validation strategy

1. Keep component coverage for deterministic markup/state assertions.
2. Use targeted Playwright coverage for runtime icon rendering, toolbox behavior, and preview layering.
3. In transformed workbench surfaces, prefer asserting visible content movement and screenshot evidence over brittle raw scroll-metric assumptions.
