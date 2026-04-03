# Implementation prompt

Implement **B13 — Validation hardening, rollout, migration rehearsal, and regression suite** for CanDoItAll.

## Bundle goal

Create the final quality gate: broad automated tests, Playwright coverage, screenshot semantics, seed data rehearsal, migration verification, and rollout/rollback notes.

## Hard rules

- follow `03_ARCHITECTURE/*` and `02_REQUIREMENTS/SCOPE_AND_NON_FUNCTIONAL_DECISIONS.md`
- keep UI in BaseLib / Razor / HTML only
- do not introduce canvas components
- preserve backward compatibility for existing project/workbench flows where relevant
- add or update tests listed in `FILE_REFERENCES.md`
- add screenshot evidence requirements from `SCREENSHOT_REQUIREMENTS.md`

## Implementation steps

1. Inspect all files in `FILE_REFERENCES.md`.
2. Implement the data model / service changes required for this bundle.
3. Implement the route or UI changes required for this bundle.
4. Wire search/activity/integration seams if this bundle requires them.
5. Add automated tests at the correct level.
6. Execute browser validation and capture screenshots.
7. Write a concise evidence note summarizing code changes, tests, and screenshots.

## Bundle-specific targets

- Create the final regression suite across component, integration, and Playwright tests.
- Standardize screenshot/evidence output and semantic-review notes.
- Exercise fresh-db startup, migration, reload persistence, and cross-module proof.
- Add rollout and rollback notes for production-quality execution.

## Stories that must be satisfied in this bundle

- **X-06** As a test lead, I can validate the module with unit, component, integration, and Playwright tests so regression risk stays manageable.
- **X-07** As a test lead, I can require screenshot-based semantic review for UI changes so visual issues are not missed by passing tests.
- **X-13** As a platform owner, I can keep core screens performant with large directories so the module scales beyond toy usage.
- **X-16** As a QA inspector, I can trace every user story to an implementation bundle, validation step, and evidence expectation so execution stays accountable.

## Stop conditions

Do not mark this bundle done until all acceptance criteria pass and the screenshot evidence is semantically reviewed.
