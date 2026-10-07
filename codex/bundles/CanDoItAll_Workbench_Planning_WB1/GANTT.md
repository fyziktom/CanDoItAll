# Complete Gantt presentation and original mutations

Sources S19–S20, S25–S27. Extract the entire useful scheduling surface, not a wrapper that
still mounts the old backend component. The native projection can remain native; map its
safe resulting facts into the leaf, or move a genuinely pure adapter only after dependency
and owner review. Do not move data access by calling it presentation.

## Required rendering and gestures

Keep actual Components.Gantt chart and TaskDragSource. Support existing title edit, bar move,
start/end resize, dependency add/remove/reconnect, insertion on a dependency, task order,
timeline double-click create and task double-click details. Toolbar retains counts, effort,
per-currency cost totals, native projection warnings/errors, Agent chats and exports. Keep
controls reachable at the supported large desktop. Existing recoverable render error boundaries
must not become a blanket way to hide a reproducible product exception.

Preserve source task identities, exact dependency identities/endpoints and ordering. A
projected schedule is visibly marked. Existing bar move/resize can materialize the complete
displayed schedule and required downstream shifts; the details form can still refuse direct
interval edits until authoritative dates exist. Do not make these two intentional policies
identical just to share a bool. Preserve cycle rejection and native expected-schedule checks.

## Read and effect ownership

Keep captured-Surface load guards already present. Assignments, saved row order, origin date
and native tasks must form a coherent accepted projection. A newer load supersedes older
success/error/finally without deleting its newer request. Loading observations remain bounded
and explicitly partial for Agent context.

Each emitted intent carries its original displayed origin and selected occurrences. The
host captures expected project admission, task IDs, schedule baseline, original projection,
callbacks and operation before await. UI selection is not authority. Gantt facts visible to
the model remain observations; the backend still obtains current native permission.

Use per-view mutation admission. Consider real double dispatch and queued shared-component
callbacks, not only disabled buttons. Native accepted work can complete after the view retires;
retain its receipt but do not notify, reload, close or mutate a successor view. A stale dialog
must not newly submit merely because its old project's backend admission is still valid.

An optimistic schedule belongs to one original operation. Roll back only that original
uncommitted preview, never a newer projection. Known rejected, known committed with readback
warning, and genuinely unknown are different states. Retry a failed refresh by reading only.
No phantom rollback of confirmed owner mutations and no blind replay of lost create results.

## Exports

Mermaid preview, text copy and .mmd download use the same frozen accepted projection,
project title and task ordering. Keep strict Mermaid policy and sanitization. A later change
must not relabel old source as current; either keep it explicitly historical or refresh by
an explicit read-only action. Existing PNG export remains real shared-chart behavior; compare
actual decoded output and geometry, not just a download event. No document runtime is added
to the planning UI solely for export.

## Native proof

Use real tasks with nontrivial dependencies, incomplete schedules and unchanged neighbors.
Perform actual mouse gestures and read back exact native start/end, node/link IDs and order.
Include title conflict, schedule conflict, cross-project/recreated-lifetime refusal, blocked
or multiple assignees, resource restrictions, dispose during original read/write, two separate
Gantt views and accepted-write/readback failure. Count calls to prove no duplicate mutations.
The final parent Structure route still loads authoritative data and publishes the correct
Gantt observation. A stub mutation delegate is only unit-layer evidence.
