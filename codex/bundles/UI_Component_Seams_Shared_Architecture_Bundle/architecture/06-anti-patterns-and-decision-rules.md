# Anti-patterns and decision rules

## Rejected anti-patterns

### Wrapper pyramid

```text
Page -> Container -> View -> Presenter -> Facade -> Service
```

Rejected when each layer only forwards parameters or methods. Add the smallest layer that
creates a real boundary.

### Interface per method

Rejected when deterministic local logic is hidden behind interfaces without substitution
or I/O. Prefer pure top-level types.

### Service-bag facade

Rejected when a facade exposes the same service graph through pass-through methods or
stores `IServiceProvider`.

### God controller

Rejected when all responsibilities from a god component move into one equally broad
controller. Split by cohesive workflow/capability, not by Razor versus C#.

### Duplicate state models without purpose

Rejected when a new view model copies an existing stable read/editor model field-for-field
and adds no presentation or compatibility boundary.

### Fake extraction

Rejected when code moves to another file but:

- the old component still decides the same policy;
- the new class reaches back into component internals;
- both old and new state machines remain active;
- dependency direction is unchanged;
- direct testing still requires the full runtime.

### AppComponents dumping ground

Rejected when a feature component is moved downward only because several callers use it,
or when the move requires module references.

### Premature physical move

Rejected when files are moved into a new UI project before state, I/O, CSS, and project
dependencies are understood.

### Partial-class expansion

Rejected when another partial file is added to make a large page appear organized without
changing ownership.

### Generic lifecycle base

Rejected when a base component attempts to own loading, canonicalization, overlays,
dirty-state navigation, Workbench integration, and error handling for unrelated features.

### Source-shape architecture tests

Rejected when tests preserve file counts, private method names, or exact source layout
rather than a durable invariant.

## Compact decision guide

### Should logic leave the Razor component?

Move it when it is:

- deterministic policy or transformation;
- repeated orchestration;
- external I/O;
- cross-module coordination;
- independently testable state reduction;
- route-significant state ownership that belongs to the page/workspace.

Keep it local when it is:

- small presentation-only state;
- element references;
- hover/dropdown/animation state;
- simple event adaptation with no business decision.

### Should a new interface be created?

Create one when:

- implementation substitution is required;
- the boundary crosses persistence, remote, browser/native, runtime, or host I/O;
- a sandbox needs a fake for a meaningful capability;
- dependency direction requires the contract to move inward.

Do not create one when:

- a static/pure function is enough;
- an existing application contract already fits;
- only one private helper is being moved;
- the interface would mirror a concrete service without changing ownership.

### Should a typed intent be used?

Use one when several actions modify one coherent semantic state. Use a simple callback for
one direct value change.

### Should a component move now?

Only when the child bundle explicitly owns physical extraction and the component is
sandbox-ready and project-extraction-ready. Otherwise record the destination and keep it
in place.

### Should a test guard the rule?

Only when the rule is durable, mechanically inspectable, and likely to regress. Do not
test temporary bundle structure or accidental source shape.
