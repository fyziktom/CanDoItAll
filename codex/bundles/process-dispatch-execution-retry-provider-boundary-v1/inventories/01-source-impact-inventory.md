# Source Impact Inventory Template

| File | Current responsibility | Target action | Must preserve |
| --- | --- | --- | --- |
| `Execution.cs` | ExecuteUntilSettledAsync attempt loop, execution launch, provider repair, recovery directive | Slim through attempt/facade/coordinator helpers | attempt order, recovery directive, provider repair, finalOutcome |
| `Concurrency.cs` | concurrent adoption, response resolution, retry decisions, no-progress signal | extract rules/coordinators | fingerprints, retry stop/continue behavior, response preference |
| `RecoveryPackets.cs` | recovery packet and reason helpers | consume retry facts only if needed | journal/rework packet semantics |
| `ImplementationProof helpers` | proof evidence checks | reuse, do not move to driver API | implementation proof behavior |
