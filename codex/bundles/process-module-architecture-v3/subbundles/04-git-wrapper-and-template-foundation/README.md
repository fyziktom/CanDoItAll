# SB04 Git Wrapper And Canonical Template Foundation

## Status

Executed on 2026-06-15. Closure proof is recorded under `proof/SB04`.

## Objective

Build the generic Git wrapper and canonical JSON template foundation, including component references, local overrides, deterministic migrations, conflict records, projection hash metadata, and template index contracts.

## Why This Bundle Exists

Templates, instructions, skills, workflows, and process definitions need versioned file-backed configuration. This bundle prevents custom VCS behavior and prevents Markdown/Mermaid sidecars from becoming canonical.

## Covered Inputs

- REQ-031 through REQ-041.
- v3 template/Git dependency decision.

## Context Reset: Read These First

- SB03 execution report.
- `architecture/09-template-git-versioning-and-migrations.md`
- `architecture/11-project-boundary-and-dependency-map.md`
- `plan/05-review-checkpoints-and-hardening-gates.md`

## Exact Source References

- `repo://codex/bundles/process-module-architecture-v3/architecture/09-template-git-versioning-and-migrations.md`
- `repo://codex/bundles/process-module-architecture-v3/architecture/11-project-boundary-and-dependency-map.md`
- `repo://Templates/Processes`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackLoader.cs`

## Source Evidence To Use

- `repo://Templates/Processes`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackLoader.cs`
- SB01 template inventory.

## Prerequisites

- SB03 complete.
- Core/template-related contracts available.

## In Scope

- `CanDoItAll.Git` wrapper with typed operations.
- Path authorization.
- Sanitized command logging.
- Git status/diff/add/commit/branch/merge/conflict/log/show support.
- Canonical template JSON schemas.
- Component references and content hashes.
- Local override patch model.
- Three-way conflict records.
- Sequential migration registry.
- Projection hash metadata.
- Template index contracts.

## Out Of Scope

- Do not migrate the full current template pack yet.
- Do not build Process UI.
- Do not execute process runtime.
- Do not make Git UI components; that belongs to SB13.

## Target Projects / Files

- `src/CanDoItAll.Git`
- `src/CanDoItAll.Processes.Templates`
- tests for Git wrapper and templates.

## Deliverables

- Generic Git wrapper.
- Canonical template schema foundation.
- Template migration/merge primitives.
- Tests for Git and template behavior.

## Expected Deliverables

- Template operations produce planned file changes; Application later composes those with Git.
- Markdown/Mermaid/current-module projections are generated/exported only.

## Dependency Impact

- SB06 builder uses template schema and migration behavior.
- SB12 uses this foundation for real template pack migration.
- SB13 uses Git wrapper through generic Git components.

## Validation Depth

- Validate with Git wrapper tests, path authorization tests, template schema tests, migration chain tests, merge/conflict tests, and projection hash drift tests.

## Architecture Invariants That Must Hold

- JSON is canonical.
- Template migrations do not skip intermediate versions.
- Git wrapper is Process-neutral.
- Paths are authorized and logs sanitized.

## Performance Antipattern Notes

- Read `architecture/19-dotnet-performance-guardrails.md` and `validation/05-dotnet-performance-antipattern-checklist.md` before creating or modifying C# hot-path code.
- Record exact performance scan counts in the execution report when this subbundle changes runtime, dispatcher, manager, projection, template, Git, adapter, persistence, or UI service code.
- Do not introduce sync-over-async, unbounded event/projector queues, per-call `HttpClient`, per-call `JsonSerializerOptions`, load-all UI queries, or LINQ-heavy hot paths without a recorded mitigation and proof.
## Implementation Steps

1. Implement Git wrapper contracts/results.
2. Add path authorization and command execution safety.
3. Define template JSON schemas and version markers.
4. Define component refs, local override patches, and conflict records.
5. Implement migration registry contract.
6. Add projection hash metadata.
7. Add tests and negative fixtures.

## Refactoring Review Checkpoint

- Split Git process execution from result parsing.
- Split template schema models from migration services.
- Keep merge rules deterministic and testable.

## Required Tests / Proof

- Git wrapper unit/integration tests.
- Template schema tests.
- Migration chain tests.
- Override merge/conflict tests.
- Projection hash drift tests.

## Search Proof

- Search for ad hoc Git/shell calls outside `CanDoItAll.Git`.
- Search for Markdown/Mermaid canonical-source behavior.

## Stop And Report Conditions

- Stop if Git command safety cannot be implemented without unsafe string concatenation.
- Stop if current template sidecars must be treated as source to proceed.
- Stop if migration chain cannot represent skipped-version safety.

## Do Not Do

- Do not implement a custom VCS.
- Do not call Git from Process runtime.
- Do not treat Markdown/Mermaid/current-module projections as canonical.

## Acceptance Checklist

- [x] Git wrapper tests pass.
- [x] Template schema tests pass.
- [x] Migration chain tests pass.
- [x] Conflict model exists.
- [x] Projection hash metadata exists.

## Proof Required

- Test output.
- Git safety review.
- Template/Git review gate output.

## Browser Validation Logging

- Browser validation is not required because no UI behavior is implemented.

## Progression Gate

- SB06 may use templates after schema, migration, and conflict primitives pass tests.

## Suggested Agent Prompt

Execute SB04 from `codex/bundles/process-module-architecture-v3/subbundles/04-git-wrapper-and-template-foundation`. Build generic Git and canonical template foundations. Keep JSON canonical.

## Handoff Notes For Next Bundle

Record Git wrapper API, template schema versions, migration registry behavior, and known migration risks for SB06 and SB12.
