# Service, I/O, and controller seams

## Choose by responsibility

- Extract deterministic policy/mapping/normalization into ordinary non-Razor types. Policies must not call static methods on component classes. Reuse a model policy where dependencies permit; do not add a domain reference to generic UI merely to share trivial string trimming.
- Reuse an existing cohesive application contract where its type graph is suitable.
- Add a workflow boundary for meaningful multi-service coordination.
- Add an I/O/host port when substitution or dependency direction requires it.
- Add a host/component boundary only when it owns real state, lifetime, composition, or
  rendering responsibility.

There is no fixed number of interfaces or mandatory controller per component. Record
the force, rejected simpler alternative, direct test seam, dependencies, and responsibility
removed from the old owner. Equivalent naming is routine implementation judgment.

## Controller constraints

Do not retain component instances, RenderFragments, NavigationManager, or dialog presentation
inside application operations. Do not hide IServiceProvider or return a bag of old services.
Keep state out of shared controller instances. Decompose independent policies/workflows when
needed; measuring responsibility matters more than counting methods or files.

Testability of a component through a fake controller does not establish testability of the
controller implementation. Audit its construction. Test pure rules directly and production
adapters through appropriately scoped integration fixtures; add a narrow dependency port
when that creates a real boundary rather than mocking an entire runtime.

## Loading boundaries

Distinguish shell summary, overview, usage, catalog, selected detail, and lazy supporting
data. Record triggers, keys, cached data validity, partial failures, retries, cancellation,
and forbidden eager reads. One interface may expose more than one cohesive operation.
An aggregate query must not force every region to reload together. Catalog/reference reads
belong on the existing read contract, not a command port merely because a command triggers
them. A core target/capability load must distinguish Loading, Ready and Failed. Failed core
load retains the requested identity, hides the editable form and exposes same-target Retry;
it must never present an empty create draft. Independent provider/secret failures can remain
partial when their absent data cannot silently overwrite saved settings.

## Mutation and failure boundaries

Record validation, confirmation, authorization, expected version, persistence commit,
post-commit refresh, notifications, and completion channels. Cancellation of a view does
not prove a command did not commit. Represent known committed outcomes separately from
refresh failures; an uncertain outcome must not cause an automatic duplicate write.

Define and prove four cases at the producer's actual commit boundary:

| Outcome | Required treatment |
|---|---|
| Known rejected before write | Preserve draft and permit correction/retry; distinguish concurrency conflict |
| Known committed | Preserve returned identity; reconcile version and publish completion once |
| Committed with secondary warning | Retain identity and visible warning; retry reads without replaying the mutation |
| Genuinely unknown persistence result | Preserve draft, prevent blind replay, provide an explicit recovery path |

A general catch of persistence exceptions as unknown is insufficient when producers already
carry commit identity or deterministic validation rejection. Use a typed validation exception
or result produced strictly before persistence; do not classify arbitrary InvalidOperationException
from an entire save call as rejected. Include cache invalidation and projection work after the
commit in the committed-warning boundary. Owner cancellation propagates unless the producer
already knows a commit occurred; in that case commit knowledge wins over the cancellation.

Preserve existing provider/secret partial loads and lazy catalog semantics, including
unavailable saved selections. Empty due to failure must not silently erase permissions.
Do not move backend policy or authorization into a UI controller as the only enforcement.

Classify baseline behavior as preserve, accepted isolation safeguard, or unresolved defect.
Characterize unresolved behavior and obtain an explicit design decision before changing
user-visible semantics; do not silently fix it or enshrine unsafe behavior as intended.

## Dependency checks

Parent injections, descendants, triggered dialogs, controller constructors, public type
assemblies, registrations, and assets all contribute to the boundary. Technical services
such as JS/focus can remain where owned; direct EF and service location in target Razor
are extraction targets. Every retained child operation must be named and scenario-tested.

## Independent refresh and multiple authorities

Catalog metadata refresh and selected-editor reconciliation are separate operations. An
unrelated external change must preserve an editable draft, its EditContext, typed section
and raw text fields. An affected authoritative read-only projection reloads by its retained
identity. Retirement, missing identity or malformed projection fails closed; it does not
silently select a different object. Unknown scope refreshes non-destructive metadata and
exposes explicit stale/retry state.

Child effects emit typed change scope from the producer: semantic operation, affected and
retired identities, changed authority/field scope, membership impact, commit knowledge and
secondary warning. An unqualified Changed event cannot support selective reconciliation.
Do not infer known affected identities from the current UI selection.

Document local intent, source configuration/trust, remote publication shape, cached catalog
and materialized runtime projection as distinct authorities. Mutation eligibility belongs
at the authoritative backend boundary before credential resolution, diagnostic maintenance
or connector effects. Allowed read/diagnostic operations remain separately classified.
Disabled controls are presentation, not enforcement.

Read/render methods must not hide persistent lifecycle transitions. Create permanent
public or audit identity through an explicit command; explain whether unpublishing,
retirement and deletion differ. Never recycle public IDs or silently remove audit records.

Bind a known first-save identity before any secondary read. A failed refresh cannot turn
a saved object back into a New draft. Reconciliation retries projection/read work without
replaying the authoritative write, preserving later edits while updating identity/version.

## Diagnostic publication and semantic effect ownership

Controlled mutation presentation, exact canonical recovery, diagnostic input fencing and target/panel/circuit lifetime distinctions are now proven by the [mutation/effect feedback](../reviews/11-authoritative-mutations-and-effect-lifetimes.md). Apply those rules at the actual producer boundary. A view generation alone cannot resolve an in-flight attempt, cancellation after dispatch cannot prove rollback, and a successful diagnostic does not prove its evidence was persisted. Cross-authority revision checks must state their real consistency limit.
