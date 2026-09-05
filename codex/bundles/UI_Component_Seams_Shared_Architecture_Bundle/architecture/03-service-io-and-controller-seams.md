# Service, I/O, and controller seams

## Choose by responsibility

- Extract deterministic policy/mapping/normalization into ordinary top-level types.
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
An aggregate query must not force every region to reload together.

## Mutation and failure boundaries

Record validation, confirmation, authorization, expected version, persistence commit,
post-commit refresh, notifications, and completion channels. Cancellation of a view does
not prove a command did not commit. Represent known committed outcomes separately from
refresh failures; an uncertain outcome must not cause an automatic duplicate write.

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
