using CanDoItAll.ComponentKit.Canvas;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectStructureCreateLeafDefinition(
    string ActionId,
    ProjectObjectType ObjectType,
    string ObjectSubtype,
    string GroupKey,
    string Label,
    string Description,
    string Icon,
    string Tone,
    string DefaultTitle,
    string TitleLabel,
    string TitlePlaceholder,
    string SubtitleLabel,
    string SubtitlePlaceholder,
    string NotesLabel,
    string NotesPlaceholder,
    bool RequiresFile = false,
    string AcceptedFileTypes = "",
    string FilePrompt = "Drop a file here or choose one.");

internal sealed record ProjectStructureInspectorCreateGroup(
    string Key,
    string Label,
    string Description,
    bool IsOpen,
    IReadOnlyList<CanvasWorkbenchAction> Actions);

internal static class ProjectStructureCanvasCatalog
{
    private sealed record CreateGroupDefinition(
        string Key,
        string Label,
        string Description,
        string Tone,
        string Icon);

    private static readonly IReadOnlyDictionary<string, CreateGroupDefinition> CreateGroups =
        new Dictionary<string, CreateGroupDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["capture"] = new("capture", "Capture", "Fast notes and decisions that keep the mindmap moving.", "neutral", "note"),
            ["planning"] = new("planning", "Planning", "Phases and milestones that shape delivery.", "primary", "plan"),
            ["blocks"] = new("blocks", "Blocks", "Typed project blocks for common planning and management areas.", "accent", "block"),
            ["prompts"] = new("prompts", "Prompts", "Prompt flows, sessions, and executable steps.", "accent", "prompt"),
            ["assets"] = new("assets", "Assets", "Repositories, files, uploads, links, and system touchpoints.", "sky", "asset"),
            ["assurance"] = new("assurance", "Assurance", "Validation, test planning, and supporting evidence.", "warn", "assure")
        };

    private static readonly IReadOnlyList<ProjectStructureCreateLeafDefinition> CreateLeafDefinitions =
    [
        new("add-note", ProjectObjectType.Note, string.Empty, "capture", "Note", "Capture a lightweight note and keep the mindmap moving.", "note", "neutral", "New note", "Headline", "Name the note", "Context", "Optional context tag", "Text", "Write the note"),
        new("add-decision", ProjectObjectType.Decision, string.Empty, "capture", "Decision", "Record a decision and its first rationale while you are still in the canvas.", "choice", "accent", "New decision", "Decision", "Name the decision", "Status", "Approved, draft, blocked...", "Rationale", "Why this decision exists"),
        new("add-phase", ProjectObjectType.Phase, string.Empty, "planning", "Phase", "Spin out a new phase branch from any point in the graph.", "phase", "primary", "New phase", "Phase", "Discovery", "Goal", "What this phase should achieve", "Focus", "Scope for this phase"),
        new("add-milestone", ProjectObjectType.Milestone, string.Empty, "planning", "Milestone", "Add a milestone node and capture the delivery target immediately.", "date", "warn", "New milestone", "Milestone", "Name the milestone", "Target", "Date or target window", "Outcome", "Describe the milestone outcome"),
        new("add-block-feature", ProjectObjectType.ProjectBlock, "feature", "blocks", "Feature block", "Group feature work, acceptance scope, and supporting details.", "feature", "accent", "Feature block", "Block name", "Feature stream", "Owner", "Area or owner", "Description", "What this feature block covers"),
        new("add-block-architecture", ProjectObjectType.ProjectBlock, "architecture", "blocks", "Architecture block", "Capture architecture, technical strategy, and system design concerns.", "arch", "accent", "Architecture block", "Block name", "Architecture block", "Area", "Platform, service, or layer", "Description", "Architecture focus and constraints"),
        new("add-block-implementation", ProjectObjectType.ProjectBlock, "implementation", "blocks", "Implementation block", "Track implementation work, execution lanes, and delivery scope.", "build", "accent", "Implementation block", "Block name", "Implementation block", "Owner", "Team or stream", "Description", "Implementation scope and outputs"),
        new("add-block-revision", ProjectObjectType.ProjectBlock, "revision", "blocks", "Revision block", "Track revisions, iteration loops, and refinement passes.", "rev", "warn", "Revision block", "Block name", "Revision block", "Cycle", "Revision number or cadence", "Description", "What this revision is changing"),
        new("add-block-testing", ProjectObjectType.ProjectBlock, "testing", "blocks", "Testing block", "Group test strategy, scenarios, and quality execution work.", "test", "warn", "Testing block", "Block name", "Testing block", "Phase", "Regression, exploratory, performance...", "Description", "What this testing block covers"),
        new("add-block-prompting", ProjectObjectType.ProjectBlock, "prompting", "blocks", "Prompting block", "Capture prompt-design work, experiments, and prompt-system scope.", "prompt", "accent", "Prompting block", "Block name", "Prompting block", "Intent", "Prompt objective", "Description", "How this prompting block is used"),
        new("add-block-research", ProjectObjectType.ProjectBlock, "research", "blocks", "Research block", "Track research tasks, comparisons, and discovery findings.", "research", "sky", "Research block", "Block name", "Research block", "Question", "What must be answered", "Description", "Research scope and expected output"),
        new("add-block-financial", ProjectObjectType.ProjectBlock, "financial", "blocks", "Financial block", "Track budget, commercial scope, and financial planning.", "money", "mint", "Financial block", "Block name", "Financial block", "Budget / owner", "Budget lane or owner", "Description", "Financial scope or constraints"),
        new("add-block-marketing", ProjectObjectType.ProjectBlock, "marketing", "blocks", "Marketing block", "Track positioning, launch support, and go-to-market work.", "market", "danger", "Marketing block", "Block name", "Marketing block", "Channel", "Audience or channel", "Description", "Marketing focus and deliverables"),
        new("add-block-operations", ProjectObjectType.ProjectBlock, "operations", "blocks", "Operations block", "Track operations, maintenance, and internal support work.", "ops", "primary", "Operations block", "Block name", "Operations block", "Owner", "Ops owner or team", "Description", "Operational scope and expectations"),
        new("add-block-delivery", ProjectObjectType.ProjectBlock, "delivery", "blocks", "Delivery block", "Track release readiness, rollout, and delivery coordination.", "ship", "warn", "Delivery block", "Block name", "Delivery block", "Release", "Target release or stream", "Description", "Delivery dependencies and outcome"),
        new("add-block-risk", ProjectObjectType.ProjectBlock, "risk", "blocks", "Risk block", "Track known risks, mitigations, and contingency work.", "risk", "danger", "Risk block", "Block name", "Risk block", "Severity", "High, medium, low...", "Description", "Risk details and mitigation"),
        new("add-block-compliance", ProjectObjectType.ProjectBlock, "compliance", "blocks", "Compliance block", "Track compliance, policy, audit, and governance requirements.", "audit", "danger", "Compliance block", "Block name", "Compliance block", "Framework", "ISO, SOC, policy...", "Description", "Compliance scope and evidence"),
        new("add-block-support", ProjectObjectType.ProjectBlock, "support", "blocks", "Support block", "Track enablement, support readiness, and handover work.", "support", "sky", "Support block", "Block name", "Support block", "Owner", "Support owner or queue", "Description", "Support coverage and handover notes"),
        new("add-prompt-flow", ProjectObjectType.PromptFlow, string.Empty, "prompts", "Flow", "Map a reusable prompt flow from the active node.", "flow", "accent", "Prompt flow", "Flow name", "Review flow", "Goal", "What the flow should produce", "Prompt strategy", "Describe the flow shape"),
        new("add-prompt-session", ProjectObjectType.PromptSession, string.Empty, "prompts", "Session", "Create a prompt session node and capture the first run context.", "session", "accent", "Prompt session", "Session name", "Prompt session", "Phase", "Discovery / review / test", "Intent", "What this session should do"),
        new("add-prompt-step", ProjectObjectType.PromptStep, string.Empty, "prompts", "Step", "Create a prompt step node from anywhere in the structure graph.", "step", "accent", "Prompt step", "Step title", "Gather context", "Branch label", "Optional branch or lane", "Prompt", "What this step should ask or do"),
        new("add-repository", ProjectObjectType.Repository, string.Empty, "assets", "Repo", "Add a repository or code artifact next to the current source.", "repo", "mint", "New repository", "Repo name", "frontend-app", "Owner / path", "team/repository", "Purpose", "What this repo is for"),
        new("add-file", ProjectObjectType.File, string.Empty, "assets", "File", "Upload or register a file node without leaving the canvas.", "file", "mint", "Uploaded file", "File title", "Architecture notes", "Folder / usage", "docs/architecture", "Purpose", "What lives in this file", true, "*/*", "Drop a file here or choose one."),
        new("add-image-asset", ProjectObjectType.ImageAsset, string.Empty, "assets", "Image", "Upload an image and keep it attached to the current part of the graph.", "image", "danger", "Uploaded image", "Image title", "Flow diagram", "Usage", "Where this image is used", "Description", "What this image explains", true, "image/*", "Drop an image here or choose one."),
        new("add-video-asset", ProjectObjectType.VideoAsset, string.Empty, "assets", "Video", "Upload a video and connect it to the selected node immediately.", "video", "accent", "Uploaded video", "Video title", "Demo recording", "Usage", "Where this video is used", "Description", "What this video demonstrates", true, "video/*", "Drop a video here or choose one."),
        new("add-link", ProjectObjectType.Link, string.Empty, "assets", "Link", "Create a link node and capture the address immediately.", "link", "sky", "New link", "Label", "API reference", "Address", "https://...", "Usage", "How this link is used"),
        new("add-connector", ProjectObjectType.Connector, string.Empty, "assets", "Connector", "Describe the integration or handoff point represented by this node.", "plug", "accent", "New connector", "Connector", "CI pipeline", "System", "Source or target system", "Handshake", "What the connector does"),
        new("add-secret-reference", ProjectObjectType.SecretReference, string.Empty, "assets", "Secret", "Track a secret reference without exposing the secret value itself.", "shield", "danger", "Secret reference", "Secret name", "API_KEY reference", "Vault / key", "Vault path or identifier", "Usage", "Where this secret is needed"),
        new("add-validation-run", ProjectObjectType.ValidationRun, string.Empty, "assurance", "Validation", "Capture a validation run and its acceptance criteria immediately.", "qa", "mint", "Validation run", "Validation", "Contract check", "Type", "Review, lint, runtime...", "Criteria", "What success means"),
        new("add-test-plan", ProjectObjectType.TestPlan, string.Empty, "assurance", "Test plan", "Create a test plan node and note the coverage target.", "test", "warn", "Test plan", "Plan name", "Regression sweep", "Phase", "Execution", "Coverage goal", "What this plan must cover"),
        new("add-test-evidence", ProjectObjectType.TestEvidence, string.Empty, "assurance", "Evidence", "Log the evidence item or result that supports a test plan.", "evidence", "warn", "Test evidence", "Evidence", "Screenshot bundle", "Source", "Run, file, or system", "Result", "What this evidence proves")
    ];

    private static readonly IReadOnlyDictionary<string, ProjectStructureCreateLeafDefinition> CreateLeafByActionId =
        CreateLeafDefinitions.ToDictionary(item => item.ActionId, StringComparer.OrdinalIgnoreCase);

    public static bool TryResolveCreateDefinition(string actionId, out ProjectStructureCreateLeafDefinition definition)
        => CreateLeafByActionId.TryGetValue(actionId, out definition!);

    public static string ResolveNodeLabel(ProjectStructureNode node) => node.ObjectType switch
    {
        ProjectObjectType.ProjectRoot => "Project",
        ProjectObjectType.Phase => "Phase",
        ProjectObjectType.ProjectBlock => ResolveProjectBlockLabel(node.ObjectSubtype),
        ProjectObjectType.PromptFlow => "Prompt flow",
        ProjectObjectType.PromptSession => "Prompt session",
        ProjectObjectType.PromptStep => "Prompt step",
        ProjectObjectType.Repository => "Repository",
        ProjectObjectType.File => "File",
        ProjectObjectType.ImageAsset => "Image",
        ProjectObjectType.VideoAsset => "Video",
        ProjectObjectType.Link => "Link",
        ProjectObjectType.Connector => "Connector",
        ProjectObjectType.ValidationRun => "Validation",
        ProjectObjectType.TestPlan => "Test plan",
        ProjectObjectType.TestEvidence => "Test evidence",
        ProjectObjectType.Milestone => "Milestone",
        ProjectObjectType.Decision => "Decision",
        ProjectObjectType.SecretReference => "Secret",
        _ => node.ObjectType.ToString()
    };

    public static string ResolveProjectBlockLabel(string subtype) => subtype switch
    {
        "feature" => "Feature block",
        "architecture" => "Architecture block",
        "implementation" => "Implementation block",
        "revision" => "Revision block",
        "testing" => "Testing block",
        "prompting" => "Prompting block",
        "research" => "Research block",
        "financial" => "Financial block",
        "marketing" => "Marketing block",
        "operations" => "Operations block",
        "delivery" => "Delivery block",
        "risk" => "Risk block",
        "compliance" => "Compliance block",
        "support" => "Support block",
        _ => "Project block"
    };

    public static IReadOnlyList<CanvasWorkbenchAction> BuildMenuCreateActions(ProjectObjectType? sourceType)
        => BuildTopLevelMenuEntries(sourceType)
            .Select(entry => entry.StartsWith("group:", StringComparison.Ordinal)
                ? BuildCreateGroupAction(entry["group:".Length..], sourceType)
                : BuildCreateLeafAction(entry))
            .ToList();

    public static IReadOnlyList<ProjectStructureInspectorCreateGroup> BuildInspectorCreateGroups(ProjectObjectType? sourceType)
    {
        var orderedGroups = ResolveInspectorGroupOrder(sourceType);
        return orderedGroups
            .Select((groupKey, index) =>
            {
                var group = CreateGroups[groupKey];
                var actions = ResolveGroupLeafDefinitions(groupKey, sourceType)
                    .Select(BuildCreateLeafAction)
                    .ToList();

                return new ProjectStructureInspectorCreateGroup(group.Key, group.Label, group.Description, index < 2, actions);
            })
            .ToList();
    }

    private static IReadOnlyList<string> BuildTopLevelMenuEntries(ProjectObjectType? sourceType) => sourceType switch
    {
        ProjectObjectType.PromptFlow or ProjectObjectType.PromptSession or ProjectObjectType.PromptStep =>
        ["add-note", "group:prompts", "add-decision", "group:assets", "group:assurance", "group:blocks", "add-milestone", "add-phase"],
        ProjectObjectType.Repository or ProjectObjectType.File or ProjectObjectType.ImageAsset or ProjectObjectType.VideoAsset or ProjectObjectType.Link or ProjectObjectType.Connector or ProjectObjectType.SecretReference =>
        ["add-note", "group:assets", "add-decision", "group:blocks", "group:assurance", "group:prompts", "add-milestone", "add-phase"],
        ProjectObjectType.ValidationRun or ProjectObjectType.TestPlan or ProjectObjectType.TestEvidence =>
        ["add-note", "group:assurance", "add-decision", "group:assets", "group:blocks", "group:prompts", "add-milestone", "add-phase"],
        _ =>
        ["add-note", "add-decision", "add-milestone", "add-phase", "group:blocks", "group:prompts", "group:assets", "group:assurance"]
    };

    private static IReadOnlyList<string> ResolveInspectorGroupOrder(ProjectObjectType? sourceType) => sourceType switch
    {
        ProjectObjectType.PromptFlow or ProjectObjectType.PromptSession or ProjectObjectType.PromptStep =>
        ["capture", "prompts", "assets", "assurance", "planning", "blocks"],
        ProjectObjectType.Repository or ProjectObjectType.File or ProjectObjectType.ImageAsset or ProjectObjectType.VideoAsset or ProjectObjectType.Link or ProjectObjectType.Connector or ProjectObjectType.SecretReference =>
        ["capture", "assets", "planning", "blocks", "prompts", "assurance"],
        ProjectObjectType.ValidationRun or ProjectObjectType.TestPlan or ProjectObjectType.TestEvidence =>
        ["capture", "assurance", "planning", "assets", "prompts", "blocks"],
        _ =>
        ["capture", "planning", "blocks", "prompts", "assets", "assurance"]
    };

    private static IReadOnlyList<ProjectStructureCreateLeafDefinition> ResolveGroupLeafDefinitions(string groupKey, ProjectObjectType? sourceType)
        => groupKey switch
        {
            "capture" => CreateLeafDefinitions.Where(item => item.GroupKey == "capture").ToList(),
            "planning" => CreateLeafDefinitions.Where(item => item.GroupKey == "planning").ToList(),
            "prompts" => ResolvePromptLeafDefinitions(sourceType),
            "assets" => ResolveAssetLeafDefinitions(sourceType),
            "assurance" => CreateLeafDefinitions.Where(item => item.GroupKey == "assurance").ToList(),
            _ => ResolveBlockLeafDefinitions()
        };

    private static IReadOnlyList<ProjectStructureCreateLeafDefinition> ResolvePromptLeafDefinitions(ProjectObjectType? sourceType)
    {
        var promptLeaves = CreateLeafDefinitions.Where(item => item.GroupKey == "prompts").ToDictionary(item => item.ActionId, StringComparer.OrdinalIgnoreCase);
        var actionIds = sourceType is ProjectObjectType.PromptFlow or ProjectObjectType.PromptSession or ProjectObjectType.PromptStep
            ? new[] { "add-prompt-step", "add-prompt-session", "add-prompt-flow" }
            : new[] { "add-prompt-session", "add-prompt-flow", "add-prompt-step" };

        return actionIds.Select(actionId => promptLeaves[actionId]).ToList();
    }

    private static IReadOnlyList<ProjectStructureCreateLeafDefinition> ResolveAssetLeafDefinitions(ProjectObjectType? sourceType)
    {
        var assetLeaves = CreateLeafDefinitions.Where(item => item.GroupKey == "assets").ToDictionary(item => item.ActionId, StringComparer.OrdinalIgnoreCase);
        var actionIds = sourceType is ProjectObjectType.Repository or ProjectObjectType.File or ProjectObjectType.ImageAsset or ProjectObjectType.VideoAsset or ProjectObjectType.Link or ProjectObjectType.Connector or ProjectObjectType.SecretReference
            ? new[] { "add-file", "add-link", "add-image-asset", "add-video-asset", "add-connector", "add-repository", "add-secret-reference" }
            : new[] { "add-repository", "add-file", "add-image-asset", "add-video-asset", "add-link", "add-connector", "add-secret-reference" };

        return actionIds.Select(actionId => assetLeaves[actionId]).ToList();
    }

    private static IReadOnlyList<ProjectStructureCreateLeafDefinition> ResolveBlockLeafDefinitions()
    {
        var order =
            new[]
            {
                "add-block-feature",
                "add-block-architecture",
                "add-block-implementation",
                "add-block-testing",
                "add-block-prompting",
                "add-block-revision",
                "add-block-research",
                "add-block-delivery",
                "add-block-operations",
                "add-block-financial",
                "add-block-marketing",
                "add-block-risk",
                "add-block-compliance",
                "add-block-support"
            };

        var blockLeaves = CreateLeafDefinitions.Where(item => item.GroupKey == "blocks").ToDictionary(item => item.ActionId, StringComparer.OrdinalIgnoreCase);
        return order.Select(actionId => blockLeaves[actionId]).ToList();
    }

    private static CanvasWorkbenchAction BuildCreateGroupAction(string groupKey, ProjectObjectType? sourceType)
    {
        var group = CreateGroups[groupKey];
        return new CanvasWorkbenchAction
        {
            ActionId = $"group-{group.Key}",
            Label = group.Label,
            Description = group.Description,
            Icon = group.Icon,
            Tone = group.Tone,
            Children = ResolveGroupLeafDefinitions(groupKey, sourceType)
                .Select(BuildCreateLeafAction)
                .ToList()
        };
    }

    private static CanvasWorkbenchAction BuildCreateLeafAction(string actionId)
        => BuildCreateLeafAction(CreateLeafByActionId[actionId]);

    private static CanvasWorkbenchAction BuildCreateLeafAction(ProjectStructureCreateLeafDefinition definition)
        => new()
        {
            ActionId = definition.ActionId,
            Label = definition.Label,
            Description = definition.Description,
            Icon = definition.Icon,
            Tone = definition.Tone,
            RequiresInput = true,
            CreateMode = "dialog",
            ObjectSubtype = definition.ObjectSubtype,
            TitleLabel = definition.TitleLabel,
            TitlePlaceholder = definition.TitlePlaceholder,
            SubtitleLabel = definition.SubtitleLabel,
            SubtitlePlaceholder = definition.SubtitlePlaceholder,
            NotesLabel = definition.NotesLabel,
            NotesPlaceholder = definition.NotesPlaceholder,
            RequiresFile = definition.RequiresFile,
            AcceptedFileTypes = definition.AcceptedFileTypes,
            FilePrompt = definition.FilePrompt,
            SupportsDragDrop = definition.RequiresFile
        };
}
