# Runtime invariants

- INV01: A process step with persisted operation contract must not depend on text parsing for allowed operations.
- INV02: API/tool save/import/export must preserve operation contracts and artifact output mappings.
- INV03: A normalized target alias has exactly one effective authority per run.
- INV04: A process artifact satisfying a required expectation must pass finalizer-grade validation regardless of whether completion came from automation or manual/API transition.
- INV05: Workflow/subprocess output cannot satisfy a process artifact expectation without explicit mapping or a deterministic single-match contract.
- INV06: Typed block reason code must be available for every blocked/failed automation transition.
- INV07: A process step cannot repeatedly retry the same no-progress state without a new evidence/recovery event.
- INV08: Scripts cannot mutate product targets outside the declared process operation contract.
- INV09: Related skill/docs must document every public process governance field.
- INV10: Processes remain generic and do not assume software delivery semantics.
