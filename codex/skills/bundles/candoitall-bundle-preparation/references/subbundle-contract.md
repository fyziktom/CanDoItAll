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

Every subbundle must contain these semantic fields under any clear headings:

- objective
- covered inputs or notes
- prerequisites
- exact source references
- scope or deliverables
- dependency impact
- validation depth
- implementation steps
- do-not-do constraints
- acceptance checklist
- proof required
- proof tier: `Standard`, `Behavioral`, or `Governed`
- progression gate
- reopen triggers

## Proof Guidance

Prefer proof that another agent can independently verify:

- `dotnet test` commands
- build commands
- Playwright flows
- screenshot artifact paths
- specific DOM or style checks
- explicit file diffs or generated artifacts

When a subbundle unlocks later phases:

- state which later phases depend on it
- require the exact proof that allows downstream work to continue
- mark it as a critical foundation when weak proof would invalidate later verification
- require a dependent-flow check, but require full manifests and transcripts only when its proof tier is `Governed`
