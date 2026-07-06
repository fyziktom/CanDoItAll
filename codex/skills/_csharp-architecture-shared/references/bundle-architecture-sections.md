# Required Bundle Architecture Sections

When a CanDoItAll bundle touches C# architecture, add these files or equivalent sections.

## `architecture/00-csharp-current-state-inventory.md`

Must include:

- source files inspected
- large classes and partial classes
- constructor dependency counts
- direct instantiation points
- provider/tool/driver/memory responsibilities
- current tests
- missing tests
- risk notes

## `architecture/01-csharp-boundary-map.md`

Must include:

- target projects
- target top-level types
- contracts vs implementations
- composition root responsibilities
- old class responsibilities to remove or leave
- temporary bridges and removal plan

## `architecture/02-csharp-dependency-direction.md`

Must include:

- current project references
- target project references
- forbidden references
- cycle risk
- new contract projects needed
- build/test proof required

## `architecture/03-csharp-pattern-selection-records.md`

Must include one record per selected pattern:

- problem force
- selected pattern
- rejected alternatives
- new types/projects
- how it improves testability
- proof required

## `architecture/04-csharp-testability-plan.md`

Must include:

- characterization tests
- isolated unit tests
- negative tests
- integration/composition smoke tests
- fake provider/tool/driver proof if relevant

## `plan/architecture-checkpoints.md`

Must include checkpoints after foundation phases:

- dependency graph review
- partial-class policy review
- testability review
- old-class shrink proof
- next-phase unlock decision
