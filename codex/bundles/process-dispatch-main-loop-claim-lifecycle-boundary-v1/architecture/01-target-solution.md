# Target Solution

- Keep all new production boundaries inside `CanDoItAll.Modules.Processes`.
- Introduce module-local claim persistence, claim coordination and heartbeat coordination boundaries.
- Introduce route context, route stage contracts and route handlers/facade that preserve the current order exactly.
- Introduce exception/failure closure helpers that make failure transition behavior explicit.
- Keep future driver concepts documentation-only.

Primary architecture reference: `bundle://architecture/01-dispatch-loop-claim-lifecycle-boundary.md`.
