# Manager-assisted repair after child no-go

The preceding feature child completed with a typed no-go packet. Treat that packet as evidence of a failed implementation strategy, not as accepted implementation proof and not as permission to repeat the same attempt.

1. Preserve the authoritative slice criteria, architecture decision, setup evidence, and child no-go packet.
2. Launch `dotnet-quality-repair` once with that evidence and submit its deferred parent outcome exactly.
3. Do not change product files in this coordinator step.
4. Accept only `quality-repair-handoff`, `quality-repair-handoff-after-bughunt`, or `quality-repair-handoff-after-final-repair` from the repair child.
5. A `quality-repair-no-go` remains a no-go and must be surfaced for manager attention; do not convert it into an accepted slice handoff.

The repair child has exactly three bounded mutation opportunities. Each used opportunity requires a diagnosis-guided action and independent validation of the original criteria before an accepted handoff; only `quality-repair-no-go` is terminal no-go evidence.
