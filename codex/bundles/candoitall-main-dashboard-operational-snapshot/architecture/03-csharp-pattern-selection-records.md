# C# Pattern Selection Records

## PSR-01 — App-Process Cache With A Scoped-Load Runner

- Problem force: five-minute reuse, force bypass, profile-ID/fingerprint/generation isolation, and concurrent caller coalescing.
- Selected pattern: DI-managed singleton `DashboardSnapshotService` and `DashboardSnapshotCache` share global operational data across Blazor circuits. Cache identity is the active database runtime profile ID, fingerprint, and generation. Each actual refresh calls singleton `IDashboardSnapshotLoadRunner`; that lifetime adapter creates a fresh async DI scope, resolves scoped `IDashboardSnapshotLoader`, awaits it, and disposes the scope. The cache retains only immutable snapshots, typed runtime identity, last-attempt/failure metadata, and one in-flight task.
- Lifetime boundary: `IServiceScopeFactory`/`IServiceProvider` resolution is allowed only inside `DashboardSnapshotLoadRunner`, whose sole job is adapting singleton cache lifetime to scoped query/loader lifetime. UI, query services, loader composition, and cache policy do not resolve services. No user-specific data enters the shared snapshot.
- Rejected: scoped-only cache (repeats work per circuit), static mutable cache, distributed cache (not justified for a local snapshot), bare `IMemoryCache` without coalescing/failure semantics, Home fields, and locks held across I/O.
- Testability: a fake typed runner and `TimeProvider` prove hits, expiry, force, failure throttling, caller-cancellation isolation, runtime changes, and concurrency. A DI composition test proves every actual runner call creates/disposes one scope and resolves a fresh scoped loader.
- Proof: one loader invocation per concurrent wave across circuits/scopes; caller cancellation does not cancel shared work; failure cannot cause a retry storm; no faulted task or scoped service is retained.

## PSR-02 — Query Services With Thin Read Models

- Problem force: existing list/overview services load substantially more data than Home needs.
- Selected pattern: four cohesive typed query services returning bounded dashboard read models; the loader validates the hard maximum of five and converts the entire cached collection graph to `ImmutableArray<T>`.
- Rejected: reuse `ProjectsService.ListAsync`, aggregate `WorkflowOverviewSnapshot`, full process workspace, or full Agent Overview; direct store injection into the loader.
- Testability: each query is instantiated directly with context/store fakes and mixed/adversarial data.
- Migration: existing consumers remain unchanged; Home alone moves to the new composite snapshot.

## PSR-03 — Explicit Active-First Selection Policy

- Problem force: “active runs or fallback latest five” must not become active-plus-recent or per-state concatenation.
- Selected pattern: store/query result with typed `Active`/`RecentFallback` mode and deterministic max-five rows.
- Rejected: UI filtering, two single-state pages concatenated to ten, or silent fallback on query error.
- Testability: mixed active/terminal input proves only active rows; no-active input proves latest five; >5 proves bound.

## PSR-04 — AppComponents Wrapper

- Problem force: four identical semantic navigation cards need consistent square/icon/focus behavior and future reuse.
- Selected pattern: small Razor composition wrapper over BaseLib Card/Button/Icon/Stack primitives with typed parameters and `Href`.
- Rejected: repeated page-local markup/CSS or modifying BaseLib for a product-specific navigation card.
- Testability: bUnit verifies semantic link, accessible label, icon, class contract, and no domain dependency.

## Anti-Patterns Explicitly Rejected

- Partial-class expansion, nested dashboard types in a large service, service location outside the dedicated lifetime adapter, broad manager/facade growth, catch-and-return-empty, fixture-specific branches, static mutable cache, background hosted refresh, and interface shells whose only implementation delegates back to the old broad service.
