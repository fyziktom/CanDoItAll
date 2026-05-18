# Target Solution

- Keep the solution in the shared shell layer: `AppShell.razor`, `AppTabStrip.razor`, `MainLayoutTopBar.razor`, and Tailwind shell/tab styles.
- Use BaseLib `Split` and `Cluster` for tab-row structure, with shell-specific CSS for the large desktop no-wrap contract.
- Add navigation partition helpers in `AppShell.razor` to render standard nav items and overflow nav items deterministically.
- Render the continuation control as the final standard desktop nav item with `Icon Name="more_up"` and a fixed-position dark flyout modeled after the database switch flyout.
- Keep mobile navigation unchanged by iterating over all `NavigationItems` in the mobile panel.
- Avoid JS measurement for this pass; use browser proof to verify the item budget and open panel at the requested large desktop size.
