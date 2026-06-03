using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench.CanvasAdapters;

public sealed class ProjectStructureActionCatalogAdapter
{
    public IReadOnlyList<CanvasWorkbenchAction> BuildNodeContextActions(ProjectStructureNode node)
        => BuildNodeContextActions(node, canLaunchRuntime: false);

    public IReadOnlyList<CanvasWorkbenchAction> BuildNodeContextActions(ProjectStructureNode node, bool canLaunchRuntime)
        => BuildNodeContextActions(node, canLaunchRuntime, canOpenInFileExplorer: false, canOpenInNewTab: false);

    public IReadOnlyList<CanvasWorkbenchAction> BuildNodeContextActions(
        ProjectStructureNode node,
        bool canLaunchRuntime,
        bool canOpenInFileExplorer,
        bool canOpenInNewTab)
    {
        if (node.ProjectRole != ProjectStructureProjectRole.None)
        {
            return BuildProjectContextActions(node);
        }

        var actions = new List<CanvasWorkbenchAction>
        {
            new() { ActionId = "open", Label = "Open", MenuLabel = "Open", Description = "Open the linked artifact or routed workspace.", Icon = "open", Tone = "accent" },
            new() { ActionId = "copy-id", Label = "Copy id", MenuLabel = "Copy id", Description = "Copy this node id to the clipboard.", Icon = "copy", Tone = "ghost" },
            new() { ActionId = "copy-info", Label = "Copy info", MenuLabel = "Copy info", Description = "Copy this node as type_title:id-hash.", Icon = "copy", Tone = "primary" },
            new() { ActionId = "copy-subtree-ids", Label = "Copy tree ids", MenuLabel = "Copy tree", Description = "Copy this node and descendants as type_title:id-hash entries.", Icon = "copy", Tone = "sky" },
            new() { ActionId = "summary", Label = "Summary", MenuLabel = "Summary", Description = "Open the hierarchical progress summary and export tools.", Icon = "summary", Tone = "sky" },
            new() { ActionId = "connect", Label = "Connect", MenuLabel = "Connect", Description = "Use the selected node as the source for a dependency link.", Icon = "link", Tone = "neutral" },
            new() { ActionId = "reconnect", Label = "Reconnect", MenuLabel = "Reconnect", Description = "Pick a new parent node for this branch explicitly.", Icon = "relink", Tone = "primary" },
            new() { ActionId = "disconnect", Label = "Disconnect", MenuLabel = "Disconnect", Description = "Move this node back to the project root without deleting it.", Icon = "unlink", Tone = "ghost" },
            new() { ActionId = "move-descendants-to-subproject", Label = "To subproject", MenuLabel = "To subproject", Description = "Move this node's descendants into a new subproject while leaving the selected node in place.", Icon = "fork", Tone = "primary" },
            BuildProgressAction(),
            BuildMarkerAction(),
            BuildPriorityAction(),
            new() { ActionId = "validate", Label = "Validate", MenuLabel = "Validate", Description = "Open project validation tooling from this node.", Icon = "fact_check", Tone = "mint" },
            new() { ActionId = "test", Label = "Test", MenuLabel = "Test", Description = "Open test planning and evidence flows.", Icon = "test", Tone = "warn" },
            new() { ActionId = "delete", Label = "Delete", MenuLabel = "Delete", Description = "Delete this node, with confirmation when the impact is not trivial.", Icon = "delete", Tone = "danger" }
        };

        if (canLaunchRuntime)
        {
            actions.InsertRange(
                1,
                [
                    new CanvasWorkbenchAction
                    {
                        ActionId = "runtime:open",
                        Label = "Run normally",
                        MenuLabel = "Run",
                        Description = "Launch the resolved workspace command in PowerShell.",
                        Icon = "powershell",
                        Tone = "accent"
                    },
                    new CanvasWorkbenchAction
                    {
                        ActionId = "runtime:admin",
                        Label = "Run as administrator",
                        MenuLabel = "Run admin",
                        Description = "Launch the resolved workspace command in an elevated PowerShell window.",
                        Icon = "admin_panel_settings",
                        Tone = "warn"
                    }
                ]);
        }

        if (canOpenInFileExplorer)
        {
            actions.Insert(1, new CanvasWorkbenchAction
            {
                ActionId = "open-local",
                Label = "Open in File Explorer",
                MenuLabel = "Explorer",
                Description = "Open the trusted managed file or folder in the system File Explorer.",
                Icon = "folder_open",
                Tone = "primary"
            });
        }

        if (canOpenInNewTab)
        {
            actions.Insert(1, new CanvasWorkbenchAction
            {
                ActionId = "open-new-tab",
                Label = "Open in New Tab",
                MenuLabel = "New tab",
                Description = "Open the routed or IPFS-backed file in a separate browser tab.",
                Icon = "open",
                Tone = "accent"
            });
        }

        if (node.ObjectType == ProjectObjectType.ProcessDefinition)
        {
            actions.InsertRange(
                1,
                [
                    new CanvasWorkbenchAction
                    {
                        ActionId = "estimate-process",
                        Label = "Estimate",
                        MenuLabel = "Estimate",
                        Description = "Prepare a launch plan and show the estimated process price and time without starting it.",
                        Icon = "query_stats",
                        Tone = "sky"
                    },
                    new CanvasWorkbenchAction
                    {
                        ActionId = "start-process",
                        Label = "Start",
                        MenuLabel = "Start",
                        Description = "Confirm and start this process with the selected project-structure node context.",
                        Icon = "play_arrow",
                        Tone = "mint"
                    }
                ]);
        }
        else if (CanLinkExistingProcess(node))
        {
            actions.Insert(5, new CanvasWorkbenchAction
            {
                ActionId = "add-process",
                Label = "Add process",
                MenuLabel = "Add process",
                Description = "Link an existing process definition so this node can be executed through that process.",
                Icon = "account_tree",
                Tone = "mint"
            });
        }

        if (node.ObjectType == ProjectObjectType.WorkflowDefinition)
        {
            actions.Insert(1, new CanvasWorkbenchAction
            {
                ActionId = "start-workflow",
                Label = "Start workflow",
                MenuLabel = "Start workflow",
                Description = "Confirm and start this workflow with the stored project-structure input.",
                Icon = "play_arrow",
                Tone = "mint"
            });
        }
        else if (CanLinkExistingWorkflow(node))
        {
            actions.Insert(6, new CanvasWorkbenchAction
            {
                ActionId = "add-workflow",
                Label = "Add workflow",
                MenuLabel = "Add workflow",
                Description = "Add a workflow node under this item and configure the input sent to the workflow.",
                Icon = "flow",
                Tone = "accent"
            });
        }

        if (node.ObjectType == ProjectObjectType.PromptFlow)
        {
            actions.Insert(1, new CanvasWorkbenchAction
            {
                ActionId = "wizard",
                Label = "Wizard",
                MenuLabel = "Wizard",
                Description = "Open the detailed prompt wizard for this flow.",
                Icon = "flow",
                Tone = "sky"
            });
        }

        if (node.ObjectType == ProjectObjectType.Recording)
        {
            actions.Insert(2, new CanvasWorkbenchAction
            {
                ActionId = "transcript:create",
                Label = "Create transcript",
                MenuLabel = "Transcript",
                Description = "Create a transcript beneath this recording and preserve the source relationship.",
                Icon = "transcript",
                Tone = "mint"
            });
        }

        if (node.ObjectType == ProjectObjectType.Transcript)
        {
            actions.Insert(2, new CanvasWorkbenchAction
            {
                ActionId = "transcript-llm",
                Label = "LLM actions",
                MenuLabel = "LLM",
                Description = "Run transcript actions with explicit confirmation and provider selection.",
                Icon = "ai",
                Tone = "accent",
                Children =
                [
                    new CanvasWorkbenchAction { ActionId = "transcript:summarize", Label = "Summarize", MenuLabel = "Summarize", Description = "Summarize this transcript with a selected provider.", Icon = "summary", Tone = "accent", MenuSize = "compact" },
                    new CanvasWorkbenchAction { ActionId = "transcript:find-my-tasks", Label = "Find my tasks", MenuLabel = "My tasks", Description = "Extract work assigned to you from this transcript.", Icon = "task", Tone = "warn", MenuSize = "compact" },
                    new CanvasWorkbenchAction { ActionId = "transcript:find-others-deliveries", Label = "Find others delivery to me", MenuLabel = "Others to me", Description = "Extract promised deliveries coming back to you.", Icon = "deliver", Tone = "sky", MenuSize = "compact" }
                ]
            });
        }

        if (string.IsNullOrWhiteSpace(node.ParentId))
        {
            actions.RemoveAll(action => action.ActionId is "disconnect" or "reconnect");
        }

        if (node.ObjectType == ProjectObjectType.PromptStep)
        {
            actions.AddRange(
            [
                new CanvasWorkbenchAction { ActionId = "branch", Label = "Branch", MenuLabel = "Branch", Description = "Create a prompt follow-up from this step.", Icon = "fork", Tone = "accent" },
                new CanvasWorkbenchAction { ActionId = "mark-used", Label = "Used", MenuLabel = "Used", Description = "Mark this prompt step as consumed.", Icon = "use", Tone = "mint" },
                new CanvasWorkbenchAction { ActionId = "skip", Label = "Skip", MenuLabel = "Skip", Description = "Skip the selected prompt step.", Icon = "skip", Tone = "warn" }
            ]);
        }

        if (node.ObjectType == ProjectObjectType.ProjectBlock)
        {
            actions.Add(new CanvasWorkbenchAction
            {
                ActionId = "block:change-type",
                Label = "Change block",
                MenuLabel = "Change block",
                Description = "Change this block to another common block type without recreating the node.",
                Icon = "swap_horiz",
                Tone = "accent"
            });
        }

        if (node.ObjectType == ProjectObjectType.Note)
        {
            actions.Add(new CanvasWorkbenchAction
            {
                ActionId = "note:convert-to-block",
                Label = "Convert",
                MenuLabel = "Convert",
                Description = "Convert this note into a typed node while keeping the note text.",
                Icon = "swap_horiz",
                Tone = "accent"
            });
        }

        actions.AddRange(ProjectStructureCanvasCatalog.BuildMenuCreateActions(node.ObjectType));
        return ProjectStructureActionShortcuts.Apply(ProjectStructureMenuComposition.OrderNodeContextActions(node, actions));
    }

    private static bool CanLinkExistingProcess(ProjectStructureNode node)
    {
        return node.ProjectRole == ProjectStructureProjectRole.None &&
               node.ObjectType is not (ProjectObjectType.ProcessDefinition or ProjectObjectType.ProcessRun);
    }

    private static bool CanLinkExistingWorkflow(ProjectStructureNode node)
    {
        return node.ProjectRole == ProjectStructureProjectRole.None &&
               node.ObjectType is not (
                   ProjectObjectType.ProcessDefinition or
                   ProjectObjectType.ProcessRun or
                   ProjectObjectType.WorkflowDefinition or
                   ProjectObjectType.WorkflowRun);
    }

    public IReadOnlyList<CanvasWorkbenchAction> BuildGroupContextActions()
    {
        List<CanvasWorkbenchAction> actions =
        [
            new CanvasWorkbenchAction
            {
                ActionId = "group-frame",
                Label = "Border",
                MenuLabel = "Border",
                Description = "Create a living border around the selected branches.",
                Icon = "frame",
                Tone = "sky"
            },
            new CanvasWorkbenchAction
            {
                ActionId = "group-clear-frame",
                Label = "Clear border",
                MenuLabel = "Clear",
                Description = "Remove borders that currently contain the selected nodes.",
                Icon = "clear",
                Tone = "ghost"
            },
            BuildProgressAction(),
            BuildMarkerAction(),
            BuildPriorityAction()
        ];

        return ProjectStructureActionShortcuts.Apply(actions);
    }

    public IReadOnlyList<CanvasWorkbenchAction> BuildQuickCreateActions(ProjectObjectType? sourceType)
        => ProjectStructureCanvasCatalog.BuildMenuCreateActions(sourceType);

    internal IReadOnlyList<ProjectStructureInspectorCreateGroup> BuildInspectorCreateGroups(ProjectObjectType? sourceType)
        => ProjectStructureCanvasCatalog.BuildInspectorCreateGroups(sourceType);

    public bool TryResolveProgressAction(string actionId, out string progressMode, out int progressPercent)
    {
        progressMode = string.Empty;
        progressPercent = 0;
        if (!actionId.StartsWith("progress:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var token = actionId["progress:".Length..];
        if (token.Equals("na", StringComparison.OrdinalIgnoreCase))
        {
            progressMode = "na";
            return true;
        }

        if (token.Equals("started", StringComparison.OrdinalIgnoreCase))
        {
            progressMode = "started";
            return true;
        }

        if (!int.TryParse(token, out var percent))
        {
            return false;
        }

        progressPercent = Math.Clamp(percent, 0, 100);
        progressMode = progressPercent >= 100 ? "complete" : "progress";
        return true;
    }

    public bool TryResolveMarkerAction(string actionId, out string markerIcon, out string markerTone, out string markerLabel)
    {
        (markerIcon, markerTone, markerLabel) = actionId switch
        {
            "marker:none" => (string.Empty, string.Empty, string.Empty),
            "marker:question" => ("question", "sky", "Question"),
            "marker:alert" => ("alert", "danger", "Exclamation"),
            "marker:thumbs-up" => ("thumbs-up", "mint", "Approved"),
            "marker:thumbs-down" => ("thumbs-down", "danger", "Rejected"),
            "marker:pause" => ("pause", "warn", "Paused"),
            "marker:stop" => ("stop", "primary", "Stopped"),
            "marker:money" => ("money", "mint", "Budget"),
            "marker:car" => ("car", "sky", "Transport"),
            "marker:idea" => ("idea", "accent", "Idea"),
            "marker:risk" => ("risk", "danger", "Risk"),
            _ => (string.Empty, string.Empty, string.Empty)
        };

        return actionId.StartsWith("marker:", StringComparison.OrdinalIgnoreCase) &&
            (actionId.Equals("marker:none", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(markerIcon));
    }

    public bool TryResolvePriorityAction(string actionId, out int priority)
    {
        priority = 0;
        if (!actionId.StartsWith("priority:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return int.TryParse(actionId["priority:".Length..], out priority);
    }

    private static IReadOnlyList<CanvasWorkbenchAction> BuildProjectContextActions(ProjectStructureNode node)
    {
        var actions = new List<CanvasWorkbenchAction>
        {
            new()
            {
                ActionId = "open",
                Label = "Open",
                MenuLabel = "Open",
                Description = "Open the selected project route in the current tab.",
                Icon = "open",
                Tone = "accent"
            },
            new()
            {
                ActionId = "copy-id",
                Label = "Copy id",
                MenuLabel = "Copy id",
                Description = "Copy this project node id to the clipboard.",
                Icon = "copy",
                Tone = "ghost"
            },
            new()
            {
                ActionId = "copy-info",
                Label = "Copy info",
                MenuLabel = "Copy info",
                Description = "Copy this project node as type_title:id-hash.",
                Icon = "copy",
                Tone = "primary"
            },
            new()
            {
                ActionId = "copy-subtree-ids",
                Label = "Copy tree ids",
                MenuLabel = "Copy tree",
                Description = "Copy this project node and descendants as type_title:id-hash entries.",
                Icon = "copy",
                Tone = "sky"
            },
            new()
            {
                ActionId = "summary",
                Label = "Summary",
                MenuLabel = "Summary",
                Description = "Review the project summary and exported hierarchy details.",
                Icon = "summary",
                Tone = "sky"
            },
            new()
            {
                ActionId = "connect",
                Label = "Connect",
                MenuLabel = "Connect",
                Description = "Use the selected project as the source for a dependency link.",
                Icon = "link",
                Tone = "neutral"
            },
            BuildProgressAction(),
            BuildMarkerAction(),
            BuildPriorityAction()
        };

        if (node.ProjectRole is ProjectStructureProjectRole.Subproject or ProjectStructureProjectRole.ParentProject or ProjectStructureProjectRole.AdditionalParentProject)
        {
            actions.Insert(1, new CanvasWorkbenchAction
            {
                ActionId = "project:open-structure",
                Label = "Structure tab",
                MenuLabel = "Structure",
                Description = "Open the related project structure in a new browser tab.",
                Icon = "open",
                Tone = "primary"
            });
        }

        if (node.ProjectRole is ProjectStructureProjectRole.ActiveProject or ProjectStructureProjectRole.Subproject)
        {
            actions.Add(new CanvasWorkbenchAction
            {
                ActionId = "project:add-subproject",
                Label = "Add subproject",
                MenuLabel = "Add subproject",
                Description = "Attach another project beneath this project and refresh the hierarchy canvas.",
                Icon = "fork",
                Tone = "accent"
            });
        }

        if (node.ProjectRole == ProjectStructureProjectRole.Subproject)
        {
            actions.Add(new CanvasWorkbenchAction
            {
                ActionId = "project:reconnect-subproject",
                Label = "Reconnect parent",
                MenuLabel = "Reconnect",
                Description = "Move this project under another parent project without leaving the canvas.",
                Icon = "relink",
                Tone = "primary"
            });
        }

        if (node.ProjectRole != ProjectStructureProjectRole.AdditionalParentProject)
        {
            actions.AddRange(ProjectStructureCanvasCatalog.BuildMenuCreateActions(node.ObjectType));
        }

        return ProjectStructureActionShortcuts.Apply(ProjectStructureMenuComposition.OrderNodeContextActions(node, actions));
    }

    private static CanvasWorkbenchAction BuildProgressAction()
        => new()
        {
            ActionId = "progress",
            Label = "Progress",
            MenuLabel = "Progress",
            Description = "Update progress for the selected item or active selection.",
            Icon = "progress",
            Tone = "mint",
            SubmenuLayout = "compact-hive",
            Children = BuildProgressPresetActions()
        };

    private static List<CanvasWorkbenchAction> BuildProgressPresetActions()
    {
        var actions = new List<CanvasWorkbenchAction>
        {
            new()
            {
                ActionId = "progress:0",
                Label = "0%",
                MenuLabel = "0%",
                Description = "Reset the progress ring to 0 percent.",
                Icon = "progress-0",
                Tone = "ghost",
                MenuSize = "compact"
            },
            new()
            {
                ActionId = "progress:started",
                Label = "Started",
                MenuLabel = "Start",
                Description = "Show that work started, but the percentage is still too early to judge.",
                Icon = "progress-started",
                Tone = "warn",
                MenuSize = "compact"
            }
        };

        foreach (var percent in Enumerable.Range(1, 10).Select(value => value * 10))
        {
            actions.Add(new CanvasWorkbenchAction
            {
                ActionId = $"progress:{percent}",
                Label = $"{percent}%",
                MenuLabel = $"{percent}%",
                Description = $"Set the clockwise progress ring to {percent} percent.",
                Icon = $"progress-{percent}",
                Tone = percent >= 90 ? "mint" : percent >= 60 ? "sky" : percent >= 30 ? "accent" : "warn",
                MenuSize = "compact"
            });
        }

        actions.Add(new CanvasWorkbenchAction
        {
            ActionId = "progress:na",
            Label = "N/A",
            MenuLabel = "N/A",
            Description = "Show the neutral not-applicable state.",
            Icon = "progress-na",
            Tone = "ghost",
            MenuSize = "compact"
        });

        return actions;
    }

    private static CanvasWorkbenchAction BuildMarkerAction()
        => new()
        {
            ActionId = "marker",
            Label = "Markers",
            MenuLabel = "Markers",
            Description = "Attach a fast visual note marker to the selected item or active selection.",
            Icon = "marker",
            Tone = "primary",
            SubmenuLayout = "compact-hive",
            Children =
            [
                new() { ActionId = "marker:none", Label = "Clear", MenuLabel = "Clear", Description = "Remove the current marker.", Icon = "marker-none", Tone = "ghost", MenuSize = "compact" },
                new() { ActionId = "marker:question", Label = "Question", MenuLabel = "Question", Description = "Mark this node as an open question.", Icon = "marker-question", Tone = "sky", MenuSize = "compact" },
                new() { ActionId = "marker:alert", Label = "Exclamation", MenuLabel = "Exclamation", Description = "Mark this node as needing attention.", Icon = "marker-alert", Tone = "danger", MenuSize = "compact" },
                new() { ActionId = "marker:thumbs-up", Label = "Approved", MenuLabel = "Good", Description = "Mark this node as looking good.", Icon = "marker-thumbs-up", Tone = "mint", MenuSize = "compact" },
                new() { ActionId = "marker:thumbs-down", Label = "Rejected", MenuLabel = "Bad", Description = "Mark this node as needing a rework or rejection.", Icon = "marker-thumbs-down", Tone = "danger", MenuSize = "compact" },
                new() { ActionId = "marker:pause", Label = "Paused", MenuLabel = "Pause", Description = "Mark this node as paused.", Icon = "marker-pause", Tone = "warn", MenuSize = "compact" },
                new() { ActionId = "marker:stop", Label = "Stopped", MenuLabel = "Stop", Description = "Mark this node as stopped.", Icon = "marker-stop", Tone = "primary", MenuSize = "compact" },
                new() { ActionId = "marker:money", Label = "Budget", MenuLabel = "Money", Description = "Mark this node with a budget or finance note.", Icon = "marker-money", Tone = "mint", MenuSize = "compact" },
                new() { ActionId = "marker:car", Label = "Transport", MenuLabel = "Drive", Description = "Mark this node with a transport or logistics note.", Icon = "marker-car", Tone = "sky", MenuSize = "compact" },
                new() { ActionId = "marker:idea", Label = "Idea", MenuLabel = "Idea", Description = "Mark this node as an idea worth keeping visible.", Icon = "marker-idea", Tone = "accent", MenuSize = "compact" },
                new() { ActionId = "marker:risk", Label = "Risk", MenuLabel = "Risk", Description = "Mark this node as a risk or blocker.", Icon = "marker-risk", Tone = "danger", MenuSize = "compact" }
            ]
        };

    private static CanvasWorkbenchAction BuildPriorityAction()
        => new()
        {
            ActionId = "priority",
            Label = "Priority",
            MenuLabel = "Priority",
            Description = "Attach a numbered priority badge to the selected item or active selection.",
            Icon = "priority",
            Tone = "warn",
            SubmenuLayout = "compact-hive",
            Children =
            [
                new() { ActionId = "priority:0", Label = "0", MenuLabel = "0", Description = "Clear the priority badge.", Icon = "priority-0", Tone = "ghost", MenuSize = "compact" },
                new() { ActionId = "priority:1", Label = "1", MenuLabel = "1", Description = "Set the highest priority marker.", Icon = "priority-1", Tone = "danger", MenuSize = "compact" },
                new() { ActionId = "priority:2", Label = "2", MenuLabel = "2", Description = "Set priority 2.", Icon = "priority-2", Tone = "warn", MenuSize = "compact" },
                new() { ActionId = "priority:3", Label = "3", MenuLabel = "3", Description = "Set priority 3.", Icon = "priority-3", Tone = "accent", MenuSize = "compact" },
                new() { ActionId = "priority:4", Label = "4", MenuLabel = "4", Description = "Set priority 4.", Icon = "priority-4", Tone = "sky", MenuSize = "compact" },
                new() { ActionId = "priority:5", Label = "5", MenuLabel = "5", Description = "Set priority 5.", Icon = "priority-5", Tone = "mint", MenuSize = "compact" },
                new() { ActionId = "priority:6", Label = "6", MenuLabel = "6", Description = "Set priority 6.", Icon = "priority-6", Tone = "primary", MenuSize = "compact" }
            ]
        };
}


