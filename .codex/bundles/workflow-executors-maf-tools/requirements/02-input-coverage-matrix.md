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
| "toolbox, selection, must be as floating windows in the canvas" | R18, R19 | Workflow canvas UI | Playwright open-state screenshots | 08 | Reopened; previous supporting-panel toolbox is insufficient. |
| "when adding something new it must show as modal" | R20 | Workflow canvas create flow | Toolbox and right-click create modal proof | 08 | Reopened. |
| "double click some node it must open modal with details and possible edit" | R21 | Workflow canvas node open/edit flow | Double-click modal screenshot and edit proof | 08 | Reopened. |
| "split workflows page into tabs" | R22 | Workflows page | Browser proof of dashboard/definitions/editor/templates/history/analytics tabs | 08 | Reopened. |
| "Create new postgresql db for this test" | R24 | Infrastructure/control plane/test instance | DB creation and startup proof | 10 | Must not mutate existing DB. |
| "testing instance those 20 real world examples" | R25 | Workflow catalog/runtime/test harness | API seed count and scenario execution report | 10 | Must distinguish PostgreSQL data from in-memory workflow seed if persistence stays in-memory. |
| "add also projects with some project structures" | R25 | Projects/project-structure services | Project seed proof and project-structure executor scenarios | 10 | Needed for complex tree/file/asset scenarios. |
| "If... something is working incorrectly repair/improve it" | R26 | Defect-specific files | Repair and retest rows | 10, 11 | No hidden residual-risk closure. |
| "up to date APIs for controlling workflows similar as processes" | R23 | Workflow API | HTTP/API observer smoke | 09 | Compare against process control endpoints. |
| "add multiple steps... executors works together with llm calls... transfer of in/outs" | R27 | MAF compiler, LLM component invoker, executor settings, PostgreSQL scenario harness | Live multi-step workflow proof with executor -> LLM -> executor chains | 10, 11 | Requires real LLM node execution and downstream content-from-input writes. |
