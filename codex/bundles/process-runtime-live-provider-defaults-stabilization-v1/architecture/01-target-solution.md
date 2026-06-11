# Target Solution

This validator-canonical file points to the target architecture in `bundle://architecture/01-target-architecture.md`.

## Solution Shape
- Keep Process Core deterministic and free of runtime, provider, UI, EF, scheduler, workflow, driver, and domain-specific leakage.
- Keep process execution in the process module runtime through assignment, dispatch, AgentFramework/MAF, managed provider profiles, finalizer, artifacts, and readback.
- Repair live smoke model selection without bypassing managed providers.
- Defer Process Runtime Core extraction; document future seams only after stabilization closes.
