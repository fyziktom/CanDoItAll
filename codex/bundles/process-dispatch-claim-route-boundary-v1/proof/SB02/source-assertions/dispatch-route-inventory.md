# SB02 Source Assertions

- `bundle://inventories/02-current-dispatch-route-map.md` now maps each `DispatchAsync` branch to source ranges, side effects, helper candidates, and proof needs.
- The route map distinguishes pure helper candidates from side-effect owners.
- Durable claim acquisition, renewal, held-check, release, heartbeat disposal, and lost-claim behavior are classified as side-effect/session boundaries, not pure planners.
- Database blocking, missing upstream materialization, subprocess handling, workflow handling, direct agent execution, finalizer invocation, transition application, and failure transitions remain side-effecting dispatcher-owned flows.
- The extraction cutline explicitly forbids route planners from executing EF writes, storage writes, subprocess service calls, workflow calls, agent execution, or finalizer transitions.
