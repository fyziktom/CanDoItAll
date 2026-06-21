# SB01 Reference Archive And Removal

## Status

Planned for later implementation branch.

## Objective

Copy the existing Process implementation into bundle reference material, then remove old Process projects, module code, and process-specific tests so the rewrite does not accidentally couple to the old architecture.

## Covered Inputs

- REQ-047
- REQ-048
- REQ-049

## Prerequisites

- New rewrite branch exists.
- Bundle versioning is enabled.
- Owner agrees that old runtime compatibility is not required during the rewrite branch.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes`
- `repo://src/CanDoItAll.Processes.Contracts`
- `repo://src/CanDoItAll.Processes.Core`
- `repo://src/CanDoItAll.Processes.Drivers.Abstractions`
- `repo://src/CanDoItAll.Processes.Drivers.VerificationGateway`
- `repo://tests`
- `repo://Templates/Processes`

## Deliverables

- `bundle://reference/old-process-module/manifest.md`
- Archived source copy with hashes.
- Solution with old Process projects/tests removed.
- Replacement placeholder projects only where needed to keep solution restorable.

## Dependency Impact

- Blocks all downstream implementation.
- If the archive is incomplete, useful behavior and tests may be lost.

## Validation Depth

- Critical foundation.
- Requires archive hash proof before deletion.

## Implementation Steps

1. Create reference archive folder.
2. Copy current Process source, templates, and related tests.
3. Generate file hash manifest.
4. Remove old Process projects and process test files from the rewrite branch.
5. Update solution and project references.
6. Confirm no old Process runtime code is compiled.

## Scope Exceptions

Do not preserve runtime behavior in production during this subbundle. This is rewrite-branch setup only.

## Do Not Do

- Do not partially refactor the old dispatcher.
- Do not keep old runtime classes as hidden compatibility services.
- Do not delete before archive proof exists.

## Acceptance Checklist

- Archive manifest exists with hashes.
- Old Process projects are absent from the solution.
- Old process tests are absent or moved into reference.
- New work can start from empty target projects.

## Proof Required

- Archive hash manifest.
- Git diff of removed project references.
- Build/restore transcript showing expected state.
- Semantic Adequacy Gate: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.
- `proof/SB01/manifest.md` with changed-file hashes, validation transcripts, source assertions, and anti-stub audit output.

## Browser Validation Logging

- N/A. No browser-visible behavior should be active after removal.

## Progression Gate

- Downstream subbundles cannot start until the reference archive is complete and old Process code is no longer compiled.

## Suggested Agent Prompt

Use `shared-prompts/implementation-prompt.md` and execute only SB01. Preserve the current implementation as reference material before deleting anything.
