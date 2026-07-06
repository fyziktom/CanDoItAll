# Appendix for `candoitall-bundle-preparation`

Add this section to the preparation skill.

## C# Architecture Gate Addendum

When the bundle touches C# architecture, large-class refactoring, partial classes, tools, providers, memory protocols, process drivers, workflow executors, runtime composition, factories, builders, adapters, catalogs, or project references:

1. Load `candoitall-csharp-architecture-bundle-guard`.
2. Load `csharp-architecture-governor`.
3. Add the required C# architecture files:
   - `architecture/00-csharp-current-state-inventory.md`
   - `architecture/01-csharp-boundary-map.md`
   - `architecture/02-csharp-dependency-direction.md`
   - `architecture/03-csharp-pattern-selection-records.md`
   - `architecture/04-csharp-testability-plan.md`
   - `plan/architecture-checkpoints.md`
   - `reviews/csharp-architecture-gate.md`
4. Every architecture-relevant subbundle must include:
   - `## C# Architecture Impact`
   - `## Boundary Ownership`
   - `## Dependency Direction`
   - `## Pattern Decision`
   - `## Testability Contract`
   - `## Partial Class Policy`
   - `## Architecture Proof Required`
5. Critical foundation subbundles must close before dependent feature work starts.
6. Do not accept partial-class expansion as the planned final architecture unless it passes the partial-class policy and has a removal plan when temporary.
7. Require proof that extracted behavior can be unit-tested without constructing the original large class.
8. Require source assertions that moved behavior no longer lives in the old runtime or partial-class cluster.
