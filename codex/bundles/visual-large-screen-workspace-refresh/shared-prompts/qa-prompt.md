# QA Prompt

```text
Review the selected subbundle implementation for the Large Screen Visual Workspace Refresh.

Focus:
- Large desktop visual quality, width usage, hierarchy, spacing, and B2B clarity.
- Collapsed shell behavior, right-side tooltips, bottom Settings/DB actions, and DB flyout open state.
- TreeView behavior for projects/processes/workflows where applicable.
- Page-input function coverage: current actions, tabs, dialogs, and UX flows remain reachable.
- Accepted `imagegen` proposal intent is reflected without copying generated fake text.
- Dialog/flyout use for dense secondary information.
- No new page-local custom CSS or non-reusable styling hacks.
- No accidental loss of existing navigation, database switching, settings, dialogs, or route state.

Required proof review:
- Compare large-screen screenshots against the two reference screenshots in inputs/.
- Confirm overlay screenshots show no clipping, lateral overflow, or layering defects.
- Confirm each changed tab and dialog has its own screenshot/proof row.
- Confirm every changed route has a browser analytics row.
- Confirm raw notes are closed as Solved, Partially solved, or Not solved with proof.
```
