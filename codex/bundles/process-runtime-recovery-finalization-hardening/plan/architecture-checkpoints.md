# Architecture Checkpoints

## Entry Checkpoint

- Confirm implementation is still scoped to process runtime/recovery hardening.
- Confirm this bundle has not been superseded by a newer process-runtime architecture bundle.
- Re-run or refresh CodeAnalytics if source has materially changed since `snap-20260707213600-f58ac646`.

## Per-Subbundle Checkpoint

- Validate project ownership before adding a type.
- Validate dependency direction before adding a reference.
- Validate testability before adding a service dependency.
- Validate the partial-class policy before editing `ProcessRuntimeEngine` or `AgentFrameworkProcessExecutionAdapter`.
- Validate generic/domain boundary before adding any term that smells like software-development, MAF, browser, GitHub, or .NET delivery to runtime contracts.

## C# Architecture Gate

- No new project cycles.
- Runtime has no dependency on Application, Persistence, Modules, AgentFramework, MAF, Blazor, project-structure, browser, GitHub, or software-development-specific packages.
- New contracts are strongly typed and do not use magic strings for identifiers, routes, status, or failure classes.
- Recovery taxonomy has an explicit unknown/manager-required path and no silent fallback.
- Finalization and handoff state transitions are unit-testable without Module integration.
- Driver-specific policy is implemented through explicit driver abstractions or concrete drivers, not runtime type checks.

## Partial Class Policy

- Do not add new partial files to current large clusters as the final design.
- Temporary edits to existing partial files must move behavior toward extracted services.
- Each extraction must have source assertions that the moved responsibility no longer lives in the original partial cluster.
- A partial-class expansion requires a written temporary-removal plan in the execution report.

## Closure Checkpoint

- CodeAnalytics dependency graph refreshed and no cycles introduced.
- Requirement traceability marks every requirement as implemented, deferred with reason, or explicitly out of scope.
- Raw architect notes are closed line by line in `reviews/01-execution-report.md`.
- Proof manifests exist for critical subbundles and include changed-file hashes, semantic invariants, failing-first proof, passing proof, source assertions, and anti-stub audit.
