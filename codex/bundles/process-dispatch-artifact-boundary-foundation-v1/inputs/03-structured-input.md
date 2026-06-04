# Structured Input

## Objectives

- Continue decomposing `ProcessRunAutomationDispatchService` without full Process Core extraction.
- Isolate artifact projection and validation responsibilities behind smaller services/helpers.
- Preserve all process runtime behavior and evidence semantics.
- Keep the branch safe for longer Codex execution by adding phase gates and source scans.

## Hard Constraints

- Do not create `CanDoItAll.Processes.Core` in this bundle.
- Do not create process driver packs.
- Do not move EF entities or Razor/UI view models.
- Do not rename or remove process runtime tools.
- Do not weaken required artifact, receipt, lineage, approval, or trust-status behavior.
- Do not reintroduce MAF -> product module dependencies.
- Do not run small/medium/mobile viewport validation.

## Success Criteria

- New artifact helper/planner/validation seams are covered by focused unit tests.
- At least one concrete artifact projection path is migrated through the new seam.
- Existing artifact-lineage and required-tool integration tests continue to pass.
- Dispatcher partials become easier to reason about without broad rewrites.
