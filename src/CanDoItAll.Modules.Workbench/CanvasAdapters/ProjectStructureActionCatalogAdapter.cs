using CanDoItAll.Components.CanvasLib;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench.CanvasAdapters;

public sealed class ProjectStructureActionCatalogAdapter
{
    public IReadOnlyList<CanvasWorkbenchAction> BuildNodeContextActions(ProjectStructureNode node)
    {
        var actions = new List<CanvasWorkbenchAction>
        {
            new() { ActionId = "open", Label = "Open", MenuLabel = "Open", Description = "Open the linked artifact or routed workspace.", Icon = "open", Tone = "accent" },
            new() { ActionId = "summary", Label = "Summary", MenuLabel = "Summary", Description = "Open the hierarchical progress summary and export tools.", Icon = "summary", Tone = "sky" },
            new() { ActionId = "connect", Label = "Connect", MenuLabel = "Connect", Description = "Use the selected node as the source for a dependency link.", Icon = "link", Tone = "neutral" },
            new() { ActionId = "reconnect", Label = "Reconnect", MenuLabel = "Reconnect", Description = "Pick a new parent node for this branch explicitly.", Icon = "relink", Tone = "primary" },
            new() { ActionId = "disconnect", Label = "Disconnect", MenuLabel = "Disconnect", Description = "Detach this node from its current parent without deleting it.", Icon = "unlink", Tone = "ghost" },
            BuildProgressAction(),
            BuildMarkerAction(),
            BuildPriorityAction(),
            new() { ActionId = "validate", Label = "Validate", MenuLabel = "Validate", Description = "Open project validation tooling from this node.", Icon = "qa", Tone = "mint" },
            new() { ActionId = "test", Label = "Test", MenuLabel = "Test", Description = "Open test planning and evidence flows.", Icon = "test", Tone = "warn" },
            new() { ActionId = "delete", Label = "Delete", MenuLabel = "Delete", Description = "Delete this node, with confirmation when the impact is not trivial.", Icon = "delete", Tone = "danger" }
        };

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

        actions.AddRange(ProjectStructureCanvasCatalog.BuildMenuCreateActions(node.ObjectType));
        return actions;
    }

    public IReadOnlyList<CanvasWorkbenchAction> BuildGroupContextActions()
        =>
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
            "marker:alert" => ("alert", "danger", "Alert"),
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

    private static CanvasWorkbenchAction BuildProgressAction()
        => new()
        {
            ActionId = "progress",
            Label = "Progress",
            MenuLabel = "Progress",
            Description = "Update progress for the selected item or active selection.",
            Icon = "progress",
            Tone = "mint",
            SubmenuLayout = "compact-ring",
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
            Label = "Marker",
            MenuLabel = "Marker",
            Description = "Attach a fast visual note marker to the selected item or active selection.",
            Icon = "marker",
            Tone = "primary",
            SubmenuLayout = "compact-ring",
            Children =
            [
                new() { ActionId = "marker:none", Label = "Clear", MenuLabel = "Clear", Description = "Remove the current marker.", Icon = "marker-none", Tone = "ghost", MenuSize = "compact" },
                new() { ActionId = "marker:question", Label = "Question", MenuLabel = "Ask", Description = "Mark this node as an open question.", Icon = "marker-question", Tone = "sky", MenuSize = "compact" },
                new() { ActionId = "marker:alert", Label = "Alert", MenuLabel = "Warn", Description = "Mark this node as needing attention.", Icon = "marker-alert", Tone = "danger", MenuSize = "compact" },
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
            SubmenuLayout = "compact-ring",
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


