# SB03 Template And Git Foundation

## Status

Planned.

## Objective

Build JSON-first templates, schema migrations, component overrides, conflict detection, and the typed Git wrapper needed for versioned configuration.

## Covered Inputs

- REQ-031 through REQ-041

## Prerequisites

- SB01 complete.
- SB02 interfaces available where template models need generic IDs.

## Exact Source References

- `bundle://architecture/01-target-solution.md`
- `repo://Templates/Processes`
- `repo://src/CanDoItAll.Modules.Processes/Templates`

## Deliverables

- `CanDoItAll.Processes.Templates`
- `CanDoItAll.Git`
- Template schemas and migrations.
- Component reference and override model.
- Three-way merge and conflict model.
- Generated Markdown/Mermaid projection service.

## Dependency Impact

- Builder cannot safely consume templates until this foundation exists.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Add Git wrapper with typed status, diff, commit, branch, merge, and conflict operations.
2. Add template schema models with schema versions and content hashes.
3. Add migration chain with no skipped intermediate versions.
4. Add component override and three-way conflict engine.
5. Convert representative current templates.
6. Generate Markdown/Mermaid on demand from JSON.

## Scope Exceptions

Do not migrate all templates until SB10 unless this subbundle chooses a representative subset for proof.

## Do Not Do

- Do not make Markdown or Mermaid canonical.
- Do not implement Git semantics manually.
- Do not store full local copies when a patch override is sufficient.

## Acceptance Checklist

- Git wrapper contract tests pass.
- Template loader rejects unsupported schema versions with actionable errors.
- Migration tests prove chained migration.
- Conflict tests show clean update and conflicting local override.

## Proof Required

- Unit/integration transcript for Git wrapper and templates.
- Migration before/after files.
- Semantic Adequacy Gate.
- `proof/SB03/manifest.md`.
- Production Behavior Artifact Matrix for template schema version, component hash, migration record, and conflict record.

## Browser Validation Logging

- N/A unless Git UI preview components are introduced early.

## Progression Gate

- SB04 cannot consume production templates until this gate passes.

## Suggested Agent Prompt

Implement the template/Git foundation only. JSON is canonical; projections are generated.
