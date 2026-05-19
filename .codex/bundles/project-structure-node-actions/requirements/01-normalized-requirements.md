# Normalized Requirements

| ID | Requirement | Observable Acceptance | Owning Subbundle |
| --- | --- | --- | --- |
| R001 | Runtime-capable nodes must expose both normal and administrator launch actions from the double-click quick-action dialog and context menu. | A valid runtime script, Python runtime, .NET runtime, and docker runtime node each show `Run normally` and `Run as administrator` where launch is available. | `01-01-runtime-launch-foundation` |
| R002 | Runtime launches must open PowerShell with the specific command and working folder configured on the node. | Launcher resolution records the expected command and working directory for command-backed scripts, Python environments, .NET runtimes, and docker runtime commands. | `01-01-runtime-launch-foundation` |
| R003 | Folder nodes must support a user-selected or typed folder path and offer Explorer open actions. | Local-folder and deployment-folder nodes can store a path and expose `Open in File Explorer` when that path resolves to an allowed local directory. | `02-02-folder-file-link-actions` |
| R004 | File nodes backed by local drive paths must offer file-location Explorer actions. | Local file metadata or storage-backed file nodes expose `Open in File Explorer`; files open with Explorer `/select` semantics and folders open directly. | `02-02-folder-file-link-actions` |
| R005 | Repository and link nodes must recognize GitHub and GitLab URLs. | Catalog aliases, metadata/presentation helpers, and tests recognize `github.com`, `gitlab.com`, and common repository URL forms without misclassifying local folders. | `02-02-folder-file-link-actions` |
| R006 | Agent project-structure tools must explain how to add links, runtime scripts, folders, and all file types. | `project_structure_node_catalog` guidance includes runtime script, folder, file, link, GitHub/GitLab, and example metadata instructions. | `03-03-agent-catalog-and-ui-proof` |
| R007 | Changes must be proven with tests, Playwright MCP, screenshots, and bundle evidence. | Execution report contains commands, browser analytics, screenshot paths, and note closure rows for all raw notes. | `03-03-agent-catalog-and-ui-proof` |

## Raw Note Closure Matrix

| Raw note | Exact wording summary | Requirement IDs | Planned Proof | Owner | Exception |
| --- | --- | --- | --- | --- | --- |
| N001 | Runtime nodes must start the process and offer normal/admin PowerShell launch. | R001, R002 | Launcher tests, page/action tests, Playwright screenshot of runtime dialog/actions. | `01-01-runtime-launch-foundation` | Admin UAC click-through may be documented as host proof gap if not automatable. |
| N002 | Folders/files open Explorer in home instead of the configured folder/location. | R003, R004 | Local opener tests, page/action tests, Playwright screenshot of folder/file actions. | `02-02-folder-file-link-actions` | Unsafe paths remain blocked by guard policy. |
| N003 | Need Folder node that allows selecting a folder path and opens folder in Explorer. | R003 | Catalog/edit tests and Playwright create-dialog screenshot. | `02-02-folder-file-link-actions` | Uses existing local-folder/deployment-folder node types unless implementation proves a distinct object type is required. |
| N004 | Repository and link nodes must recognize GitHub or GitLab links. | R005 | Unit/component tests for aliases/presentation and Playwright visible labels where practical. | `02-02-folder-file-link-actions` | Recognition only, not remote API integration. |
| N005 | Agent tools need information about how to add links, runtime scripts, folders, and files. | R006 | `ProjectStructureNodeCatalogTests` and catalog output inspection. | `03-03-agent-catalog-and-ui-proof` | None. |
| N006 | Validate with Playwright MCP and screenshots. | R007 | Playwright MCP screenshot paths and analytics rows in execution report. | `03-03-agent-catalog-and-ui-proof` | Host windows may need separate note. |
