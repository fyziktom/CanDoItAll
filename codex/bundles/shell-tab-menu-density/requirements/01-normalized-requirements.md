# Normalized Requirements

| Requirement | Source notes | Acceptance |
| --- | --- | --- |
| `R001` Large desktop tab chrome density | `N001`, `N002` | At the desktop shell breakpoint (`xl` / 1280px and wider), the tab row contains inline tabs, search, tab overflow/reopen controls, and top-bar status badges without a second search/status row. |
| `R002` Conservative smaller layouts | `N001`, `N002` | Below the large desktop breakpoint, the existing safe wrapping/stacking behavior remains allowed. |
| `R003` Sidebar height and no nav scrolling | `N003` | Desktop sidebar is viewport-limited on standard pages and focus pages, and primary nav no longer uses internal vertical scrolling. |
| `R004` Continuation menu item | `N004` | Overflow nav items are accessed through a final standard menu item using `more_up`, opening on hover/focus. |
| `R005` Continuation panel cards | `N005` | Overflow items render as compact square icon cards with centered icon and one-word label, arranged in a max-three-row grid that expands columns. |
| `R006` Existing navigation behavior preserved | `N003`, `N004`, `N005` | Existing routes, active-state matching, badges, and mobile navigation remain available. |
