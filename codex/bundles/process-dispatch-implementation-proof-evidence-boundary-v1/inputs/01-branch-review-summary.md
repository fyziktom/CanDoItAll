Branch review summary from assistant:
- Previous subprocess runtime/projection boundary appears complete in declared scope.
- Execution report says SB01-SB24 completed; no UI proof required; no Process Core or production driver API added.
- Subprocess lifecycle, observation, capability-gap, projection plan/gap/writer helpers exist and Dispatch.cs is reduced to ~1261 lines.
- Remaining safe seam before Process Core: implementation proof / runtime evidence boundary, because it still mixes generic process evidence with .NET/JS/domain-specific receipts, runnable-host detection, stack detection, concrete mutation/read sequencing, and carry-forward proof state.
