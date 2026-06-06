# Structured Input

## Normalized Objective

Split the process dispatch route handler implementation from nested dispatcher-private classes into module-local top-level handlers, then replace direct handler constructor dependencies on `ProcessRunAutomationDispatchService` with explicit route facets or ports.

## Hard Constraints

- Keep the work inside `CanDoItAll.Modules.Processes`.
- Do not create `CanDoItAll.Processes.Core`.
- Do not introduce production process driver APIs, driver packs, or driver registries.
- Preserve route order and all dispatch runtime behavior.
- Do not touch UI, Razor, CSS, JavaScript, TypeScript, mobile, small-screen, or medium-screen artifacts.
- Keep every subbundle represented by a distinct execution-report row.

## Execution Inputs

- Requirements: `bundle://requirements/01-normalized-requirements.md`.
- Traceability: `bundle://traceability/01-requirement-traceability.md`.
- Phase plan: `bundle://plan/01-phase-plan.md`.
- Target architecture: `bundle://architecture/01-target-solution.md`.