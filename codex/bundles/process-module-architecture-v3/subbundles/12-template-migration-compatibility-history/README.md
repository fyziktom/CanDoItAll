# SB12 Template Migration, Existing Process Pack Compatibility, And Runtime History Plan

## Status

Future implementation package; prepared by architecture bundle v3; not executed in v3.

## Objective

Migrate/import the current `Templates/Processes` pack into canonical JSON structures, detect sidecar drift, produce branch outcome migration diagnostics, deprecate current-module projections, inventory legacy runtime history, and decide migration/archive/read-only compatibility behavior.

## Why This Bundle Exists

Current templates and historical runs are valuable. They must not be deleted or silently corrupted. This bundle turns compatibility risk into explicit reports and gates.

## Covered Inputs

- REQ-031 through REQ-037.
- REQ-050 final migration proof.
- v3 runtime history compatibility architecture.

## Context Reset: Read These First

- SB11 execution report.
- `architecture/09-template-git-versioning-and-migrations.md`
- `architecture/13-branch-switch-and-loop-contract.md`
- `architecture/17-runtime-history-migration-and-readonly-compatibility.md`
- `plan/05-review-checkpoints-and-hardening-gates.md`

## Exact Source References

- `repo://codex/bundles/process-module-architecture-v3/architecture/09-template-git-versioning-and-migrations.md`
- `repo://codex/bundles/process-module-architecture-v3/architecture/17-runtime-history-migration-and-readonly-compatibility.md`
- `repo://Templates/Processes`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackLoader.cs`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs`

## Source Evidence To Use

- `repo://Templates/Processes`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackLoader.cs`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs`
- SB01 template/runtime archive inventory.

## Prerequisites

- SB11 complete.
- Template/Git foundation complete.
- Runtime/projection compatibility contracts available.

## In Scope

- Migration dry-run for `Templates/Processes`.
- Component extraction.
- Local override representation where possible.
- Sidecar drift detection.
- Branch outcome migration diagnostics.
- Current-module projection deprecation plan.
- Template compatibility report.
- Runtime history inventory.
- Decision: full migration, archive export, read-only legacy projection adapter, or approved deletion.
- Legacy projection adapter plan or implementation depending on selected scope.

## Out Of Scope

- Do not delete templates.
- Do not keep old runtime code alive for history.
- Do not rebuild full UI.
- Do not run final E2E closure.

## Target Projects / Files

- `src/CanDoItAll.Processes.Templates`
- `src/CanDoItAll.Processes.Application`
- `src/CanDoItAll.Processes.Projections`
- migration/compatibility reports under implementation proof area.

## Deliverables

- Template migration dry-run report.
- Sidecar drift report.
- Branch migration diagnostic report.
- Runtime history inventory.
- Compatibility decision report.
- Tests for migration and legacy read-only behavior if implemented.

## Expected Deliverables

- JSON remains canonical.
- Sidecar changes are not silently lost.
- Ambiguous branch outcomes require manual resolution.
- Legacy runtime history has an explicit compatibility path.

## Dependency Impact

- SB13 UI rebuild needs compatibility projections and template catalog behavior.
- SB14 final closure depends on compatibility decision proof.

## Validation Depth

- Validate with template migration dry-run tests, sidecar drift tests, branch migration diagnostics, runtime history inventory tests, legacy read-only projection tests, and compatibility review.

## Architecture Invariants That Must Hold

- Template migrations run sequentially.
- Markdown/Mermaid/current-module projections are not canonical.
- Old runtime code is not kept alive only for history.
- Legacy history is read-only unless fully migrated.

## Performance Antipattern Notes

- Read `architecture/19-dotnet-performance-guardrails.md` and `validation/05-dotnet-performance-antipattern-checklist.md` before creating or modifying C# hot-path code.
- Record exact performance scan counts in the execution report when this subbundle changes runtime, dispatcher, manager, projection, template, Git, adapter, persistence, or UI service code.
- Do not introduce sync-over-async, unbounded event/projector queues, per-call `HttpClient`, per-call `JsonSerializerOptions`, load-all UI queries, or LINQ-heavy hot paths without a recorded mitigation and proof.
## Implementation Steps

1. Run template migration dry-run.
2. Extract components and detect overrides.
3. Detect sidecar drift and projection hash mismatches.
4. Produce branch outcome migration diagnostics.
5. Inventory runtime history records.
6. Select compatibility option with report.
7. Implement or plan read-only legacy projection adapter as approved.
8. Add tests.

## Refactoring Review Checkpoint

- Split migration scanning from migration writing.
- Split compatibility inventory from projection adapter.
- Verify reports are deterministic and reviewable.

## Required Tests / Proof

- Template migration dry-run tests.
- Sidecar drift tests.
- Branch migration diagnostics tests.
- Runtime history inventory tests.
- Legacy read-only projection/action-denial tests when adapter is implemented.

## Search Proof

- Search for Markdown/Mermaid canonical-source behavior.
- Search for old runtime code references outside archive/migration adapter.
- Search for free-text branch routing migration shortcuts.

## Stop And Report Conditions

- Stop if migration cannot distinguish canonical JSON from generated or stale sidecars.
- Stop if legacy history display requires old runtime services.
- Stop if ambiguous branch outcomes would be auto-migrated without diagnostics.

## Do Not Do

- Do not delete `Templates/Processes`.
- Do not silently discard sidecar edits.
- Do not keep old runtime code alive only for history.
- Do not auto-create free-text branch routing.

## Acceptance Checklist

- [ ] Template dry-run report exists.
- [ ] Sidecar drift report exists.
- [ ] Branch migration diagnostics exist.
- [ ] Runtime history inventory exists.
- [ ] Compatibility decision exists.
- [ ] Tests pass.

## Proof Required

- Compatibility report path.
- Test output.
- Migration review output.
- Old-symbol scan.

## Browser Validation Logging

- Browser validation is not required unless a legacy projection UI is implemented in this bundle; otherwise defer to SB13/SB14.

## Progression Gate

- SB13 may start after template and runtime history compatibility decisions are explicit.

## Suggested Agent Prompt

Execute SB12 from `codex/bundles/process-module-architecture-v3/subbundles/12-template-migration-compatibility-history`. Migrate/import templates safely and decide runtime history compatibility. Do not delete templates or preserve old runtime code as active behavior.

## Handoff Notes For Next Bundle

Record template compatibility status, legacy history decision, projection needs, unresolved manual conflicts, and UI labeling requirements for SB13.
