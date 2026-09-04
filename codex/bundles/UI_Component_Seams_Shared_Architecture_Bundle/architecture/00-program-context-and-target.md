# Program context and target architecture

## Problem being addressed

Slow UI iteration is not only a tooling problem. Many feature Razor projects and
components currently combine several concerns:

- rendering and presentation state;
- data loading and persistence access;
- application orchestration;
- cross-module coordination;
- dialog and notification hosting;
- navigation state;
- browser/native actions;
- feature policy and transformation logic.

This makes a component expensive to instantiate, difficult to isolate, difficult to move,
and likely to pull a broad project graph into any future sandbox. Improving the
development Manager before correcting these boundaries would optimize around the current
coupling rather than remove it.

## Program objective

Create stable logical seams before physical extraction.

A component or coherent component cluster should converge toward:

```text
route/page/workspace owner
    owns semantic workspace state
    receives host location state later
    handles navigation-level intents

feature component
    renders typed state
    owns only local presentation/draft state
    emits typed callbacks or intents

pure policies and mappers
    normalize, filter, map, reduce, and decide without DI

feature UI controller/facade, only when needed
    coordinates a coherent UI workflow across multiple services

application and infrastructure adapters
    perform persistence, runtime, file, provider, or host operations
```

## Two-stage architecture change

### Stage A — logical separation in place

- Keep source files in their current module/project.
- Make ownership explicit.
- Extract pure logic.
- Replace hidden service location and direct persistence access.
- Introduce controlled state and typed intents for route-significant interactions.
- Remove duplicated responsibility from the original component.
- Add only the tests required by the child bundle.

### Stage B — physical extraction after proof

- Classify the component as application-wide or feature-owned.
- Move it into `AppComponents` or `CanDoItAll.Modules.<Feature>.UI`.
- Narrow project references.
- Create browser sandbox scenarios.
- Point a small sandbox host at the new UI project.
- Measure the resulting watch/build graph separately.

Do not combine Stage A and Stage B by default. A child bundle may combine them only when
the component is already logically isolated and the move is low-risk.

## Target dependency direction

```text
shared product-neutral libraries
    CanDoItAll.Components
    CanDoItAll.FileTools
            |
            v
application-wide UI
    CanDoItAll.AppComponents
            |
            v
feature UI
    CanDoItAll.Modules.<Feature>.UI
            |
            v
web host and feature composition
    CanDoItAll.Web
    CanDoItAll.Composition
```

Feature UI may depend on its own stable contracts/application abstractions. It must not
reach into web-host composition, persistence implementations, or another feature's
implementation project merely to render.

## Success characteristics

At program maturity:

- significant UI state has one owner;
- child components do not build parent URLs;
- feature UI can be rendered from explicit scenario state;
- a sandbox does not require full production DI;
- `IServiceProvider` is absent from Razor components;
- direct EF access is absent from Razor components;
- large partial page classes shrink by responsibility;
- module UI project references are narrow and intentional;
- `AppComponents` remains feature-neutral;
- architecture tests protect durable boundaries rather than source layout;
- later routing work binds URL codecs to existing state instead of redesigning component
  ownership again.
