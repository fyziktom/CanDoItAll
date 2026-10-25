# Phase / slice review record

Status: draft. Do not fill unexecuted evidence with assumed values.

## Identity and scope

- Phase ID and stable mutation IDs:
- Actual branch / resolved commit / working-tree changes:
- Sibling source revisions and dependency mode:
- Selected owner and complete enabled write perimeter:
- Explicitly excluded owners/operations:
- Owner, UI/API/tool, persistence and QA reviewers:

## Decisions and impact

| Decision/invariant | Accepted interpretation | Changed callers/contracts/schema | Evidence or open blocker |
|---|---|---|---|
| | | | |

State the exact original expected state, atomic effect boundary, current-authority protocol, operation replay scope, accepted-result shape, projection guarantee, lock classes and size limits. Record whether a source is pinned, current at admission, current at publication or external observation.

## Source refresh

List changed/moved anchors, actual caller discovery method and unresolved graph edges. An identical source blob is not proof of unchanged callers. Record source/CI/migration guidance changes.

## Validation

| Planned scenario IDs | Actual test project/symbol | Exact command/filter | Expected / discovered | Passed / failed / skipped | Source/environment | Evidence path |
|---|---|---|---|---|---|---|
| | | | | | | |

Include positive as well as negative cases. Record required PostgreSQL/two-process/tool/browser/platform lanes separately. Mark unavailable environments as unavailable, not passing. For protected repository changes record portability scan/review/final enforcement without baseline-writing mode.

## Performance

Use BENCHMARK_REPORT.md. Record baseline and changed SQL counts, rows, latency distribution, local/DB wait and hold, allocations and payload. State any candidate-budget exception and its accepted trade-off; no weakening of conditions as a workaround.

## Lifecycle and operation recovery

Migration lineage/model parity; existing data; old-client refusal; creation/reservation; receipt retention; restart; import; destructive restore namespace; rollback-to-compatible-build or forward-repair plan; uncertain effect recovery.

## Exit / residual risk

For each exit gate: met / not met / not applicable with reason and evidence. List residual risk IDs, affected operations, owner acceptance and next decision. State the precise coverage that may be advertised and the safe stop state. Do not infer full-product safety from a closed slice.
