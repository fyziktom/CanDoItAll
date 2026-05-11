# Input Coverage Matrix

| Source input | Normalized requirements | Impacted surface | Planned proof | Owning subbundle | Notes |
|---|---|---|---|---|---|
| "add executors in workflows (MAF)" | R01, R03, R11 | Models, Core contracts, MAF compiler/runtime | Unit/integration workflow execution tests | 01, 04 | Critical foundation. |
| "access to files via storage driver" | R06, R12 | Workspace file services, executor registry | Storage read/write/list/search scenarios | 03, 06 | Use existing guarded file service. |
| "Access to project structure... node or tree..." | R07 | MAF project-structure services | Project read/subtree scenario | 03, 06 | Requires seeded/existing project data. |
| "add asset node with artefact... mermaid, json, image, md" | R07, R12 | Project-structure asset service, artifacts | Asset create scenario | 03, 06 | Selector must be typed. |
| "generic tool/executor is getting some from http, https" | R08 | HTTP executor | HTTP JSON/text scenarios and invalid URI tests | 03, 06 | Enforce scheme and size limits. |
| "Getting of image from some AI model" | R09, R17 | Image providers | Provider-backed or explicit blocker scenario | 03, 06 | Existing provider availability unknown. |
| "reading/writing to excels... ClosedXML... wrapper" | R04, R05 | New Tools.Documents project, spreadsheet executor | Wrapper tests and xlsx scenarios | 02, 06 | ClosedXML must not leak. |
| "setup... get/write cell, multiple reads/writes" | R05, R15 | Settings schemas, inspector | Settings validation and spreadsheet scenario | 02, 05 | Use batched operations. |
| "identify others generic" | R10 | Catalog descriptors/followups | Descriptor test and follow-up list | 01, 07 | Keep implementation scoped. |
| "right click menu... second layer... toolbox" | R13, R14 | Workflow canvas UI | Browser screenshot and DOM/action proof | 05 | Use existing CanvasWorkbenchAction children and OverlayComponentToolbox. |
| "extensions with custom plugins later" | R01, R15 | Contracts, descriptor UI renderer key | Architecture review | 01, 07 | No plugin runtime yet. |
| "timeouts, retry tries and non happy paths" | R02 | Runtime policy, executor invoker | Unit tests for timeout/retry/failure | 01, 04, 06 | No silent fallback. |
| "xlsx detailed plan" | R16 | Bundle artifacts | Workbook exists and matches plan | 06, 07 | Stored under bundle artifacts. |
| "test workflows... 20 examples... gpt-5-mini... gptoss20b64k" | R16, R17 | Runtime/provider setup | Execution report scenario table | 06 | Provider blockers must be explicit. |
