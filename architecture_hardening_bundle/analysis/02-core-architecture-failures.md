# Core architecture failures

## 1. Canonicality failure

The dependency concept is represented in more than one persisted or derived way. That weakens every downstream area: validation, save, read, clone, and runtime activation.

## 2. Mutation purity failure

Validation currently relies on state normalization with side effects. That is a foundational architectural smell because correctness checks should not change the thing being checked.

## 3. Aggregate-stability failure

Saving a definition currently behaves more like replacing a document subtree than mutating a durable aggregate graph. That is fragile for long-term maintenance.

## 4. Conflict-handling failure

Critical mutations can race without aggregate-level conflict protection. The module needs application-level optimistic concurrency, not only provider-level write coordination.

## 5. Responsibility-concentration failure

The public service façade and large UI surface each own too much orchestration knowledge. Partial-file splitting helped navigation but did not truly solve responsibility concentration.

## 6. Duplication failure

Template parsing, summary building, and generic transformation helpers are duplicated enough that future fixes would likely be repeated inconsistently.

## 7. Query-shape failure

Read surfaces still assume limited scale and often pull more data than necessary.

## Repair philosophy

The bundle sequence repairs these failures in order of architectural leverage:
1. fix canonicality,
2. fix mutation purity,
3. fix atomicity and conflict handling,
4. fix persistence stability,
5. split responsibilities,
6. consolidate duplication,
7. finish schema and UI cleanup after the mutation core is trusted.
