# Target Solution

## End State

The solution builds and passes focused regression validation after a conservative Microsoft Agent Framework package update to the 1.13 line. Current CanDoItAll behavior remains intact:

- agent runtime execution still streams and records usage;
- approvals remain enforced;
- required finalizer behavior remains authoritative for process-step output;
- provider lane gates, timeouts, failure redaction, and credential boundaries remain intact;
- process execution remains through existing API/adapter/project-structure bridge surfaces;
- workflow adapter behavior compiles and remains non-durable unless explicitly adopted in a later phase;
- evidence records what was changed and what was intentionally not adopted.

## Allowed Side Effects

- Direct package reference changes in approved project files.
- Minimal source compatibility edits in existing MAF and workflow adapter seams.
- Focused tests adjusted or added where package API behavior requires it.
- Evidence documentation created or updated.

## Disallowed Side Effects

- Product feature adoption from MAF 1.13.
- New process direct runtime tools.
- New process routes.
- Central package management introduction.
- Broad warning suppression.
- New project references without architecture gate repair.
- Runtime partial-class expansion as final architecture.

## Architecture Posture

Adapter compatibility is allowed. Architecture redesign is not. If the package update exposes a real design problem that cannot be fixed with a small adapter-safe change, implementation must stop, record the blocker, and ask for a separate architecture bundle or bundle repair.
