# Architecture Checkpoints

## Checkpoint A - Before SB01 Implementation

- Confirm resolver contract placement does not force module dependencies into runtime or contracts.
- Confirm unresolved placeholders are reported as explicit diagnostics, not silently ignored.

## Checkpoint B - Before SB02/SB03 Merge

- Confirm aggregate gate result types preserve original diagnostics.
- Confirm recovery classifier does not depend on free-form message text.
- Confirm retry policy has bounded budget and stable fingerprinting.

## Checkpoint C - Before SB06 Merge

- Confirm child run state and artifact bridge are separate responsibilities.
- Confirm accepted artifact slots and no-go outputs are typed.
- Confirm physical file existence is used only as explicitly labeled fallback evidence.

## Checkpoint D - Before SB08/SB09 Migration

- Confirm template schema contracts are validated by structured model tests.
- Confirm runtime does not parse markdown prose to enforce hard gates.
- Confirm execution class and required tool metadata are typed, not magic strings.

## Checkpoint E - Before SB11 Runtime Executor

- Confirm deterministic .NET setup command records are idempotent and scoped to the workspace.
- Confirm executor respects tool-plan guard and does not duplicate prompt-only behavior.
- Confirm errors include actionable state and mask sensitive data.

## Checkpoint F - Final Closure

- Confirm dependency direction remains acyclic.
- Confirm partial-class growth is justified and minimal.
- Confirm all new services are independently unit-testable.
- Confirm validation includes negative shallow-pass traps.
