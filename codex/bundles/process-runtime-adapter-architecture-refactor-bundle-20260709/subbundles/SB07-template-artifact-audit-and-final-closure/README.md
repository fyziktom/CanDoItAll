# SB07 - Template Artifact Audit And Final Closure

## Status

- Status: `Completed`

## Objective

Audit templates/artifacts and close the architecture bundle with regression proof beyond the observed example process.

## Covered Inputs

- User requirement to analyze all similar process templates and artifact templates.
- GPTPro template/agent contract findings.
- Final architecture review requirements.

## Prerequisites

- SB01 through SB06 complete.
- Extracted services and domain driver isolation are production wired.

## Exact Source References

- `repo://src/Processes/CanDoItAll.Processes.Templates`
- `repo://src/Processes/Drivers`
- `repo://src/Modules/CanDoItAll.Modules.Processes`
- `repo://src/Modules/CanDoItAll.Modules.Workbench`
- `repo://codex/bundles/process-runtime-adapter-architecture-refactor-bundle-20260709/inventories/01-template-and-artifact-audit-plan.md`

## Dependency Impact

- May add template contract records or validation tests.
- Any project-reference change requires CodeAnalytics proof.

## Validation Depth

- Template inventory.
- Migration tests.
- Source assertions.
- Targeted process runtime tests.
- Build.
- CodeAnalytics final proof.
- 5032/equivalent process validation.

## Do Not Do

- Do not limit proof to Tetris or Calculator.
- Do not accept prompt-only deterministic plans when typed contracts are required.
- Do not pass final gate with pending critical proof.

## Acceptance Checklist

- [ ] Template inventory complete.
- [ ] Artifact inventory complete.
- [ ] Non-example process regression passes.
- [ ] 5032/equivalent validation recorded.
- [ ] Final C# architecture gate passes.

## Proof Required

- Proof manifest with template/artifact matrices.
- Test transcript.
- Build transcript.
- CodeAnalytics proof.
- Source assertions.
- Final architecture gate result.

## Browser Validation Logging

- Required only for process E2E paths that involve browser/UI proof.
- Capture URL/run id, screenshot/log artifact if applicable, and explicit operator validation note for 5032/equivalent flow.

## Progression Gate

- Final bundle closure requires all checklist items and `reviews/csharp-architecture-gate.md` status `Pass`.

## Suggested Agent Prompt

Implement SB07 only after SB01-SB06. Audit templates/artifacts, run regressions, and close the architecture gate with evidence.

## Goal

Prove the architecture refactor addresses the full escalation/root-cause class across process templates and artifact templates, not only the observed blocked process example.

## Scope

- Process templates.
- Artifact templates.
- Template fragments from drivers.
- Launch variable contributors.
- Acceptance criteria and required receipt metadata.
- Final regression and architecture gate.

## Implementation Steps

1. Execute the inventory plan in `inventories/01-template-and-artifact-audit-plan.md`.
2. Audit all relevant templates and artifact contracts for prompt-only deterministic plans, missing branch applicability, unresolved placeholders, file-only artifact evidence, and ambiguous acceptance/repair/blocker semantics.
3. Migrate templates to typed execution contracts where required.
4. Add template validation tests for typed tool plans, branch receipt rules, and placeholder resolution.
5. Add regression covering at least one process path that is not Tetris or Calculator.
6. Run targeted process runtime tests.
7. Run solution or affected-project build.
8. Run CodeAnalytics final snapshot and dependency/cycle check.
9. Run source assertions for partial-class and domain-boundary rules.
10. Run 5032/equivalent manual or automated process validation.
11. Complete final architecture review gate.

## C# Architecture Impact

This subbundle ensures architecture changes are not undermined by old templates or artifact contracts that still rely on prompt-only behavior or generic-domain leakage.

## Boundary Ownership

Templates may contain domain terms when they are the right owner. Generic runtime/dispatcher must consume those terms as data through typed contracts.

## Dependency Direction

Template validation may depend on contracts. It must not depend on module runtime integration implementation. Runtime must not parse template markdown prose for hard gates when typed fields exist.

## Pattern Decision

Use Builder for typed template/tool-plan metadata. Use Strategy/driver policies for domain-specific interpretation.

## Testability Contract

Required tests:

- Template with branch-specific receipt rules validates.
- Template with unresolved tool-critical placeholder fails.
- Prompt-only deterministic plan is flagged or migrated.
- Artifact template with file-only evidence is flagged.
- Non-example process regression exercises the generic route.
- 5032/equivalent process validation proves repair routing does not regress.

## Partial Class Policy

Final gate blocks if adapter partial responsibilities remain without a documented follow-up and owner. New partial files are a blocker.

## Architecture Proof Required

- Template inventory and migration decisions.
- Artifact inventory and migration decisions.
- Test transcripts.
- Build transcript.
- CodeAnalytics final snapshot id and cycle result.
- Final source assertions:
  - no new partials,
  - adapter shrink,
  - domain-free generic runtime/dispatcher,
  - no `IsDotNetRuntimeLifecycleTool` in receipt writer.
- `reviews/csharp-architecture-gate.md` completed with `Pass`.
