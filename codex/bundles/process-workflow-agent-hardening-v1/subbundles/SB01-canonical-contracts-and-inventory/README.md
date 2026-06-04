# SB01 - Canonical Contracts And Inventory

## Status

Completed. Classification: **Critical foundation**.

## Objective

Create the canonical contract foundation before any large refactor. Identify which identifiers, enum values, JSON paths, tool ids, executor ids, process operations, proof rules, and usage statuses are internal canonical contracts versus external/template/test boundaries.

## Covered Inputs

Covers canonicity drift, string-key/JSON-path surface, numeric enum shape, duplicated template/runtime/skill/UI rules, and the requirement to harden before adding more processes/features.

## Prerequisites

None. This is the first subbundle. Rerun `python scripts/validate_bundle.py --stage prepared` before starting.

## Exact Source References

- `repo://codex/bundles/chatgpt-pro-process-workflow-agent-hardening-inputs-v1/analysis/02-observed-weak-spots.md`
- `repo://codex/bundles/chatgpt-pro-process-workflow-agent-hardening-inputs-v1/inventories/01-hotspot-files-and-apis.md`
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.*.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.cs`
- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://Templates/Processes/seed-catalog/baseline-scenarios.json`
- `repo://codex/skills/candoitall-api-processes/SKILL.md`
- `repo://codex/skills/candoitall-api-workflows/SKILL.md`
- `bundle://inventories/02-string-key-json-path-inventory.md`

## Deliverables

- Canonical contract inventory document or code descriptors.
- Drift scanner test/helper.
- Classification table for internal vs external/template/test literals.
- Baseline drift report.
- Initial constants/descriptors where low-risk.
- Proof manifest and semantic invariants for SB01.

## Dependency Impact

All downstream subbundles depend on SB01. If SB01 is wrong, later refactors can move duplicated rules into new duplicated places.

## Validation Depth

Deep semantic validation. Must include scanner negative/positive tests and prove the scanner rejects an unowned internal id while accepting classified external/template/test literals.

## Implementation Steps

1. Read all source references.
2. Inventory process operation ids, target scopes, artifact statuses, tool ids, browser tool ids, workflow executor ids, provider/capability statuses, usage statuses, and enum display mappings.
3. Classify every literal as internal canonical, external boundary, template content, UI label, or test fixture.
4. Add or extend code descriptors/constants for internal canonical ids.
5. Add a scanner or tests that detect unowned internal ids in scoped files.
6. Produce `proof/SB01/contract-drift-report.md`.
7. Update downstream source references if files are renamed or moved.

## Scope Exceptions

Do not attempt to fully refactor dispatch/UI in this subbundle. Only make low-risk descriptor additions and scanner/test foundations.

## Do Not Do

- Do not rename public API fields unless a compatibility adapter is included.
- Do not hard-code Tetris as a canonical app-generation concept.
- Do not mark a literal as external just to avoid refactoring it.
- Do not skip template JSON validation.

## Acceptance Checklist

- [x] Contract inventory exists.
- [x] Drift scanner exists.
- [x] Scanner has positive and negative tests.
- [x] Internal ids are behind descriptors/constants or listed as temporary exceptions.
- [x] Template ids are validated against descriptors.
- [x] SB01 proof manifest exists.
- [x] Downstream reopen triggers are documented.

## Proof Required


Because this is a critical subbundle, the Semantic Adequacy Gate proof must include:

- `proof/SBxx/manifest.md`
- `proof/SBxx/semantic-invariants.md` or `.json`
- changed-file hashes
- command transcript paths
- source assertions
- shallow-pass trap
- adversarial negative proof
- semantic positive proof
- anti-stub audit
- raw-note literal closure
- dependency smoke proof where stated

Production Behavior Artifact Matrix required if new production contract records/events are added. Matrix must identify producer, consumer, lifecycle, and negative tests.


## Browser Validation Logging

N/A for browser UI unless the implementation adds a visible contract inventory page. If UI is touched, add Playwright route, viewport, screenshot, and console evidence to `reviews/01-execution-report.md`.

## Progression Gate

SB01 passes only when a later subbundle can import/use canonical descriptors without guessing ownership of identifiers or proof semantics.

## Suggested Agent Prompt

Implement SB01 only. Build the canonical contract inventory and drift scanner first. Do not refactor large files yet. Preserve compatibility and write artifact-backed proof under `proof/SB01/`.
