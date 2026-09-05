# Anti-patterns and decision rules

Reject:
- A wrapper pyramid whose layers only forward.
- A service-bag facade hiding the same dependency graph or IServiceProvider.
- A god controller or route page accumulating unrelated effects.
- An interface quota or a mandatory interface for every pure helper.
- DTO reuse that drags implementation assemblies into UI, or copies without boundary value.
- Parent-only injection checks advertised as complete sandbox proof.
- Mutable session data shared through circuit-scoped services.
- An initial-data shortcut that bypasses the only production lifecycle exercised by tests.
- A route section update that recreates the editor and loses its draft.
- A single aggregate that destroys independent loading and failure boundaries.
- A generic base owning unrelated lifecycle, navigation, overlays, and dirty-state policy.
- Partial-file growth used as architectural separation.
- Private shape/count tests that block legitimate refactoring.
- Physical movement, routing, visual redesign, and behavior fixes mixed without explicit ownership.

## Positive alternatives

Keep a component local when behavior is rendering, element references, focus, simple event
adaptation, or transient interaction. Extract policy, external operations, repeated workflow,
or duplicated semantic ownership.

Allow a thin host when it owns dialog/session lifetime, composition, focus/result adaptation,
or a real route boundary. Allow section components when they isolate a cohesive responsibility.
Neither is a requirement to split every editor.

Allow a projection when it removes a concrete type/assembly/mutability dependency. Prefer
existing lightweight contracts and successful reusable UI families.

A new interface requires a responsibility/substitution explanation and proof. A fourth
interface is neither inherently good nor inherently bad. No routine naming change requires
an owner approval ritual.

Separate physical extraction and navigation binding after the state seam; prove each with
the evidence appropriate to its claim. Preserve runtime behavior and make residual coupling
explicit instead of renaming it.
