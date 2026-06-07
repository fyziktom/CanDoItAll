# Bundle Self Review

## Architect
- Planned Core expansion is limited to pure rule/read-model families.
- Side-effectful orchestration remains module-local.
- Driver work remains docs/tests-only.

## QA
- Focused parity tests are required for every production move.
- UI/mobile proof is explicitly disallowed for this runtime/service-only bundle.
- Completed-stage validator is required in SB036.

## Manager
- Subbundles are broader, meaningful work slices.
- Critical gates occur after every three subbundles.
- Raw user request remains traceable through requirements and execution report closure.
