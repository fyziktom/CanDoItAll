# Sandboxability and future project boundaries

## Sandboxability is an architectural property

A browser sandbox is useful only when it can reference a small UI graph. Referencing a
current large feature Razor project together with all of its runtime and persistence
dependencies may provide visual examples but will not materially reduce watch/build cost.

The program therefore treats sandboxability as a readiness result of seam extraction.

## Sandbox levels

### Level 1 — isolated component harness

The component can render in a component test or minimal renderer with:

- explicit state/parameters;
- component-library services;
- small fake feature ports;
- no database;
- no full production service collection;
- no service locator.

This level can be reached before physical project extraction.

### Level 2 — module browser catalog

A future module UI project exposes deterministic scenarios:

```text
loading
empty
normal
large-data
validation-error
operation-error
selected-detail
open-overlay
restricted/read-only
```

The catalog supplies state and fake port behavior. Scenarios should be directly
addressable so a developer can return to the same visual state.

### Level 3 — integration host

A small host composes several module UI projects and selected real adapters without
starting the full application runtime. This is for integration surfaces, not a replacement
for the production host.

## Live sibling decision

The future sandbox continues to use live sibling source references for Components and
FileTools. This bundle does not introduce package snapshots. The performance gain must
come from excluding unrelated CanDoItAll feature/runtime projects, not from disconnecting
the UI libraries the designer actively edits.

## Sandbox-ready checklist

A component is sandbox-ready when:

1. Its required semantic state is explicit.
2. Its route-significant state is controlled.
3. Its I/O goes through small fakeable ports or an existing coherent application
   abstraction.
4. It does not require `IDbContextFactory<AppDbContext>`.
5. It does not require `IServiceProvider`.
6. It does not require production hosted services to render.
7. It does not imperatively open a significant detail that the scenario cannot express.
8. Loading, empty, normal, error, and relevant overlay states can be constructed.
9. Static assets and CSS ownership are known.
10. The component can be rendered without registering unrelated modules.

## Project-extraction-ready checklist

A component cluster is ready for `CanDoItAll.Modules.<Feature>.UI` when:

- the future project references are known and narrow;
- no inward reference to persistence or web composition is required;
- cross-module feature dependencies are represented by stable contracts;
- state and intent types can move with the UI;
- controller implementations can remain outside if they require heavier dependencies;
- component tests do not depend on internal types from the old monolithic project;
- CSS/static assets have a clear destination;
- moving the files will not change ownership or behavior.

## AppComponents extraction readiness

Move to `AppComponents` only when the component is application-wide and feature-neutral.
A move that requires adding a module reference to `AppComponents` is not ready and is
normally the wrong destination.

## Scenario quality

Do not build a sandbox by recreating the full production DI graph with mocks. A scenario
must declare the state it intends to demonstrate. Fake behavior should be small,
deterministic, and local to the feature catalog.

## Timing

Create the first browser sandbox after the first real lightweight module UI boundary
exists. Before that point, use component-level scenarios/tests to validate the seam, but
do not claim a large current module project is isolated merely because it has a preview
route.
