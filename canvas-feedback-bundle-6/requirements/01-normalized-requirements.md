# Normalized Requirements

- `R001` Progress submenu items must render their value inside the circular progress icon, with no center text for `Start` and visible center text for percentage and `N/A` entries.
- `R002` Progress submenu geometry must be enlarged enough that neighboring progress preset hexes do not overlap.
- `R003` Marker submenu geometry must be enlarged enough that neighboring marker preset hexes do not overlap.
- `R004` Nested submenu layers must never render underneath the toolbar and must remain fully visible within the available canvas host area.
- `R005` Opening second and third submenu layers must require an observable hover delay of about `500ms`, with a visible loading-circle indicator during the wait.
- `R006` Compact submenu composition must read as a hive-style staggered hex layout rather than a simple ring.
- `R007` Existing progress, marker, and priority commands must remain clickable and continue applying the correct node metadata after the layout changes.
- `R008` Completion requires browser screenshots and focused automated proof for timing, placement, and overlap-sensitive behavior.
