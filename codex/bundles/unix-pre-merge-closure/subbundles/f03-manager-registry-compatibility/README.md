# Legacy Manager registry compatibility

## Goal

Read old Manager ownership records without crashes and without granting unsafe termination authority.

## Entry

Read the root execution prompt, findings, requirements, invariants and
validation strategy. Reconfirm the exact repository anchor before editing.

## Tasks

1. Introduce a current registry schema version and an explicit schema-1 legacy DTO.
2. Detect missing process Boundary before current-record validation.
3. Convert legacy records to OwnershipUnverified with stable diagnostic legacy-process-boundary-missing.
4. Never call TerminateOwnedProcessAsync for converted legacy records.
5. Atomically rewrite converted records in the current schema, or retain an explicit migration-pending state with deterministic next-write behavior.
6. Validate current boundary kind/native-id/instance-id combinations.
7. Add real JSON fixture tests and fake-host termination-count assertions.

## Rules

- Preserve unrelated changes.
- Use focused failing-first tests.
- Keep source comments in English.
- Do not push or merge.
- Do not weaken a validator to make evidence pass.
