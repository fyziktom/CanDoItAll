# B13 — Validation hardening, rollout, migration rehearsal, and regression suite

## Purpose

Create the final quality gate: broad automated tests, Playwright coverage, screenshot semantics, seed data rehearsal, migration verification, and rollout/rollback notes.

## Dependencies

B01, B02, B03, B04, B05, B06, B07, B08, B09, B10, B11, B12

## Main stories covered

- **X-06** As a test lead, I can validate the module with unit, component, integration, and Playwright tests so regression risk stays manageable.
- **X-07** As a test lead, I can require screenshot-based semantic review for UI changes so visual issues are not missed by passing tests.
- **X-13** As a platform owner, I can keep core screens performant with large directories so the module scales beyond toy usage.
- **X-16** As a QA inspector, I can trace every user story to an implementation bundle, validation step, and evidence expectation so execution stays accountable.

## Main routes

- `/crm-hr`
- `/projects`
- `/activity`
- `/resources`
- `/validation`
- `/test-lab`

## Done when

- Component, integration, and Playwright tests exist for the final CRM/HR surface.
- Evidence folders contain screenshots plus semantic review notes.
- Fresh-db startup and seeded defaults are proven.
- The final QA gate can be executed repeatably.
