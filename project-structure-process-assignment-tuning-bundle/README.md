# Project Structure Process Assignment Tuning

This bundle coordinates the follow-up tuning for the fullscreen project-structure process assignment modal.

## Profile

- `feedback`

## Mission

Refine the existing assignment modal so the fullscreen content uses the available width, the left rail starts with an `All` summary item, selecting a role opens a role-specific candidate ranking view, and every visible agent card exposes model, tool, skill, and readonly detail metadata.

## Outcome Contract

- Requested outcome: project-structure process Start opens a fullscreen assignment modal with a clear summary-review mode and a role-specific assignment mode.
- Hard constraints: keep the existing launch-plan lifecycle, reuse the existing manual agent picker for arbitrary agent selection, preserve HR-recommended candidates, and do not start until required roles remain resolved.
- Evidence required before closure: prepared and completed bundle validator output, targeted component tests, web project build, browser screenshots for summary view, role-specific candidate ranking, agent picker, details dialog, and tooltip badge behavior.
- Known blockers or explicit scope exceptions: none at preparation time. If live launch plans do not expose multiple HR candidates for the proof project, tests must still prove ordering with multiple candidates and browser proof must show the production role drilldown and plus-card picker path.

## Bundle Layout

- `inputs/` raw request and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` subbundle order and dependency map
- `traceability/` requirement-to-proof mapping
- `shared-prompts/` implementation and QA prompts
- `subbundles/` numbered workstreams
- `reviews/` self-review, execution report, screenshots, and proof JSON

## Recommended Execution Order

1. `subbundles/01-full-width-all-summary`
2. `subbundles/02-role-specific-candidate-ranking`
3. `subbundles/03-agent-metadata-badges-details`
4. `subbundles/04-browser-proof-and-closure`

## Dependency And Validation Map

- The full-width shell and `All` mode are the critical foundation for all screenshot proof.
- Role-specific ranking depends on the `All` mode split so assignment and review are separate flows.
- Metadata badges/details depend on agent catalog enrichment and the candidate-card rendering created by subbundle 02.
- Browser proof depends on all prior subbundles and must reopen earlier work if screenshots contradict the design.

## Validation Summary

- Bundle preparation status: `Ready after prepared-stage validation`
- Execution status: `Completed`
- Subbundle gate review: `All closure gates passed`
- Final closure gate: `Completed-stage validator passed`
- Browser validation analytics: `Passed with screenshots and proof JSON`
