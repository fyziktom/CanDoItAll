# Structured Input

## Core Objective

- Fix the visible regressions from the first agent integration wave and complete a real template-driven end-to-end showcase in the requested managed profile database until the workflow demonstrably works.

## Hard Constraints

- Use `candoitall-bundle-workflow`.
- Prepare and validate the bundle before implementation.
- Use the template system for processes and related setup. Do not hardcode showcase process definitions.
- Keep the bundle open until the live run passes end to end, including artifacts, QA, and project-structure updates.

## Source Artifacts

- Use the files listed in `inputs/01-source-artifacts.md`.
- Use the requested showcase database at `C:\Users\lucys\AppData\Local\CanDoItAll\control-plane\database-profiles\managed-sqlite\529c12060808489fad29feb5bc60dda1\db\candoitall.db`.
- Use the collected code analytics snapshot `snap-20260415205622-d225a84b` as read-only architectural evidence.

## Input Coverage Signals

- `U001` CRM-HR agent inventory mismatch.
- `U002` Processes page scroll regression.
- `U003` Missing copy buttons for database profile paths.
- `U004` Full showcase provisioning, execution, bug harvest, and bundle recording using the requested database.

## Dependency And Sequencing Signals

- Agent source alignment is a foundation because the showcase later depends on CRM-HR sourcing agents correctly.
- Processes and database UX fixes are a second foundation because manual and browser validation rely on them.
- Template-driven provisioning must complete before the live run.
- The live run is the final closure gate and can reopen earlier subbundles.

## Validation Expectations

- Targeted tests for service and component changes.
- Browser proof for `/agents`, `/crm-hr/agents`, `/processes`, and the database dialog.
- Concrete provisioning evidence in the requested database.
- Concrete runtime evidence that the calculator-delivery workflow completed with artifact handoffs and QA coverage.

## UI Validation Strategy

- Run a large-screen browser pass at approximately `1600x900` or larger for all affected pages.
- Re-run at a narrower width only where layout or overflow behavior changes.
- Review screenshots for count convergence, scroll containment, copy affordance visibility, and progress-state accuracy.

## Browser Validation Analytics

- Each subbundle must record route, viewport, Playwright or browser actions, screenshot paths, and pass or fail result in `reviews/01-execution-report.md`.

## Working Assumptions

- The calculator showcase can use unique names to avoid colliding with existing user data.
- Existing template assets are close enough to model the requested delivery flow with extensions instead of replacement.

## Primary Risks

- CRM-HR may still be implicitly coupled to party-backed AI agents in later code paths beyond directory listing.
- The live run may expose gaps in runtime binding, artifact exchange, or project-structure projection that require additional code beyond the three visible regressions.
