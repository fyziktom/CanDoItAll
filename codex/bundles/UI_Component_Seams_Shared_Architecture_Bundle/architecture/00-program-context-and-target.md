# Program context and target

## Objective

Reduce UI iteration cost and architectural coupling while preserving the application.
The desired outcome is a real feature UI cluster that can render and operate against
small deterministic substitutes, then compile and run in a lightweight browser host.

Current coupling includes rendering, semantic location, drafts, orchestration, persistence,
cross-module data, runtime operations, global dialogs, and static assets in the same graph.
Moving methods to another file or replacing many injected services with one service bag
does not establish the target boundary.

## Target responsibilities

| Responsibility | Owner |
|---|---|
| Semantic feature location and accepted transitions | One workspace instance; route adapter maps current host location |
| Draft and validation lifetime | One editor instance/session |
| Rendering and simple interaction | Cohesive feature component |
| Deterministic selection, normalization, presentation mapping | Pure policies near the owning feature |
| Coherent use cases and orchestration | Feature application/workflow services |
| Persistence, provider, browser/native, and host mechanisms | Outward adapters behind suitable contracts |
| Assembly wiring and route discovery | Host/composition |

A route page can delegate effects without delegating semantic authority. Controllers may
compose pure policies; they must not retain every responsibility of the former component.

## Dependency direction

Arrows mean depends on:

~~~mermaid
flowchart TD
    Production[Production host / composition] --> UI[Feature UI]
    Production --> Adapters[Production adapters]
    Sandbox[Small sandbox host] --> UI
    Sandbox --> Fakes[Scenario fakes]
    UI --> Contracts[Lightweight feature contracts]
    Adapters --> Contracts
    Fakes --> Contracts
    Adapters --> Application[Application / infrastructure]
    UI --> Shared[Selected reusable UI libraries]
~~~

These are responsibility boundaries, not a mandatory project per box. The eventual sandbox
must reach the real feature UI without reaching production runtime composition. Assess
transitive project references and public type ownership, not just direct references.

## Migration

First characterize, then extract one coherent seam in place. At a frozen checkpoint,
schedule physical extraction and sandbox proof independently from production navigation
binding. Measure both dependency closure and edit-to-visible-change behavior.

Apply the pattern to a different UI archetype before generalizing. Preserve existing
Conversations UI boundaries and other successful local designs instead of normalizing
every feature into an Agents-shaped template.
