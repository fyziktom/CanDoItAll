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

## Execution status

- Implemented on `2026-04-04` as the final CRM-HR closure gate over the current live repo.
- The final gate reused the shipped phase-specific component and integration suites and added one dedicated Playwright regression pass for the final route set instead of duplicating a second full test pyramid.
- Closure exposed and fixed one live regression in the old shell smoke: B12 introduced a second `Open directory` button on the home page, so the final gate added explicit home-page test ids and updated the smoke to target the intended header action.
- Final rollout and rollback rehearsal notes are recorded in `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\reviews\02-rollout-and-rollback-notes.md`, and browser evidence is stored under `C:\repositories\CanDoItAll\evidence\crm-hr\b13\`.

## Done when

- Component, integration, and Playwright tests exist for the final CRM/HR surface.
- Evidence folders contain screenshots plus semantic review notes.
- Fresh-db startup and seeded defaults are proven.
- The final QA gate can be executed repeatably.
