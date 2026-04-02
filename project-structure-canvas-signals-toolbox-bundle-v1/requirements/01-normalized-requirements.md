# Normalized Requirements

## Objectives

- `R001` Increase the second-layer context-menu marker glyph size substantially, targeting roughly three-times current visual prominence, without enlarging the surrounding circular badge.
- `R002` Add a toolbar-triggered floating toolbox over the canvas for node signals.
- `R003` The toolbox must work from the current selection and make it obvious which node or node set will receive the change.
- `R004` Clicking a marker, progress, or priority option in the toolbox must immediately apply it to the selected node or selection.
- `R005` Nodes must support multiple markers at the same time.
- `R006` The canvas must visibly render multiple markers on nodes instead of silently storing them.
- `R007` Existing single-marker consumers must remain compatible through a primary-marker bridge instead of a breaking migration.
- `R008` The toolbox composition should take inspiration from XMind-style grouped palettes, but not copy the same visual style.

## Hard Constraints

- `C001` Do not increase the marker badge circle size in the right-click menu.
- `C002` Add the toolbox trigger to the top canvas toolbar.
- `C003` Keep browser-visible proof tied to the project-structure canvas route.

## Proof Expectations

- `P001` Browser proof for enlarged second-layer marker glyphs with screenshot evidence.
- `P002` Browser proof that the floating toolbox opens from the toolbar, shows selection context, and stays fully visible.
- `P003` Browser proof that applying at least two markers to the same node leaves both markers visible on the node.
- `P004` Focused automated validation for the marker metadata compatibility path.
