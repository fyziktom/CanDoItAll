# Implementation Prompt

```text
Implement only the assigned subbundle of `C:\repositories\CanDoItAll\cdi_process_workspace_density_and_recomposition_bundle`.

Mandatory rules:
- Keep the change strongly typed and minimal.
- Prefer extending existing BaseLib, CanvasLib, and process-module seams over adding parallel abstractions.
- Do not bypass product persistence with direct database writes.
- If the subbundle touches UI, capture browser proof exactly as the subbundle requires before claiming closure.
- If the subbundle touches shared layout or recomposition math, add targeted automated tests instead of relying on screenshots alone.

Bundle references:
- Requirements: `C:\repositories\CanDoItAll\cdi_process_workspace_density_and_recomposition_bundle\requirements\01-normalized-requirements.md`
- Architecture: `C:\repositories\CanDoItAll\cdi_process_workspace_density_and_recomposition_bundle\architecture\01-target-solution.md`
- Phase plan: `C:\repositories\CanDoItAll\cdi_process_workspace_density_and_recomposition_bundle\plan\01-phase-plan.md`
- Traceability: `C:\repositories\CanDoItAll\cdi_process_workspace_density_and_recomposition_bundle\traceability\01-requirement-traceability.md`

Before closing:
- State exactly what changed.
- State the commands run and whether they passed.
- State the browser and database proof captured.
- State any residual risk that blocks downstream work.
```
