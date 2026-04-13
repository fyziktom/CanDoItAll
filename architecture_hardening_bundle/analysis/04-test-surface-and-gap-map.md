# Test surface and gap map

## Existing strengths

### Definition and publish behavior
The integration suite already covers:
- branch-outcome publish rejection,
- artifact-input structural validation,
- preservation of authoring positions and artifact links across publish/clone.

### Runtime branching behavior
The runtime suite already covers:
- selected branch routing,
- non-selected path skipping,
- dependency join readiness.

### UI behavior
Component tests already cover:
- workspace load,
- help affordances,
- canvas mode switching,
- connection creation and deletion,
- persistence of role and artifact links,
- node movement persistence,
- template dialog behaviors.

### MCP behavior
MCP tests cover:
- definition save/publish projection,
- transition request forwarding,
- template list/get/import behavior.

## Gaps that matter for this initiative

### Concurrency and conflict gaps
Missing or insufficient:
- conflicting concurrent definition save,
- concurrent publish of the same definition,
- concurrent runtime transition of the same step,
- unique-slug conflict translation,
- next-version conflict translation.

### Differential persistence gaps
Missing or insufficient:
- no-op save preserves child IDs,
- single-step edit preserves unrelated child IDs,
- assignment/artifact/dependency links survive targeted graph updates,
- rollback leaves no partial child graph.

### Canonicality and compatibility gaps
Missing or insufficient:
- core logic reads only canonical dependency rows,
- validation is provably side-effect free,
- compatibility adapter is the only place reading legacy dependency fields.

### Read-side and performance gaps
Missing or insufficient:
- query-shape tests for definition list,
- analytics query footprint proof,
- large-data smoke or at least query-behavior assertions.

### Architecture guardrail gaps
Missing or insufficient:
- tests or analyzable evidence that the service façade delegates to smaller internals,
- guardrails against reintroducing duplicated dependency fallback logic.

## Gap-ownership summary

- subbundles 01-04 own the baseline and canonicality gap closure,
- subbundles 05-07 own concurrency and graph-stability proof,
- subbundles 08-11 own publish/runtime/query proof,
- subbundles 12-15 own consolidation and decomposition proof,
- subbundle 16 owns the final regression matrix and closure.
