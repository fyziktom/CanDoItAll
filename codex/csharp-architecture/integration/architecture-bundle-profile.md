# Architecture Bundle Profile

Use this profile for C# refactoring and architecture tasks.

## Root additions

```text
architecture/
  00-csharp-current-state-inventory.md
  01-csharp-boundary-map.md
  02-csharp-dependency-direction.md
  03-csharp-pattern-selection-records.md
  04-csharp-testability-plan.md
plan/
  architecture-checkpoints.md
reviews/
  csharp-architecture-gate.md
```

## Recommended subbundle phases

### SB01: Current-state inventory and characterization

Owns source inspection, responsibility map, risky behavior characterization, and test gap discovery.

### SB02: Contract extraction

Owns abstractions, DTOs, options, result records, contract project creation, and dependency-direction plan.

### SB03: Implementation extraction

Owns moving a cohesive responsibility into top-level types and/or implementation project.

### SB04: Construction and registration

Owns builder, factory, catalog provider, DI extension methods, and composition smoke.

### SB05: Runtime wiring and old-code deletion

Owns replacing old logic with thin delegation and removing duplicate partial-class behavior.

### SB06: Testability and extension proof

Owns isolated unit tests, negative tests, and fake extension proof.

### SB07: Architecture checkpoint

Owns architecture review gate, dependency proof, source assertions, and downstream unlock.

### SB08+: Dependent feature work

Starts only after foundation checkpoints pass.

## Checkpoint rule

Do not start dependent feature work until contract extraction, implementation extraction, construction/registration, and testability proof have passed.
