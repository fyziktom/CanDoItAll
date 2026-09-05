# Component ownership and placement

## Placement decisions

| Location | Owns | Excludes |
|---|---|---|
| Components / FileTools | Product-neutral reusable capabilities | Application feature semantics |
| AppComponents | Application-wide feature-neutral shell, navigation, overlays, and host adaptation | Concrete module implementations |
| Cohesive application UI families | Shared semantic UI such as Conversations.Components and Conversations.Shell | Unrelated feature orchestration |
| Feature UI | Feature components, state, presentation, and consumer-owned ports | Persistence, provider implementations, Web composition |
| Feature contracts/application | Stable feature use-case contracts and behavior | Host-specific rendering |
| Host/composition and adapters | Concrete registrations and production mechanisms | Reusable feature rendering logic |

Reuse by multiple modules does not remove the original feature's ownership. Consumers
may reference an intentional feature UI/contract boundary; arbitrary references between
large module implementation assemblies are not an acceptable substitute.

## Public contract audit

Before selecting an existing model, record its declaring assembly, transitive graph,
mutability, sensitive fields, and necessary UI semantics.

Reuse a lightweight contract when it fits. Move a truly shared contract at its owning
feature boundary when justified. Introduce a narrow projection when it removes a real
assembly, mutability, security, or compatibility dependency. A projection is not forbidden
merely because some fields resemble another model.

Do not create duplicate DTO families only for renaming. Do not retain an implementation
assembly dependency merely to avoid a small justified projection.

## In-place and later movement

Preserve current component placement during logical extraction unless a child explicitly
owns relocation. New cohesive types and necessary local child seams are permitted.
Inventory cross-module prerequisites before execution and assign them an owner; do not
discover them only in final closure.

A new project or cross-module public contract change needs a dependency decision and
affected-consumer plan. Routine local type names and justified host components are not
permission gates. AppComponents -> concrete feature module remains a forbidden direction.

CSS, asset URLs, JS modules, source-generation/build behavior, and route assembly discovery
are part of placement. Moving files without moving these responsibilities is incomplete.
