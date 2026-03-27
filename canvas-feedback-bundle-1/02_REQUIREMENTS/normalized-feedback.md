# Normalized requirements

## R1. Section the create dialog into explicit steps

- Split the shared canvas create dialog into logical sections.
- Present the sections as a wizard-style surface inside the existing canvas dialog.
- Keep the existing create flows strongly typed and selector-stable.

## R2. Keep the submit path visible on long forms

- The create dialog must constrain its height.
- The body content must scroll internally.
- The action bar must stay reachable without forcing the user to hunt for the submit button.

## R3. Fix project structure toolbox scrolling

- The standard blocks toolbox must scroll inside its floating window.
- The body layout must stop inheriting the shared two-column toolbox grid when the page only renders one column.
- The result should feel denser and more explorer-like.

## R4. Replace text window actions with icons only

- Shared floating window action chrome must use icons instead of text.
- Accessibility labels must remain explicit.

## R5. Use the requested icon tokens

- Minimize uses `minimize`
- Expand uses `expand`
- Reset uses `restart_alt`
- Hide uses `visibility_off`
