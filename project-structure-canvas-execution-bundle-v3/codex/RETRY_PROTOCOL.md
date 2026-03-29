# Retry protocol

When a validation gate fails:

1. identify the smallest failing behavior,
2. map it to the impacted feature IDs,
3. fix the implementation,
4. rerun the smallest relevant test scope first,
5. rerun the browser/screenshot scope if UI behavior changed,
6. rerun the performance check if hot-path code changed,
7. repeat until green.

Never leave a task in a partially failing state.
