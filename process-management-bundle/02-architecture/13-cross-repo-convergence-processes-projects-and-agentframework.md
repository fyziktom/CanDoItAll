# Cross-repo convergence: Processes, Projects, and AgentFramework

This bundle now defines a stronger three-way separation.

## Processes

Owns:

- collaboration topology
- handoffs
- work briefs
- routing decisions
- runtime state
- journals
- conformance and operating-model semantics

## Projects

Owns:

- project scope
- hierarchy
- delivery context
- project-scoped navigation
- references to process runs or definitions where needed

## Future AgentFramework bridge

Owns only:

- external execution mechanics
- session continuity
- runtime-specific approvals
- external log / metric generation

but **not**:

- durable business identity
- business role templates
- canonical provider registry
- canonical process topology

## Why the separation matters

If these three concerns collapse into one model, CanDoItAll risks:

- a second hidden scheduling language
- dual registries
- orphaned runtime evidence
- and diagrams that no longer match production collaboration

## Required typed links

The process bundle therefore recommends typed references such as:

- process definition -> project
- process run -> project object / work item
- step run -> project object / deliverable / artifact
- external executor session -> process run / step / assignment correlation

This keeps the models aligned without making them identical.
