# Subbundle Contract

## Split Principles

Split by coherent ownership, not by arbitrary file counts.

Good subbundle boundaries:

- one shared-library extraction phase
- one UI behavior cluster
- one validation or proof phase
- one migration step with a clear rollback story

Bad subbundle boundaries:

- “misc fixes”
- “cleanup later”
- “remaining issues”
- “part 2” without a named objective

## Required Sections

Every subbundle README must contain:

- objective
- covered inputs or notes
- exact source references
- scope or deliverables
- implementation steps
- do-not-do constraints
- acceptance checklist
- proof required
- suggested agent prompt

## Proof Guidance

Prefer proof that another agent can independently verify:

- `dotnet test` commands
- build commands
- Playwright flows
- screenshot artifact paths
- specific DOM or style checks
- explicit file diffs or generated artifacts
