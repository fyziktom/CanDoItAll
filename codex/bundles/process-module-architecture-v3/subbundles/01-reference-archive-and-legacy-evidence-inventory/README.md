# SB01 Reference Archive And Legacy Evidence Inventory

## Status

Future implementation package; prepared by architecture bundle v3; not executed in v3.

## Objective

Archive the old Process implementation as complete reference material before any active deletion, with machine-readable and human-readable manifests that preserve source, tests, templates, integration snippets, hashes, categories, and reuse decisions.

## Why This Bundle Exists

The rewrite must not wrap the old dispatcher, but it also must not throw away hard-won edge-case knowledge. This bundle protects against both risks by preserving old code as evidence before active removal.

## Covered Inputs

- REQ-048: copy old Process implementation into reference material before deletion.
- REQ-049: remove old Process projects/tests only after archive proof.
- v3 Phase 0 split: archive-only work belongs here; active removal belongs to SB02.

## Context Reset: Read These First

- `README.md`
- `analysis/04-current-code-evidence-map.md`
- `analysis/05-reuse-decision-log.md`
- `architecture/17-runtime-history-migration-and-readonly-compatibility.md`
- `plan/02-phase-0-reference-archive-and-removal.md`
- `plan/04-future-subbundle-roadmap.md`
- `plan/05-review-checkpoints-and-hardening-gates.md`

## Exact Source References

- `repo://codex/bundles/process-module-architecture-v3/analysis/04-current-code-evidence-map.md`
- `repo://codex/bundles/process-module-architecture-v3/analysis/05-reuse-decision-log.md`
- `repo://codex/bundles/process-module-architecture-v3/plan/02-phase-0-reference-archive-and-removal.md`
- `repo://src/CanDoItAll.Modules.Processes`
- `repo://Templates/Processes`
- `repo://tests`

## Source Evidence To Use

- `repo://src/CanDoItAll.Modules.Processes`
- `repo://src/CanDoItAll.Processes.Core`
- `repo://src/CanDoItAll.Processes.Contracts`
- `repo://src/CanDoItAll.Processes.Drivers.Abstractions`
- `repo://src/CanDoItAll.Processes.Drivers.ArtifactEvidence`
- `repo://src/CanDoItAll.Processes.Drivers.BusinessAnalysis`
- `repo://src/CanDoItAll.Processes.Drivers.ObservationAggregation`
- `repo://src/CanDoItAll.Processes.Drivers.OfficeEvidence`
- `repo://src/CanDoItAll.Processes.Drivers.RuntimeEvidence`
- `repo://src/CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence`
- `repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification`
- `repo://src/CanDoItAll.Processes.Drivers.VerificationGateway`
- `repo://Templates/Processes`
- `repo://tests`
- `repo://CanDoItAll.slnx`

## Prerequisites

- Architecture bundle v3 approved by user.
- Clean working tree.
- Future implementation branch created.

## In Scope

- Create `codex/bundles/process-module-rewrite-reference-v1/legacy/`.
- Copy Process source projects, process-related tests, templates, and integration snippets.
- Compute SHA-256 hashes, line counts, file sizes, categories, and reuse decisions.
- Inventory DI, routes, navigation, EF configuration, scheduler/workflow, workbench, and project-structure references.
- Inventory template sidecars and current-module projections as migration input.

## Out Of Scope

- Do not delete active source.
- Do not create new target projects.
- Do not migrate templates.
- Do not fix old Process tests.
- Do not execute product rewrite behavior.

## Target Projects / Files

- `codex/bundles/process-module-rewrite-reference-v1/legacy/**`
- `codex/bundles/process-module-rewrite-reference-v1/manifest.json`
- `codex/bundles/process-module-rewrite-reference-v1/manifest.md`
- `codex/bundles/process-module-rewrite-reference-v1/inventories/*.md`

## Deliverables

- Complete reference archive.
- Machine-readable manifest with hashes.
- Human-readable archive summary.
- Integration snippet inventory.
- Old test inventory.
- Template pack inventory.

## Expected Deliverables

- Manifest entries include source path, archive path, hash, file size, line count, category, reuse decision, reason, related requirements, and related future tests.
- Archive proof is reproducible by rerunning hash commands.

## Dependency Impact

- No active product source should change except archive files.
- SB02 cannot start until this bundle passes archive completeness gates.

## Validation Depth

- Validate archive completeness with hashes, line counts, manifest checks, source inventory, test inventory, template inventory, and git status.

## Architecture Invariants That Must Hold

- Old implementation remains reference evidence only.
- No future code should reference the archive as production source.
- `Templates/Processes` is migration input and must be preserved.

## Implementation Steps

1. Verify branch and clean working tree.
2. Inventory Process projects, tests, templates, and integration references.
3. Copy sources into the reference archive.
4. Generate manifest JSON and Markdown summary.
5. Categorize files with archive/adapt/drop/replace/migrate decisions.
6. Verify hashes and line counts.
7. Record search commands and results.

## Refactoring Review Checkpoint

- Confirm no active product files were refactored.
- Confirm archive categories match `analysis/05-reuse-decision-log.md`.
- Confirm no archive gaps for dispatch, runtime, templates, tests, UI, drivers, and integrations.

## Required Tests / Proof

- Hash verification script or command output.
- `git status --short -uall`.
- Search proof for Process-related projects/tests/templates included in manifest.

## Search Proof

Search for Process source, test, and template paths and prove each is represented in the manifest.

## Stop And Report Conditions

- Stop if any Process-related source/test/template surface cannot be copied or hashed.
- Stop if integration references are too broad to inventory confidently.
- Stop if working tree is dirty with unrelated user changes that would be mixed into archive proof.

## Do Not Do

- Do not wrap or reuse `ProcessRunAutomationDispatchService`.
- Do not remove active Process projects.
- Do not modify product behavior.
- Do not treat archive material as production code.

## Acceptance Checklist

- [ ] Reference archive exists.
- [ ] Manifest JSON exists.
- [ ] Manifest Markdown summary exists.
- [ ] Hashes are reproducible.
- [ ] Source, tests, templates, and integrations are inventoried.
- [ ] SB02 handoff notes are written.

## Proof Required

- Archive manifest path.
- Hash verification output.
- Inventory report paths.
- Git status output.

## Browser Validation Logging

- Browser validation is not required because this bundle creates reference documentation and archive files only.

## Progression Gate

- SB02 may start only after archive manifest and hash proof are complete.

## Suggested Agent Prompt

Execute SB01 from `codex/bundles/process-module-architecture-v3/subbundles/01-reference-archive-and-legacy-evidence-inventory`. Preserve old Process source, tests, templates, and integration evidence with hashes. Do not delete active source.

## Handoff Notes For Next Bundle

Record archive path, manifest path, skipped items, hidden dependency notes, and exact old-symbol inventory for SB02.
