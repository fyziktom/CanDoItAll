# Service, I/O, and controller seams

## Goal

Reduce the number and breadth of responsibilities owned by Razor components without
replacing visible coupling with empty layers.

## Dependency categories

### Technical component-local dependencies

These may remain directly injected when they are intrinsic to the component:

- `IJSRuntime` for browser behavior owned by the component;
- component-library dialog, tooltip, renderer, or focus services;
- `NavigationManager` in the route-owning page;
- logging when the component itself owns meaningful operational events.

### Feature application dependencies

A component may depend on one or a small number of coherent feature contracts. Prefer
existing application interfaces when they already represent the required use case.

### Infrastructure or composition dependencies

These should not be owned directly by Razor components:

- `IDbContextFactory<AppDbContext>`;
- persistence implementations;
- runtime composition services;
- concrete external provider drivers;
- `IServiceProvider`;
- cross-module implementation services used only to assemble a UI snapshot.

## Extraction choices

### Choice A — pure extraction

Use a top-level function, static policy, mapper, reducer, builder, or immutable record
when no I/O or substitution is required.

Good candidates:

- filtering;
- selection normalization;
- available-action policy;
- state reduction;
- display mapping;
- stable tree construction;
- dependency normalization;
- URL-independent state canonicalization.

Do not create an interface for pure deterministic logic by default.

### Choice B — feature-scoped controller/facade

Use one scoped controller/facade when a component coordinates several services into one
cohesive UI workflow, for example:

- loading an editor and its supporting catalogs;
- saving/deleting and returning an outcome;
- opening files through several host capabilities;
- assembling a dashboard snapshot from several sources.

The facade must expose a UI workflow result, not mirror every underlying service method.

A useful facade reduces dependencies and moves orchestration ownership. A useless facade
is a service bag with the same old dependencies hidden behind pass-through methods.

### Choice C — explicit port

Create an interface at a real boundary:

- persistence or remote query/command;
- browser/native host operation;
- file or process launch;
- provider/runtime operation;
- navigation/history host;
- capability that must be replaced by a sandbox fake.

Place the contract in the layer that owns the need, and place the implementation outward.
Do not duplicate an existing coherent application contract solely to rename it for UI.

## Controller quality rules

A feature controller/facade must:

- own a coherent workflow rather than the whole page;
- return typed results or state;
- accept cancellation where work can overlap;
- avoid retaining component instances or `RenderFragment`;
- avoid depending on `NavigationManager` unless navigation is its explicit boundary;
- avoid returning infrastructure entities when stable read models already exist;
- avoid becoming the new god object;
- permit direct unit testing without constructing the full web host.

## Error and notification ownership

Lower-level operations return typed failures or throw documented exceptions. The
page/container decides how to present errors. Do not make a reusable view component
depend on global notifications simply to display a local validation state.

Dialog and notification services may remain in a top-level feature container during the
transition. They should not be spread through nested views.

## Async state safety

For components with overlapping loads:

- identify the state key or generation associated with each request;
- cancel or ignore stale completion;
- do not let a late response overwrite a newer selection;
- keep loading state granular to the region being updated;
- avoid performing route-significant initialization only in `OnAfterRenderAsync`.

These rules prepare the component for later query-only navigation and direct deep links.

## Evidence of real extraction

A child bundle must show that:

- the old component no longer owns the moved decision or operation;
- direct dependencies decreased or became more coherent;
- the extracted unit can be tested or substituted independently;
- no duplicate state machine remains in both component and controller;
- the new boundary is named after the owned responsibility, not the source file.
