# Target Solution

## Architecture Intent

- Upgrade the Microsoft Agent Framework adapter from the 1.3 package line to the current compatible 1.6.x line before changing process runtime behavior.
- Keep the MAF adapter boundary explicit: package/API migration belongs in `src/CanDoItAll.AgentFramework.Maf`, hosting A2A changes belong in `src/CanDoItAll.AgentFramework.Hosting`, and process recovery remains in `src/CanDoItAll.Modules.Processes`.
- Use a shared process artifact validation path for satisfaction read models and finalizer completion so required artifact status cannot diverge.
- Preserve process genericity: fixes must apply to process artifacts, runs, step executions, and workflows without hard-coding the Blazor/Tetris template.

## Behavioral Target

- Current-run workspace-written artifacts with matching process run, step, expectation, and source execution run validate as satisfied when their content can be resolved.
- Missing or unreadable content is reported as content/hash failure, not `StaleOrWrongRun`.
- Recovery failures become actionable blocked/recoverable diagnostics visible through API/UI surfaces.
- Agent tool approval, finalizer capture, tracing, metrics, handoff, A2A, workflow, and capability discovery behavior remains stable after the MAF package upgrade.
