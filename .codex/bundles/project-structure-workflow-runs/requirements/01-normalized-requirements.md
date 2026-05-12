# Normalized Requirements

## Functional Requirements

| Id | Requirement | Observable success criteria |
| --- | --- | --- |
| `R001` | Project-structure workflow runs are supported end to end. | A saved workflow can be started from a workflow node on the project-structure canvas and produces a persisted workflow run id. |
| `R002` | Workflow nodes are explicit project-structure nodes, not loose notes. | Add/create flows store workflow id/version/input settings using typed contracts and render a recognizable workflow node. |
| `R003` | Add workflow opens a workflow-selection dialog. | The dialog lists available active workflows, shows selected workflow metadata, and blocks creation until a workflow is selected. |
| `R004` | Add workflow dialog supports advanced input setup. | The user can see and configure what project, parent node, subtree, file/folder, and optional manual values are included in input. |
| `R005` | Workflow input always includes project and parent-node details. | Backend input composition tests prove the JSON includes project id/title/status and full selected parent node details, even if no optional inputs are selected. |
| `R006` | Start workflow uses confirmation only, without process matching resources. | Right-click/inspector start opens a confirmation dialog, not staffing or matching-resource stages. |
| `R007` | Workflow node status/progress/markers reflect the run state. | Starting sets progress mode to `started`; completion sets progress to 100; failed/cancelled/waiting states set explicit status and marker. |
| `R008` | Selection floating window shows workflow run detail. | Selecting a workflow node shows run state, current step/total steps, run id, last message, and link/action to run details where available. |
| `R009` | Workflow-created project nodes are created under the workflow node. | Project-structure executor/projection uses the workflow node id as default parent for new result nodes. |
| `R010` | Every workflow run provides a project-structure execution summary. | Summary includes status, basic result text, run id, step count, created node ids, created asset ids, and file paths for file operations not represented as asset nodes. |
| `R011` | Backend API and service capabilities exist before UI implementation. | Backend subbundles pass tests for create/start/input/status/summary/projection before UI subbundle entry gate. |
| `R012` | At least 20 realistic workflow cases are validated. | Execution report has at least 20 scenario rows with input, workflow, provider/backend, result, and validation decision. |
| `R013` | Scenario data covers supplied files and synthetic real-world inputs. | Mouser XLS/PDF, IoTFactory workbook, SEAMARK folder, email, business-plan, folder-summary, and file-save cases are represented. |
| `R014` | PostgreSQL, `gpt-5-mini`, and local Ollama `gptoss20b64k` are covered. | Validation evidence identifies the PostgreSQL DB used and includes provider runs for both required models or product-blocking failures. |
| `R015` | Scenario failures create repair work instead of being buried. | Any product defect found in scenario validation is represented by a repaired bundle/subbundle and rerun proof. |

## Non-Functional Requirements

- Maintain UI/Application/Domain/Infrastructure separation; do not put workflow runtime logic into Razor components.
- Use strongly typed workflow node metadata/input settings/status models.
- Keep changes scoped to project-structure workflow integration and required workflow examples.
- Log workflow start/projection failures with project id, node id, workflow id, run id when available, and no sensitive payload content.
- Use existing component-library layout primitives and existing project CSS patterns.
- Keep Blazor state changes explicit and testable.
