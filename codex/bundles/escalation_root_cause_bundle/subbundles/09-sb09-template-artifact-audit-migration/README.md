# SB09 - Template And Artifact Audit Migration

## Status

- `Completed`
- Critical foundation: yes

## Objective

Audit and migrate the full source-controlled process and artifact template set so the repair covers the systemic failure class, not only the blocked calculator example. Every hard gate must be typed, already enforceable, explicitly exempt, or blocked with source-backed justification.

## Covered Inputs

- User requirement to analyze all process templates, artifact templates, and similar trouble areas.
- GPTPro template/agent combination analysis.
- REQ-012, REQ-015, REQ-016, REQ-017, REQ-018, REQ-020.
- Inventories for process templates and artifact templates.

## Prerequisites

- SB08 strict template validation complete.
- SB06 subprocess bridge semantics complete.
- SB05 artifact acceptance semantics complete.
- SB07 deterministic .NET setup guard complete.

## Exact Source References

- `bundle://analysis/04-template-agent-combination-analysis.md`
- `bundle://inventories/02-process-template-inventory.md`
- `bundle://inventories/03-artifact-template-inventory.md`
- `bundle://templates/01-template-audit-index.md`
- `bundle://templates/02-template-contract-migration-checklist.md`
- `repo://Templates/Processes/processes`
- `repo://Templates/Processes/processes/dotnet-solution-setup/definition.json`
- `repo://Templates/Processes/processes/dotnet-development-slice/definition.json`
- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://Templates/Processes/processes/blazor-app-delivery/definition.json`
- `repo://Templates/Processes/processes/app-page-screenshot/definition.json`
- `repo://Templates/Processes/processes/business-plan-development/artifacts/business-plan.json`
- `repo://Templates/Processes/manifest.json`

## Deliverables

- Full audit table for all 24 process definitions, 155 step markdown files, 30 validation JSON files, 30 prompt JSON files, and six artifact JSON templates.
- Migrated typed execution contracts for every high-risk process definition.
- Typed hard gates for required receipts, deterministic tool plans, product readbacks, subprocess accepted/no-go outputs, artifact slots, and branch decisions.
- Explicit exception rows for templates/artifacts with no hard runtime/proof gate.
- Updated template tests proving strict validation over the full pack.

## Dependency Impact

- SB12 final validation depends on full migration proof.
- Missed templates can reproduce the escalation class in non-calculator flows.
- SB10 capability assignment depends on migrated execution class/tool metadata.

## Validation Depth

- Critical foundation with full-pack template validation, fixture negatives, and migration audit proof.
- Semantic proof must show every user-requested template/artifact scope was inspected and closed.

## Implementation Steps

1. Generate a fresh file inventory for `Templates/Processes`.
2. For each process definition, record execution class coverage and hard gate coverage.
3. For each step markdown file, identify hard requirements that must be mirrored into typed metadata.
4. For each validation and prompt JSON file, confirm it does not own a hard runtime gate without typed template support.
5. Migrate `dotnet-solution-setup` first because it is the concrete incident child.
6. Migrate parent subprocess templates including `dotnet-development-slice`, `software-delivery`, `dotnet-feature-function-implementation`, `dotnet-runtime-command-writeback`, `dotnet-ui-screenshot-writeback`, and `dotnet-architecture-design-review`.
7. Migrate Blazor delivery/repair templates and screenshot templates for runtime/tool receipt proof.
8. Audit six business artifact templates for semantic completion and accepted artifact slots.
9. Remove or demote prose-only hard gates after typed equivalents exist.
10. Run strict full-pack validation and record every exception.
11. Add tests that remove a required typed field from representative templates and prove validation fails.
12. Update proof with the full audit disposition table.

## Do Not Do

- Do not edit only `dotnet-solution-setup`.
- Do not leave hard gates only in markdown because the prose is clear.
- Do not mark templates "out of scope" without source-backed exception.
- Do not make strict validation optional for the migrated pack.

## Acceptance Checklist

- [x] All 24 process definitions have audit disposition.
- [x] All 155 step markdown files have audit disposition.
- [x] All validation/prompt JSON files in scope have audit disposition.
- [x] All six artifact templates have semantic acceptance disposition.
- [x] High-risk templates are migrated to typed contracts.
- [x] Strict template validation passes for the full migrated pack.
- [x] Negative fixture tests fail when typed hard gates are removed.

## Proof Required

- `proof/SB09/manifest.md`
- `proof/SB09/semantic-invariants.md`
- Full template audit table.
- Full-pack strict validation transcript.
- Failing-first missing-typed-gate tests.
- Changed-file hashes for every migrated template.
- Anti-stub audit proving hard gates are not prose-only.

## Browser Validation Logging

- `N/A`; no browser surface is changed.

## Progression Gate

- SB12 may start only after every process and artifact template has a closed audit row and strict validation passes.

## C# Architecture Impact

Mostly template data migration, but may require validator adjustments if gaps are found.

## Boundary Ownership

Templates own data; `Processes.Templates` owns validation; runtime consumes normalized typed contracts.

## Dependency Direction

Migration must not introduce runtime markdown parsing or template implementation dependencies in runtime.

## Pattern Decision

Use the schema validators from SB08; do not introduce new patterns unless a repeated validation concern appears.

## Testability Contract

Tests must load real templates and representative invalid fixtures.

## Partial Class Policy

No partial-class changes expected.

## Architecture Proof Required

- Full audit table and strict validation proof.
- Exception rows with source-backed reasoning.

## Suggested Agent Prompt

```text
Execute SB09 only. Audit and migrate the entire process and artifact template set. Do not narrow to the calculator example. Produce a full disposition table and strict validation proof for all process definitions, steps, prompt/validation JSON, and artifact templates.
```
