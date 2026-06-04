# Structured Input

```json
{
  "branch": "maf-processes-refactor",
  "reviewed_head": "35ccf5cc26dd1bf1dbf6444c796bfa6ad8d05121",
  "previous_bundle": "process-dispatch-artifact-write-coordinator-expansion-v1",
  "next_bundle": "process-dispatch-artifact-validation-rule-boundary-v1",
  "core_policy": "Do not create or extract CanDoItAll.Processes.Core in this bundle.",
  "driver_policy": "Prepare driver-readiness descriptors and inventories only; do not create driver packs yet.",
  "viewport_policy": "No small/medium/mobile proof. Runtime refactor proof should be N/A; if UI proof unexpectedly becomes necessary, use large desktop/PC only."
}
```

## User Priorities

- Continue gradual isolation of huge dispatcher services.
- Keep all existing behavior.
- Add abstractions/seams first, then use them for concrete paths.
- Delay Process Core until enough seams are proven.
- Remember future process helper drivers: do not implement drivers yet, but prepare evidence/validation semantics so drivers can later plug in cleanly.
