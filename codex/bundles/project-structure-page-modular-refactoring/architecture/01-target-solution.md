# Target Solution

The page remains the Blazor orchestration facade, but two deterministic responsibilities move to top-level Workbench owners. Both UI and agent process-launch paths share one immutable context builder, hierarchy candidate rules live in one policy, and tests exercise both without constructing the page.

This is a local extraction, not a project-boundary extraction. The acceptance bar is fewer page responsibilities, deleted duplicate logic, unchanged observable output, no new partials/references, and direct tests that would fail a shallow wrapper.
