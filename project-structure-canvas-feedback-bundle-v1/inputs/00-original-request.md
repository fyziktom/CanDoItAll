# Original Request

Paste the raw user request or source note here without rewriting it.
# Original Request

## Raw Notes

1. `N001` Each node category must have different color on canvas.
2. `N001.a` Some colors must follow the common color for that topic. Examples called out explicitly: PDF asset node red, Excel node green, deployment block blue because Docker is commonly blue.
3. `N001.b` Color scheme must live as a property of the node, and Tailwind should be used for setup of colors and effects as node property.
4. `N002` Simple note must allow `Shift+Enter` for newline so notes can be multiline.
5. `N003` Each node must have one copy button for its own id and a second copy button that copies the full id structure under that node.
6. `N004` Add `Ctrl+X` and `Ctrl+V` for nodes. If a selected node has descendants, the cut and paste flow must take them too.
7. `N005` Move everything under a node into a subproject. The user called this an advanced version of existing flow `16`.
8. `N006` Change block type, at least for common blocks.
9. `N007` Add a new common computer block.
10. `N008` Change a simple note into some block, using the text in the note as title and as the basis for inside-note content.
11. `N009` Add a new common router block plus related subblocks like WiFi.

## Mandatory Directions

- Split all tasks into detailed subbundles.
- Truly test everything with Playwright MCP and screenshots.
- Especially for canvas behavior, do not skip real validation.
- Use the existing Tailwind style system and existing component libraries.
- For node colors, build maintainable architecture instead of ad hoc styling.
- The color solution should behave like other node preset parameters and stay modular.
