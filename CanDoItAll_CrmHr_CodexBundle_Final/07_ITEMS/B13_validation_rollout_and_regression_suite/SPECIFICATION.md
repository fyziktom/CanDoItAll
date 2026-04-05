# Specification

## Objective

Create the final quality gate: broad automated tests, Playwright coverage, screenshot semantics, seed data rehearsal, migration verification, and rollout/rollback notes.

## Scope

- Create the final regression suite across component, integration, and Playwright tests.
- Standardize screenshot/evidence output and semantic-review notes.
- Exercise fresh-db startup, migration, reload persistence, and cross-module proof.
- Add rollout and rollback notes for production-quality execution.

## Services and entities involved

**Services**

- `All CRM/HR services plus existing test infrastructure`

**Entities / concepts**

- `n/a`

## Bundle-specific implementation notes

1. Follow the global architecture documents first.
2. Keep the module inside `CanDoItAll.Modules.CrmHr` unless the file reference list explicitly points to another module for integration changes.
3. Reuse the existing CanDoItAll services listed in `FILE_REFERENCES.md` instead of inventing parallel registries or orchestration layers.
4. Keep database changes additive and backward compatible where Workbench or existing modules already persist data.
5. Any UI added here must stay inside BaseLib + normal Razor patterns.

## Detailed functional outcomes

- **X-06** As a test lead, I can validate the module with unit, component, integration, and Playwright tests so regression risk stays manageable.
- **X-07** As a test lead, I can require screenshot-based semantic review for UI changes so visual issues are not missed by passing tests.
- **X-13** As a platform owner, I can keep core screens performant with large directories so the module scales beyond toy usage.
- **X-16** As a QA inspector, I can trace every user story to an implementation bundle, validation step, and evidence expectation so execution stays accountable.

## Out of scope inside this bundle

- Bundles that are listed as dependencies but handled elsewhere stay out of this bundle.
- Do not prematurely solve later-wave concerns unless the dependency chain requires a small seam.
- Do not introduce payroll, marketing automation, or canvas-based UI work here.

## Definition of success

- Component, integration, and Playwright tests exist for the final CRM/HR surface.
- Evidence folders contain screenshots plus semantic review notes.
- Fresh-db startup and seeded defaults are proven.
- The final QA gate can be executed repeatably.
