# Post-Phase Validation Roles

## Required Roles

- Architecture reviewer
  checks boundary integrity, dependency drift, and premature abstractions.
- Canonical model reviewer
  checks source-of-truth ownership, projection boundaries, and duplicated registries.
- Persistence and seed reviewer
  checks migrations, append-only evidence strategy, storage seams, and seed completeness.
- Helper and maintainability reviewer
  checks helper isolation, large classes, mixed responsibilities, and refactor pressure.
- UI and component reviewer
  checks shared-component usage, compact layout, overlays, and Playwright evidence.
- Cross-repo convergence reviewer
  checks AgentFramework and IPFS seam decisions against the planned ownership rules.

## Mandatory Review Outputs

- defect list with owning repair subbundle
- reopen recommendation when a prior phase foundation is no longer trusted
- explicit “safe to continue” or “phase must stop” decision
