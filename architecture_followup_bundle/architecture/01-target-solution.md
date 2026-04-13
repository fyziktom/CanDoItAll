# Target architecture

## 1. Canonical dependency model

The target state is:

- `ProcessStepDefinition` does **not** carry legacy scalar dependency mirrors;
- `ProcessStepEditorModel` does **not** carry legacy scalar dependency mirrors;
- `ProcessStepRunViewModel` does **not** expose single-dependency shortcut fields;
- the only dependency representation used inside the module is the canonical collection / row shape;
- old import/export formats, if still required, are handled by explicit versioned boundary DTOs and mapping code.

## 2. Database-backed integrity

The target state is:

- aggregate-owned rows have explicit foreign keys;
- cross-edge references have deliberate delete behavior, documented instead of implied;
- the DB rejects orphan rows and invalid references without relying on service ordering alone.

## 3. Domain invariants enforced in the schema

The target state is:

- one draft per definition;
- one published version per definition;
- safe active published version pointer;
- version number allocation is conflict-safe and does not use `MAX + 1`;
- dependency uniqueness works for both conditional and unconditional routes.

## 4. Durable side effects

The target state is:

- DB mutations enqueue side effects inside the transaction;
- external dispatch is retriable and idempotent;
- command handlers do not pretend the DB failed when only a post-commit projection or activity dispatch failed.

## 5. Structural follow-up only after invariant safety

Once the red invariants are closed, the final follow-up is:

- turn nested/static query helpers into injectable seams;
- keep `ProcessesService` thin or split it further;
- continue reducing `ProcessWorkspace` orchestration concentration without reopening core model ambiguity.

