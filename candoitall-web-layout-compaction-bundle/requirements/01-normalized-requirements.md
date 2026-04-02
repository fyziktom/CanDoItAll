# Normalized Requirements

| ID | Requirement |
| --- | --- |
| `R-01` | The app must be optimized for a maximized large-screen browser window before responsive follow-up work, using more of the available width intentionally on main routes. |
| `R-02` | The projects route must place search, all filter selects, and reset on the same large-screen toolbar row whenever the viewport is wide enough, instead of stacking them vertically. |
| `R-03` | Non-essential helper copy should be allowed to move behind a small info affordance when keeping it always visible wastes valuable space. |
| `R-04` | The initiative must analyze and compact other main pages besides `/projects`, especially repeated page-header, summary-tile, list-header, and filter-bar patterns. |
| `R-05` | Main modals and overlay dialogs must be compacted where they currently waste space, while preserving readability, open-state visibility, and actions. |
| `R-06` | Shared components should become flexible enough that common form controls stretch naturally and layout composition does not require route-specific hacks for ordinary cases. |
| `R-07` | The implementation must prefer Tailwind-prepared styles and component class hooks over ad hoc plain CSS. |
| `R-08` | Tailwind watch must be running during implementation and changes in imported files under `Tailwind/input.css` must rebuild the generated stylesheet. |
| `R-09` | Browser proof must be recorded for the affected routes and modal open states, with large-screen evidence first and narrower-width follow-up where layout changed. |
| `R-10` | The bundle must include explicit subbundles, acceptance checklists, dependencies, progression gates, and traceability before implementation proceeds. |

