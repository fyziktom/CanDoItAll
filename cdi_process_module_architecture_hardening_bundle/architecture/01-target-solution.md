# Target solution

## Core target

The target architecture keeps the `Processes` module as the canonical owner of process definitions and process runtime, but repairs the internal shape so that:

- each concept has one source of truth,
- critical mutations are atomic and conflict-aware,
- graph persistence preserves stable identity,
- runtime transition logic is decomposed and testable,
- query logic is projection-focused,
- duplicated infrastructure is extracted intentionally,
- UI composition stops concentrating domain behavior in one workspace monolith.

## Canonical data rules

### Dependencies
The canonical dependency representation should be the explicit dependency row/collection model, not a pair of legacy scalar fields on the step definition.

### Validation
Validation is a pure analysis stage.
Normalization is a separate, explicit transformation stage.

### Publication
Publication lifecycle and definition cloning are separate responsibilities.
The lifecycle decides *whether* publish may proceed.
The clone engine decides *how* the next draft graph is created.

### Runtime
Runtime transition orchestration should be composed from smaller policies/services such as:
- transition guard,
- branch-outcome validator,
- dependent-activation planner,
- non-selected-path resolver,
- run-status recompute service,
- journal/improvement side-effect dispatcher.

These exact names are suggestions, but the responsibility split is mandatory.

## Compatibility boundary recommendation

A safe transition pattern is:

1. choose the canonical dependency model,
2. centralize legacy-field interpretation in one adapter,
3. update save/load/clone/read/runtime logic to use the canonical model,
4. only then decide whether the legacy fields can be removed outright or retained temporarily for migration compatibility.

## Public service façade recommendation

Keep `ProcessesService` as a façade if that minimizes caller churn. Inside the module, delegate to narrower internal services such as:

- definition command/persistence service,
- publication service,
- clone service,
- runtime command service,
- query services,
- template/shared helper services.

The façade should orchestrate, not own all detailed logic.

## UI target

The workspace should move toward:
- smaller components,
- a clearer state container or presenter,
- thinner event handlers,
- canvas and runtime surfaces that consume prepared state rather than computing domain rules inline.

## Proof target

The target is not only “cleaner code”.
The target is **auditable proof** that:
- canonicality improved,
- conflicts are surfaced cleanly,
- stable IDs survive,
- runtime behavior is preserved,
- broad query loads were reduced,
- UI remains coherent after decomposition.
