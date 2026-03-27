# Normalized requirements

## R1. Section the create dialog into explicit steps

- Split the shared canvas create dialog into logical sections.
- Present the sections as a wizard-style surface inside the existing canvas dialog.
- Keep the existing create flows strongly typed and selector-stable.

## R2. Keep the submit path visible on long forms

- The create dialog must constrain its height.
- The body content must scroll internally.
- The action bar must stay reachable without forcing the user to hunt for the submit button.

## R3. Keep the toolbox as a practical single-surface explorer

- The standard blocks toolbox must behave as a single-column explorer surface inside its floating window.
- The page must stop inheriting the shared two-column toolbox body layout.

## R4. Replace text window actions with icons only

- Shared floating window action chrome must use icons instead of text.
- Accessibility labels must remain explicit.

## R5. Use the requested window icon tokens

- Minimize uses `minimize`.
- Expand uses `expand`.
- Reset uses `restart_alt`.
- Hide uses `visibility_off`.

## R6. Make the window action icons visibly render in black

- The action icons must render as actual icons in the browser.
- Their visible color must be black.
- The result must be captured in a screenshot.

## R7. Remove duplicate toolbox chrome

- The toolbox must read as one dark owned surface.
- The outer white floating-window copy must not duplicate the inner toolbox title and summary.

## R8. Make toolbox sections behave as an accordion in the browser

- Clicking a toolbox section must open its items.
- Search must open matching groups automatically.

## R9. Keep toolbox search results usable

- Search results must remain scrollable with real wheel input.
- Icons inside searched items must render as icons, not token text.

## R10. Restore file-node background colors

- File nodes must resolve palette colors from subtype.
- PDF, spreadsheet, document, diagram, and log-style files must no longer collapse to the same generic palette.

## R11. Keep maximized PDF previews above the canvas

- Double-clicking a PDF while the canvas is maximized must open the preview above the shell.
- Validation must use the maximized canvas state, not the default state.
