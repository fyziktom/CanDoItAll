# Current State

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ChatWorkspacePanel.razor` owns the reusable chat transcript/composer body used by the Agents chat tab.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentChatPanel.razor.cs` owns the current chat page orchestration: agent selection, thread creation, send, approvals, attachments, runtime details, and execution-log updates.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Agents\Access\AgentProjectStructureAccessModels.cs` stores typed project-structure access metadata with `CanRead`, `CanWrite`, `AllowAllProjects`, and `AllowedProjectIds`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Agents\Access\AgentProcessAccessModels.cs` stores typed process access metadata with `CanRead`, `CanWrite`, `AllowAllDefinitions`, and `AllowedDefinitionIds`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Core\CanvasFloatingWindow.razor` wraps OverlayLib movable windows for canvas-hosted overlays.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor` hosts the project structure canvas and existing toolbox, health, signals, and selection floating windows.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.ToolWindows.cs` persists project structure floating-window states in the canvas UI state dictionary.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceStepsTab.razor` hosts the process definition canvas and existing toolbox, selection, and editor floating windows.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.cs` owns process canvas floating-window state and canvas actions.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Forms\TagEditor.razor` already provides the tag-filter editing surface required by the prompt.
