# Original Request

Date captured: `2026-04-01`

Raw request preserved from chat:

> Use the `candoitall-bundle-workflow` and `frontend-skill` skills to improve layouts of main pages and modals in the CanDoItAll web app.
>
> Optimize first for a maximized browser window on a large screen.
>
> Example: the projects page screenshot does not use the available space efficiently. All filter selects should be on the same row, ideally next to the search bar, and the reset button should also fit on the same row on large screens to save height.
>
> This is only one example. During bundle preparation, analyze other pages and think how to make the UI more compact.
>
> One tip: hide some texts behind a tiny `?` icon in a blue circle and show the text in a tooltip on hover. Example text: `Search, inspect, edit, and jump into dashboard, structure, or calendar from one compact board without losing the project list.`
>
> After analyzing all pages and their modals, create subbundles and checklists for each subbundle so the implementation agent covers everything well.
>
> When the bundle is ready, execute it through the bundle flow.
>
> If some components are missing expected layout flexibility, tune them. Example: text edit controls should try to stretch to the full available width by default and only be constrained by callers when necessary.
>
> Prefer improving Tailwind-prepared styles or using component `Class` hooks instead of pure CSS. Assure that Tailwind watch is running and that changes propagate correctly from imported files in `input.css`.

## Embedded Screenshot Note

- The request included a browser screenshot of `/projects` on a large desktop viewport.
- The screenshot shows:
  - the shell already consuming a wide sidebar plus a right rail
  - a large top bar with an oversized active-database card
  - a `Project workspace` header area before the board
  - the projects board using a tall intro and vertically stacked search plus three filters plus reset
  - large unused space to the right of the stacked filters while the first-screen vertical budget is already consumed

