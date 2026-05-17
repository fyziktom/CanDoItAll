# Current State

- `AppShell.razor` owns desktop sidebar rendering, mobile navigation, tab strip placement, top-bar placement, body height behavior, and shell mode styles.
- `AppTabStrip.razor` renders inline tabs and places search/overflow/reopen controls in a `Split` that can stack below the tab row.
- `MainLayoutTopBar.razor` renders workspace, route, project, server, live item, and tab-count status badges.
- `Tailwind/navigation/workbench-shell.css` sets the sidebar to `overflow-hidden`, the nav to `overflow-y-auto`, and the database switcher to a fixed hover flyout.
- `Tailwind/navigation/tabs.css` contains the tab strip, search field, overflow menu, and active tab summary styles.
- Component tests already cover basic `AppTabStrip` actions but do not cover one-row shell density or sidebar overflow rendering.
