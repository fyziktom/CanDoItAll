# Normalized Requirements

## Runtime Node Requirements

| Requirement | Statement | Source notes | Acceptance |
| --- | --- | --- | --- |
| `REQ-RUN-001` | Runtime-capable nodes must still open the quick-action modal on double-click instead of directly launching the command. | `N001` | Double-clicking a runtime-capable node produces a modal with an edit action plus explicit run choices. |
| `REQ-RUN-002` | The quick-action modal must offer both "run normally" and "run as administrator" for every runtime-capable node whose launch plan resolves on a supported host. | `N001`, `N002` | The modal contains two enabled runtime actions mapped to `LaunchRuntimeAsync(node, false)` and `LaunchRuntimeAsync(node, true)`. |
| `REQ-RUN-003` | The canvas right-click menu for runtime-capable nodes must include the same two run options. | `N002` | `BuildNodeContextActions` includes normal and administrator runtime actions when `RuntimeLauncher.Resolve(node)` succeeds. |
| `REQ-RUN-004` | Runtime run actions must preserve existing safety and feedback behavior. | `N002` | Execution still flows through `IProjectStructureRuntimeLauncher`, UAC cancellation remains handled, and `workflowFeedback` reports success/failure. |

## File And IPFS Node Requirements

| Requirement | Statement | Source notes | Acceptance |
| --- | --- | --- | --- |
| `REQ-FILE-001` | File-backed nodes that resolve to trusted local or managed file-system paths must offer "Open in File Explorer". | `N003` | Local drive nodes expose a File Explorer action in the quick-action modal, right-click menu, and inspector/support-panel paths where node actions are shown. |
| `REQ-FILE-002` | File Explorer actions must use the existing guarded local opener and blocked-extension rules. | `N003` | No caller opens paths directly; execution uses `IProjectStructureLocalFileOpener.OpenAsync`. |
| `REQ-FILE-003` | IPFS-backed file nodes must offer "Open in New Tab". | `N004` | IPFS nodes expose a new-tab open action when they have a route or access URL that can be opened by the browser. |
| `REQ-FILE-004` | IPFS-backed file nodes must not offer "Open in File Explorer" unless the same node also has a trusted local file-system path. | `N004` | IPFS-only nodes do not pass `CanShowLocalOpen`; their primary file action opens the browser route in a new tab. |

## MCP And Internal Agent Tool Requirements

| Requirement | Statement | Source notes | Acceptance |
| --- | --- | --- | --- |
| `REQ-TOOLS-001` | Project Structure MCP node summaries must include action-capability information for runtime, local-file, and IPFS/new-tab behavior. | `N005` | `project_structure_read` returns structured action/capability fields without requiring agents to infer behavior from raw metadata. |
| `REQ-TOOLS-002` | Internal agent project-structure tools must expose the same capability information as the MCP. | `N005` | `MafAgentRuntime.ProjectStructureTools` compact node mapping includes the new capability payload and tool descriptions mention how agents should interpret it. |
| `REQ-TOOLS-003` | Tool descriptions must document how runtime and file/IPFS nodes work without suggesting that agents can execute local host actions through MCP. | `N005` | MCP and internal tool descriptions say these are UI/host action capabilities, not remote execution APIs. |

## Explicit Non-Goals

- Add a remote MCP command that launches local PowerShell, UAC, File Explorer, or browser tabs from an agent call.
- Replace the existing `ProjectStructureRuntimeLauncher`.
- Replace the existing managed-file endpoint routes.
- Redesign the project-structure canvas layout.
