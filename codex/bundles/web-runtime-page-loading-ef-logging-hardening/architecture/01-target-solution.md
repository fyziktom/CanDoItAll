# Target Solution

## Design

The repair should remove eager page-init work rather than adding caches that hide expensive behavior. Each page should load the minimal data for its current visible state, then explicitly load additional data at tab-change or command boundaries.

## Processes Boundary

- Keep definition list and selected definition load in `LoadWorkspaceAsync`.
- Defer executor/workflow/manager-agent/party option calls behind explicit ensure methods.
- Defer analytics and improvement loading until the analytics section needs them or a command explicitly refreshes analytics.
- Keep template-pack loading tied to template dialogs and toolbox surfaces.

## Project Structure Boundary

- Keep persistence in the existing workbench services.
- After successful persistence, locally patch `ProjectStructureSurface` with the returned node, hierarchy link, user-authored pending links, and follow-up move positions.
- Fall back to full reload only when there is no current surface to patch, because that is an explicit missing-prerequisite condition.

## Workflows Boundary

- Remove page-init template seed invocation.
- Load workflow definitions and settings first.
- Load components/provider options through a single explicit gate used by editor/templates/analytics tabs and starter workflow creation.
- Preserve background warmup/seeding behavior outside the page.

## EF Logging Boundary

- Add `DatabaseOptions.EnableEntityFrameworkConsoleLogging` with default `false`.
- When disabled, add web-host logging filters for EF command and infrastructure categories.
- Keep the option under the existing `Database` configuration section.
