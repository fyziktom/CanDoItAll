# Inherited publication snapshots and catalog ordering

Status: designed before implementation; the corrected snapshot prerequisite passes
[238 selected cases](../validation/2026-10-20-inherited-publication-snapshot.md).
Business publication and producer activation are still closed.

A child planner must retain its exact schema-7 run, journal, original parent binding
and approved effect while checking current executor and launching-Agent permissions.
It has no interactive chat. Reconstructing a chat or taking another catalog lease
inside a held snapshot would respectively invent authority or create a lock inversion.

Extend the existing publication expectation with an explicit inherited factory and
origin. Preserve the interactive constructor and its organization/session constraints.
An inherited expectation may select one additional, distinct, nonempty Agent ID. This
only selects current catalog evidence; it grants no source or business authority.
The primary Agent remains the executor, and at most two frozen Agents and their
selected capabilities are exposed. Physical storage stays in the profile organization;
the inherited business/history scope remains the original run metadata.

Acquire both Agent keys and the exact run key in the existing single ordered write
set: Agent keys by ordinal Guid N, then run, then interactive chat when present. The
workspace gate is released while waiting for keys, reacquired for witness validation,
then released for the held snapshot lifetime. Ordinary catalog read leases still hold
the workspace gate and must not be nested here. No Process root or Project SQL lock
may be held while this acquisition waits. Subsequent business ordering is selected
catalog keys, Project locks, enlisted Process root fence. No new project references,
runtime partials, schema changes, cache or lock implementation are needed.

Catalog writes still compare the complete original catalog bytes after their key wait.
An unrelated Agent can publish during that wait; the waiting writer must then receive
an explicit conflict instead of overwriting the unrelated change. Prove both ordinary
release and that conflict path for each selected Agent, followed by an explicit fresh
read/update. Do not introduce a hidden whole-catalog retry or claim conflict-free
throughput from the fine-grained authority snapshot.

The first run retained the coarse catalog lease as a performance comparison. Its
forced-SQL-wait probe failed the unrelated-writer responsiveness bound; the selected
snapshot passed. Keep that negative comparison evidence and qualify the selected path
in the corrected acceptance lane. General coarse catalog callers remain a separate
capacity concern. The three new permission/capability assertion failures used a seeded
Agent whose defaults are restored by normalization; repair the fixture with the same
independent custom Agent setup used by the existing interactive tests, without changing
production normalization or snapshot semantics.

Validate exact scope, session, run/journal revisions and journal invariants for both
origins. Interactive snapshots retain original authority and latest-chat checks.
Inherited snapshots require schema 7, governed background purpose, original source,
profile and Agent binding, valid inherited planning metadata, and no chat or interactive
governance. Omit the chat witness only for that explicit origin. A missing selected
Agent refuses acquisition and releases all keys. Cancellation and witness failure must
also release every partially acquired key. Source revocation is captured as current
catalog evidence, not hidden or replaced with original permissions.

The Core publication guard keeps inherited effect dispatch closed until the real
parent-owned continuation is implemented. There is currently no admitted inherited
saved-effect continuation to prove positive publication without inventing work or
bypassing that owner. Its later implementation must require the same live work,
invocation claim, exact executing approved payload, original metadata and inherited
origin; work/basis duration checks remain mandatory. This snapshot is not a Process
dispatch grant, a manager continuation, or an enlisted publication fence. Those remain
separate checks at their existing owning boundaries.

Prove no-chat schema-7 acquisition, one/two selected Agents, missing/substituted identity,
immutable capability selection, exclusion of either selected Agent's changes, progress
for an unrelated third Agent, reversed source/executor acquisition without inversion,
cancellation after partial acquisition, and refusal to promote a snapshot into live
publication. Positive inherited publication belongs with the actual continuation. Repeat existing
interactive snapshot/publication and inherited journal/coordinator cases. Run standard
Release builds, exact discovery, frozen observed tests, architecture comparison and
full portability-static enforcement before the signed checkpoint. Actual workload
throughput and parent/business atomicity require later integration qualification.
