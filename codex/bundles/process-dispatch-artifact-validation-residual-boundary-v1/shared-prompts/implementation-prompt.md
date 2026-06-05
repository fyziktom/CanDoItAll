# Shared Implementation Prompt

You are executing a behavior-preserving module-local refactor in `maf-processes-refactor`.

Rules:
- Preserve all current behavior.
- Keep helpers under `CanDoItAll.Modules.Processes`.
- No Process Core.
- No production driver APIs.
- No UI changes.
- No small/medium/mobile proof artifacts.
- Run the required focused tests and scans at each gate.
- If a helper extraction changes branch order, stop and repair before continuing.
