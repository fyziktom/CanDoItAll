# Target architecture

## Target direction

The Process module should now aim for **correctness-first closure with explicit boundaries**, not broad reinvention.

### 1. One canonical dependency model plus one legal graph invariant
The core dependency representation is now mostly canonical. The next step is to make the graph itself legal:

- canonical dependency rows remain the only dependency truth in core types;
- save/publish enforce a DAG invariant;
- runtime and canvas stop compensating silently for invalid graphs.

### 2. Schema-backed runtime singularity
The runtime code should not assume singular rows that the schema allows to duplicate.

The target is:

- exactly one `ProcessStepRun` per run+step definition;
- exactly one `ProcessRunAssignment` per logical role scope;
- conflict surfaces that stay understandable under concurrency races.

### 3. Deterministic workspace write ordering
`ProcessWorkspace` should have one explicit “definition persistence quiescence” boundary used before actions that depend on persisted state:

- publish;
- delete;
- export;
- process switch;
- disposal if needed.

### 4. Cohesive workspace reads
A workspace refresh should preferably come from one read boundary or a very small number of intentional boundaries, not many unrelated service calls that may each see different snapshots.

### 5. Isolated template mapping helpers with an explicit pack-thread-safety decision
Template mapping rules should have one owner. Pack loading should remain either:

- scoped because the graph is mutable, or
- safely broader-cached only after the graph becomes immutable or defensively cloned.

### 6. Targeted performance cleanup after correctness
Only after the correctness gaps are closed should Codex reduce obvious repeated scans, dead locals, low-value duplication, and file concentration.
