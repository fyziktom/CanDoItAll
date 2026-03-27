# Structured Input

## Core Objective

- Improve the installed bundle skills so future feedback-driven workflow runs catch reference-quality issues, stale bundle status reporting, and misuse of `mtp-hot-reload`.
- Keep the changes local to the skill files and validator script unless the bundle itself needs documentation updates.

## Hard Constraints

- Do not weaken the raw-feedback closure rules added after the QA inspection run.
- Do not make the validator so strict that a prepared-but-not-executed bundle becomes invalid.
- Keep `mtp-hot-reload` optional and gated on actual Microsoft Testing Platform usage.

## Source Artifacts

- `inputs/00-original-request.md`
- `inputs/01-source-artifacts.md`
- `inputs/03-audit-findings.md`

## Working Assumptions

- Absolute-path bullets under `## Exact Source References` are the primary place where automated reference validation should apply.
- Feedback-profile bundles should always carry `## Status` and `## Raw Note Closure` scaffolding in `reviews/01-execution-report.md`.

## Primary Risks

- Over-validating markdown could reject older bundles that were prepared before the stricter rules existed.
- Future agents could misuse `mtp-hot-reload` as if it were equal to a clean standard proof run.
