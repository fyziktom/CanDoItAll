# C# Testability Plan

## Characterization

- Preserve existing workflow overview, process projection, Agent Overview, and project list tests; new queries must not alter their contracts.
- Component characterization confirms `/` and `/dashboard` remain route aliases and PageTitle remains Dashboard.

## Isolated Behavioral Tests

- Dashboard cache: cross-circuit/cross-scope fresh hit, exact five-minute success/failure eligibility, force while idle, force during in-flight, concurrent coalescing, caller cancellation that does not cancel shared refresh, profile-ID/fingerprint/generation change during load, fault cleanup, prior-success retention with explicit stale metadata, and initial-failure throttling.
- Dashboard load runner: each actual refresh creates and asynchronously disposes one fresh scope, resolves one fresh scoped loader, uses application-shutdown cancellation for shared work, and never exposes/retains `IServiceProvider` outside the lifetime adapter.
- Snapshot graph: projects, workflows, and processes are `ImmutableArray<T>`; each boundary value 0/5 succeeds and a source count of 6 fails explicitly before publication.
- Recent projects: 8 seeded rows including equal timestamps; exactly deterministic newest 5 and no tracked entities.
- Workflow: mixed active/terminal returns only active; no active returns latest 5; both active states included; >5 bounded; aggregate overview fake remains untouched.
- Process: canonical mixed states active-only; no-active latest 5; >500 newer terminal projections do not hide an older active state; result/projection-ID bounds; counting fakes prove no assignment/diagnostic/history/agent/usage enrichment calls; lag remains visible.
- Agent totals: projection totals/UpdatedAt copied exactly; unknown cost not synthesized; store exception propagates.
- QuickActionCard/Home: semantic routes, states, tabs, countdown/force/expiry/disposal.

## Adversarial Negative Tests

- A shallow implementation that infers activity from a bounded projection page must fail when more than 500 newer terminal projections precede an older active canonical run.
- A workflow implementation that queries Running and Waiting separately then concatenates must fail a max-five test.
- A cache keyed only by time/profile ID must fail when generation changes; one keyed only by generation must fail when profile ID changes.
- Catch-and-return-empty must fail because seeded previous data plus a throwing refresh must render an explicit stale/error state.
- A process wrapper that calls the full workspace query must fail counting fake assertions for enrichment collaborators.

## Composition And Integration Smoke

- Existing DI module setup resolves all four narrow queries and the scoped loader, while singleton service/cache/runner resolve without manually constructing broad facades.
- Two independent scopes/circuits share the same cached snapshot and one concurrent refresh wave, while separate actual refreshes resolve distinct scoped loader instances.
- Full solution build and real app route load prove composition.

## Architecture Testability Gate

Reject closure if a query can only be tested by constructing Home, `AgentFrameworkWorkspaceService`, full process runtime, or a service provider; if provider resolution appears outside `DashboardSnapshotLoadRunner`; if tests assert counts without data-selection semantics; if the immutable hard-five invariant is not tested; or if new types simply delegate to the broad paths rejected above.
