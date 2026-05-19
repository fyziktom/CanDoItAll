# Assumptions And Risks

## Working Assumptions

- The UI should keep using existing CanDoItAll BaseLib components because the page already depends on them.
- A single bounded snapshot request is acceptable if every collection is independently paged and no query materializes the full dataset first.
- The new quality operations can run from the page with the current operator policy and project scope.
- Large-screen proof should use a desktop viewport at or above 1600px wide.

## Critical Path Risks

- Adding per-collection paging can touch many query methods and tests.
- Dream runs and cluster planning can create data, so the UI must present dry-run and persisted execution clearly.
- Aggregate application can mutate canonical memory and must be limited to approved candidates.
- Existing generated quality entities may not have UI-friendly list contracts yet.

## Validation Risks

- bUnit tests can prove markup and actions but not final layout fit.
- Browser proof is required because this is UI work.
- Imagegen proposals are not validation evidence.

## Reopen Triggers

- Build or tests fail after paging contract changes.
- Browser proof shows overflow, hidden pager controls, unreadable dense rows, or tab content clipping at large desktop.
- Completed bundle validation fails because closure artifacts drift from implementation.
