# SB15 Source Assertions

- Gate D runtime smoke used the full solution build: `dotnet build CanDoItAll.slnx --no-restore`.
- Focused integration proof covered execution selection, wrapper parity, guard lease, heartbeat renewal/claim-loss, start-transition request parity, route planner decisions, and fresh recovery skip behavior.
- Focused architecture proof covered Gate A, Gate B, Gate C, finalizer context factory, no premature Process Core/driver pack creation, and bundle proof path policy.
- Runtime proof policy scan found no Process Core or driver API in production source, no MAF back-dependencies, no UI file diff, no prohibited small/medium/mobile proof paths, and line counts under gate thresholds.
- Adversarial policy trap rejected a simulated prohibited mobile proof path without creating that artifact.
- Browser validation remains N/A because no UI files changed.
