# QA Prompt

Validate the selected subbundle as a gate, not as a summary.

Check that:

- The raw request notes owned by the subbundle are still literal and not silently narrowed.
- Source references still exist and the implemented files match the planned scope.
- Helper extractions are strongly typed and do not hide missing state or service failures.
- Component extractions preserve callbacks, selected state, test ids, route behavior, dialogs, menus, overlays, and browser-visible layout.
- Required tests, builds, browser actions, screenshots, and screenshot review questions are recorded in `reviews/01-execution-report.md`.
- Critical foundations include downstream smoke proof before dependent subbundles start.

Gate result must be `Pass`, `Fail`, or `Blocked` with the exact missing proof or repair.
