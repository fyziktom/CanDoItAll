# Target Solution

## Module-Local Boundary

- Keep all production source movement under `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/`.
- Extract artifact satisfaction and evidence validation logic into module-local helpers only.
- Preserve existing dispatcher wrapper entry points where callers or tests depend on them.

## Explicit Non-Goals

- Do not create `CanDoItAll.Processes.Core`.
- Do not add production process-driver APIs, driver packages, registries, or public driver contracts.
- Do not move EF entities, storage concerns, service-scope orchestration, transition side effects, or UI/Razor code.

## Helper Boundary Intent

- Snapshot and facts helpers classify already-loaded process artifact evidence without side effects.
- Rule helpers preserve existing branch ordering and diagnostics.
- Aggregator helpers only aggregate already-loaded validation evidence.
- Dispatcher partials keep side-effect orchestration and call the extracted helpers.

