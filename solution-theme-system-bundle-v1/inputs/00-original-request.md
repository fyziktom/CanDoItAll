# Original Request

## Raw User Request

- Use the `candoitall-bundle-workflow` skill to prepare and execute a bundle for improving and refactoring styles across the whole solution to add color schemes.
- Build a shared system of basic colors and theme basics such as button rounding.
- Define the system via Tailwind CSS.
- Ensure component styles such as buttons use the new color themes.
- Ensure projects consuming `CanDoItAll.Components.BaseLib` as a NuGet package can define and override their own color theme.
- Use UI best-practice semantic colors such as primary, secondary, and danger.
- Existing shared colors such as `cda-button--tone-primary` already point in the right direction, but the user is unsure whether changing a single primary color would update all matching surfaces consistently.
- Stabilize naming prefixes around styles. Prefixes such as `zy-sheet-toolbar-actions` are not acceptable for the shared non-canvas style system. The user wants at least `cad-*`.

## Mandatory Workflow Notes

- Analyze first.
- Produce lists and Excel-style inventories of the work that must be done.
- Create an architecture subbundle and then critically validate that architecture as a senior QA Tailwind specialist. Improve the architecture if concerns appear.
- Create a subbundle for each implementation step and map all touched places so nothing is missed.
- Execute only after the architecture and subbundle map are ready.
- Validate after execution.

## Required Validation Notes

- First prove the system with a simple dark theme and runtime theme switching.
- Confirm, but do not yet implement, that the same contract can be used by future Zyphonote server and WebAssembly apps once they move onto BaseLib components.
