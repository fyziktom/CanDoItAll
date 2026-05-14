# OAuth Email Workflow Defaults

This bundle covers the Office365/Gmail workflow-default repair requested on 2026-05-14.

## Profile

- `feedback`

## Mission

Email workflows must use the OAuth connection already configured in Plugin settings when executor settings leave `connectionId` blank, Project Structure workflow starts must expose the same generic project-structure write skip simulation available in workflow preview surfaces, and Office365 email summaries must mark processed messages by category.

## Outcome Contract

- Requested outcome: Office365 email summary workflow starts without manual connection id copy/paste, Project Structure start dialog can skip project-structure storage writes during preview, and the Office365 workflow moves processed messages to `CanDoItAllSummaryTestProcessed`.
- Hard constraints: keep OAuth failures explicit, do not select disconnected OAuth grants, keep skip generic by executor operation, preserve existing workflow templates through managed seed refresh, and create missing processed categories explicitly.
- Evidence required before closure: targeted tests for OAuth connection resolution, project-structure simulation planning, project context fallback, component/UI behavior, build, and browser proof.
- Known blockers or explicit scope exceptions: none at preparation time.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-01-oauth-connection-defaults`
2. `subbundles/02-02-generic-project-storage-skip-preview`
3. `subbundles/03-03-office365-processed-category-and-template-settings`
4. Final validation and raw-note closure.

## Dependency And Validation Map

- See `plan/01-phase-plan.md`.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Implemented`
- Subbundle gate review: `01, 02, and 03 closure gates passed by targeted tests`
- Final closure gate: `Completed-stage validator passed`
- Browser validation analytics: `Live Project Structure page loaded with Development static assets; specific start-dialog option covered by component test`
