# Structured Input

## Raw Notes

- `N001` Put the progress text inside the icon for submenu items such as `10%`, `20%`, and `N/A`; keep `Start` without center text.
- `N002` Increase progress submenu diameter so hexagons no longer overlap.
- `N003` Increase marker submenu size so marker items no longer overlap.
- `N004` Keep second-layer menus out of the toolbar and fully visible within the available canvas space.
- `N005` Add an approximately `500ms` hover delay before opening second and third menu layers, with a small loading circle that fills while waiting.
- `N006` Tune the hexagon composition into a bees-hive layout inspired by the reference screenshot, without copying the same visual style.

## Derived Expectations

- The progress submenu must remain recognizable as a progress ring and the ring center must carry the percentage or `N/A` text at submenu scale.
- The larger submenu sizes must be reflected in actual menu metrics, not only in screenshots.
- The hover-delay indicator must be visible before submenu expansion and cancellable when the pointer leaves early.
- Toolbar-safe clamping must work for nested layers, not just the root radial menu.
- Browser proof must cover layout, delay, and visibility; DOM-only assertions are insufficient for this feedback.

## Assumptions

- A hover delay close to `500ms` is acceptable if browser timing variance lands slightly above or below the target.
- `Start` can keep the play glyph without any center text, matching the raw note.
- The hive inspiration applies to spacing and staggering of submenu hexes, not to a gold-outline or game HUD visual theme.
- Priority submenu sizing can stay smaller than progress and marker, as long as its layout remains clean and consistent with the revised hive geometry.
