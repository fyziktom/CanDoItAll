# Contract for implementation bundles referencing this base

## Mandatory reference

Every child bundle must include the exact block from
`templates/child-bundle-reference-block.md`.

## Required preparation content

### 1. Outcome and scope

- user-visible and architectural outcome;
- exact component/page cluster;
- source files and projects;
- explicit non-goals;
- whether physical relocation, routing, redesign, or Manager work is excluded.

### 2. Current responsibility inventory

For every primary component:

- rendering responsibility;
- semantic state owned;
- local/draft/transient state;
- injected dependencies;
- direct persistence/runtime/host access;
- dialogs/overlays opened;
- navigation performed;
- cross-module types used;
- relevant tests and source-shape guards.

Use `templates/component-boundary-assessment-template.md`.

### 3. Target seam decisions

State which extraction choice is used and why:

- pure policy/mapper/reducer;
- existing application contract;
- feature controller/facade;
- new explicit I/O/host port;
- child presentational component;
- controlled state and typed callback/intent.

Record rejected alternatives when wrapper/interface inflation is a risk.

### 4. Ownership after implementation

The bundle must state:

- page/workspace-owned state;
- component-local state;
- controller-owned orchestration;
- application/infrastructure-owned operations;
- future physical destination;
- remaining direct dependencies and justification.

### 5. Test impact

The child bundle owns its test selection. It must classify affected tests:

```text
keep
rewrite
remove
temporary migration proof
```

It must explicitly search for implementation-shape tests in the touched area. It must not
copy product test commands from this shared base because this base contains none.

### 6. Acceptance criteria

At minimum:

- current behavior remains unless explicitly changed;
- moved responsibility is absent from the original component;
- state has one owner;
- direct dependencies are reduced or made coherent;
- no new `IServiceProvider` or direct EF access exists in Razor;
- no new partial file is used as the final boundary;
- no feature reference is added to `AppComponents`;
- component can be rendered with explicit state and minimal fakeable dependencies;
- source-shape tests are not added or preserved without durable justification;
- route-ready, sandbox-ready, and project-extraction-ready decisions are recorded.

## Implementation constraints

- Keep files in place unless relocation is an explicit owned outcome.
- Do not opportunistically refactor adjacent modules.
- Do not create a generic base class for unrelated component lifecycles.
- Do not create a facade that mirrors all old services.
- Do not create new DTOs that merely copy stable existing models.
- Do not implement URL routing in an in-place seam bundle unless the bundle explicitly
  belongs to the later bookmarkability stage.
- Preserve live sibling source development.
- Comments added to source code must be in English.

## Closure report

The execution report must contain:

```text
Shared base reference:
Baseline executed:
Responsibilities removed:
New boundaries:
Remaining coupling:
State owner:
Test cleanup:
Route-ready:
Sandbox-ready:
Project-extraction-ready:
Shared-base deviation:
Durable documentation candidate:
```

## Reopen triggers

Reopen the child bundle when:

- a later step discovers duplicate state ownership;
- the new controller becomes a service bag or god object;
- sandbox construction still requires the full production runtime;
- physical extraction introduces a dependency cycle;
- routing requires child components to know page query keys;
- an obsolete source-shape test blocks valid simplification;
- a dependency was hidden rather than removed.
