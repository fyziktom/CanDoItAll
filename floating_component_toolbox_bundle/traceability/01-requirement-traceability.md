# Requirement Traceability

| Requirement | Raw note | Owning subbundle | Planned proof | Source areas |
|---|---|---|---|---|
| RQ-001 | R1 | 01 | OverlayLib build and component callback test | OverlayLib |
| RQ-002 | R1, R2 | 02, 04 | Project structure toolbox click adds visible block in canvas screenshot | Workbench module, CanvasLib |
| RQ-003 | R1 | 02, 04 | Process canvas toolbox opens and role/step action still reaches editor flow | Processes module |
| RQ-004 | R1 | 02, 04 | Prompt factory toolbox search/add/preview smoke remains functional | Factory module |
| RQ-005 | R1, R2 | 03 | WebGL toolbox window opens over the stage | WebGlSandbox, OverlayLib |
| RQ-006 | R2 | 03, 04 | New role from WebGL toolbox appears as a role/person node in 3D screenshot | WebGlSandbox, Process module |
| RQ-007 | R1 | 01, 03 | Generic models are catalog-agnostic and do not encode process-only concepts | OverlayLib |
| RQ-008 | R2 | 04 | Playwright MCP analytics and screenshots recorded in execution report | Browser evidence |

## Input Coverage

| Raw note | Coverage | Exception status |
|---|---|---|
| R1 | Covered by generic shared component and all current host migrations. | No exception planned. |
| R2 | Covered by WebGL role add and project structure block add browser proof. | No exception planned. |
