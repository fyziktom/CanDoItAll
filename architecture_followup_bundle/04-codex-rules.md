# Codex execution rules

## Non-negotiable rules

1. Do **not** accept the previous “completed” status as authoritative. Start from the live repository state.
2. Do **not** treat “mirror fields behind a bridge” as equivalent to true canonicality.
3. Do **not** relax database integrity just because the current save algorithm temporarily prefers looser ordering.
4. Do **not** close a subbundle on prose alone. Fresh proof must exist in artifacts.
5. Do **not** claim that a test suite ran unless the emitted `.trx` clearly shows the relevant tests.
6. Do **not** continue past an architecture review gate if any gate question fails.
7. When a gate fails, create the corrective subbundle first, complete it, re-run proof, and only then continue.

## Preferred design rules

- Prefer removing legacy mirrors over synchronizing them.
- Prefer DB-enforced invariants over service assumptions.
- Prefer provider-agnostic invariants when possible.
- Reuse an existing outbox pattern from the repository if it fits, but do not force-fit a brittle abstraction.
- Keep compatibility logic at import/export boundaries, not in entity/editor/runtime core types.

## Forbidden shortcuts

- “Application-managed for now” without a written reason, failing proof, and a follow-up task.
- `FirstOrDefault()` fallback semantics for things the domain claims are singular invariants.
- Broad `MAX + 1` allocators for versioning.
- Post-commit best-effort side effects presented as atomic command success.
