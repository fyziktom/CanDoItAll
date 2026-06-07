# Core Candidate Boundaries

## Production Core seed in this bundle

Move only:

- route stage enum / descriptors,
- canonical route stage order,
- pure run/step eligibility decisions,
- pure route-order assertion helper if dependency-free.

Do not move:

- route handlers,
- route services,
- dispatch route model adapters,
- candidate hydration,
- direct-agent runtime,
- workflow/subprocess orchestration,
- materialization side effects,
- transitions,
- claim lifecycle.

## Rehearsal candidates

These are mapped and tested only; do not move in production in this bundle unless explicitly allowed by a future decision:

- subprocess lifecycle status/request pure rules,
- artifact expectation snapshot/matching pure rules.

## Future driver preparation

Prepare only:

- documentation-only verification lane descriptions,
- permission-mode vocabulary,
- negative architecture tests that reject production driver APIs.

Do not create:

- `IProcessDriverPack`,
- `IProcessDriverRegistry`,
- driver DI registration,
- manager tools,
- runtime dispatch hooks,
- execution-capable helper drivers.
