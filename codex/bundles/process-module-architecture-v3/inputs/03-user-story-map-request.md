# User Story Map Improvement Request

## Preserved User Instruction

The v3 architecture bundle must be improved with a user-story map derived from the actual current Process implementation, including UI/UX behavior. The running instance at `http://localhost:5032/` may be used for additional analysis.

The purpose of the story map is to track whether the new architecture covers the required functions, models, and user-facing workflows. Some future implementation details will differ because the new architecture is cleaner, but the user experience should remain recognizably equivalent where the current UI/UX direction is useful.

The current subbundle set is too broad for later automatic implementation. Complex areas, especially Process UI reconnection and rebuild, must be split into smaller subbundles with explicit validation after each complex part. Browser validation, Playwright MCP proof, and screenshots are required for browser-facing subbundles.

## Resulting Bundle Changes Required

- Add a current-implementation user-story map grounded in source files, tests, templates, and live UI inspection.
- Add a UI current-state evidence inventory that references captured Playwright snapshots and screenshots.
- Add an architecture coverage model explaining how user stories map to generic core, runtime, dispatcher, manager, templates, projections, drivers, strategies, and UI.
- Expand future subbundles from the broad SB01-SB14 roadmap into a finer SB01-SB28 roadmap.
- Split the previous broad UI subbundle into separate browser-verifiable subbundles for workspace shell, definition editing, roles, step canvas, step editor, templates, exchange/Git UI, launch planning, runtime, operator controls, evidence/assignments/messaging, analytics/live dashboard, project-scoped integration, API/tool compatibility, and final story regression.
- Update traceability, validation, reviews, prompts, and readiness checks so implementation agents can execute without losing scope.
