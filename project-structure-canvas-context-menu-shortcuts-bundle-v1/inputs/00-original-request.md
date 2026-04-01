# Original Request

Date captured: `2026-04-01`
Requested workflow: use `candoitall-bundle-workflow` to prepare, validate, execute, and close the bundle for the canvas right-click menu keyboard behavior.

## Main Goals

- Simplify the orientation in the menu just from the keyboard.
- With simple one-letter shortcuts it will be possible to select something in the menu.

## Generic Principle

- When I open right click menu and then press the key it opens specific second layer menu.
- In second/third layer we will use shortcuts too.

## Recommended Shortcuts

- `b` -> `Blocks`
- `d` -> `Delivery block`
- `b` -> `Backlog`
- `s` -> `Support`
- `f` -> `Feature`
- `a` -> `Asset`
- `p` -> `PDF`
- `e` -> `Excel`
- `w` -> `Word`
- `j` -> `JSON`
- `t` -> `Text`
- `m` -> `Markers`
- `q` -> `Question mark`
- `e` -> `Exclamation mark`
- `p` -> `People`
- `i` -> `Infrastructure`
- `n` -> `Note`
- `q` -> `Meetings`
- `s` -> `Onsite`
- `o` -> `Online`
- `w` -> `Work`
- `t` -> `Task`

## Other Notes From Architect

- We should add section of this into help modal.
- Help modal should have some better structure like pages where you can browse basic docs.
- If we use text in the right click menu item, it is nice to underscore the letter that is used for that shortcut.
- I did not mention all, add others shortcuts to other possible options of right menu too.
- I think `src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/03-interaction-and-state.js` is already quite large file. If possible split it to logical parts for better maintainability.
