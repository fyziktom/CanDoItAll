using CanDoItAll.Components.CanvasLib;
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
    string FilePrompt = "Drop a file here or choose one.",
    bool ShowDefaultTextFields = true,
    string SubmitLabel = "Create",
    IReadOnlyList<CanvasWorkbenchInputField>? InputFields = null,
    IReadOnlyList<CanvasWorkbenchInputValue>? DefaultInputValues = null);

public sealed record ProjectStructureMutationTypeOption(
    string ActionId,
    string ObjectSubtype,
    string Label,
    string Description,
    string Icon,
    string Tone);

public sealed record ProjectStructureInspectorCreateGroup(
    string Key,
    string Label,
    string Description,
    bool IsOpen,
    IReadOnlyList<CanvasWorkbenchAction> Actions);

internal static partial class ProjectStructureCanvasCatalog
{
    private static readonly IReadOnlyList<CanvasWorkbenchInputField> DeliveryTargetRootFields =
    [
        Field("outputRoot", "Output root", "text", @"C:\repositories\CanDoItAll\output\App"),
        Field("targetRoot", "Target root", "text", @"C:\repositories\CanDoItAll\output\App"),
        Field("repositoryRoot", "Repository root", "text", @"C:\repositories\CanDoItAll")
    ];

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
            ["meetings"] = new("meetings", "Meetings", "Meetings, recordings, and transcript flows stay explicit on the canvas.", "sky", "meeting"),
            ["people"] = new("people", "People", "Participants and lightweight org-chart nodes stay reusable.", "mint", "people"),
            ["work"] = new("work", "Work", "Tasks, issues, follow-ups, and operational delivery nodes.", "warn", "task"),
            ["prompts"] = new("prompts", "Prompts", "Prompt flows, sessions, and executable steps.", "accent", "prompt"),
            ["runtime"] = new("runtime", "Runtime", "Repositories, scripts, environments, and local execution context.", "mint", "runtime"),
            ["assets"] = new("assets", "Assets", "Repositories, files, uploads, links, and system touchpoints.", "sky", "asset"),
            ["infrastructure"] = new("infrastructure", "Infrastructure", "Servers, domains, deployment details, keys, and AI references.", "danger", "infra"),
            ["assurance"] = new("assurance", "Assurance", "Validation, test planning, and supporting evidence.", "warn", "assure")
        };

    private static readonly Lazy<IReadOnlyList<ProjectStructureCreateLeafDefinition>> LazyCreateLeafDefinitions = new(() =>
    {
        List<ProjectStructureCreateLeafDefinition> definitions =
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
            new("add-block-delivery", ProjectObjectType.ProjectBlock, "delivery", "blocks", "Delivery block", "Track release readiness, rollout, and delivery coordination.", "ship", "warn", "Delivery block", "Block name", "Delivery block", "Release", "Target release or stream", "Description", "Delivery dependencies and outcome", false, string.Empty, "Drop a file here or choose one.", true, "Create", DeliveryTargetRootFields),
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

        definitions.AddRange(RichCreateLeafDefinitions ?? []);
        return definitions;
    });

    private static readonly Lazy<IReadOnlyDictionary<string, ProjectStructureCreateLeafDefinition>> LazyCreateLeafByActionId =
        new(() => CreateLeafDefinitions.ToDictionary(item => item.ActionId, StringComparer.OrdinalIgnoreCase));

    private static IReadOnlyList<ProjectStructureCreateLeafDefinition> CreateLeafDefinitions
        => LazyCreateLeafDefinitions.Value;

    private static IReadOnlyDictionary<string, ProjectStructureCreateLeafDefinition> CreateLeafByActionId
        => LazyCreateLeafByActionId.Value;

    public static bool TryResolveCreateDefinition(string actionId, out ProjectStructureCreateLeafDefinition definition)
        => CreateLeafByActionId.TryGetValue(actionId, out definition!);

    public static bool TryResolveCreateDefinition(ProjectObjectType objectType, string? objectSubtype, out ProjectStructureCreateLeafDefinition definition)
    {
        var normalizedSubtype = objectSubtype?.Trim() ?? string.Empty;
        definition = CreateLeafDefinitions.FirstOrDefault(item =>
            item.ObjectType == objectType &&
            string.Equals(item.ObjectSubtype, normalizedSubtype, StringComparison.OrdinalIgnoreCase))
            ?? CreateLeafDefinitions.FirstOrDefault(item =>
                item.ObjectType == objectType &&
                string.IsNullOrWhiteSpace(item.ObjectSubtype))!;
        return definition is not null;
    }

    public static CanvasWorkbenchAction BuildComposerAction(ProjectStructureCreateLeafDefinition definition)
        => BuildCreateLeafAction(definition);

    public static string ResolveNodeLabel(ProjectStructureNode node)
        => ProjectNodeKindRegistry.ResolveLabel(node.ObjectType, node.ObjectSubtype);

    public static string ResolveProjectBlockLabel(string subtype)
        => ProjectNodeKindRegistry.ResolveLabel(ProjectObjectType.ProjectBlock, subtype);

    public static IReadOnlyList<CanvasWorkbenchAction> BuildMenuCreateActions(ProjectObjectType? sourceType)
    {
        var actions = BuildTopLevelMenuEntries(sourceType)
            .Select(entry => entry.StartsWith("group:", StringComparison.Ordinal)
                ? BuildCreateGroupAction(entry["group:".Length..], sourceType)
                : BuildCreateLeafAction(entry))
            .ToList();

        return ProjectStructureActionShortcuts.Apply(actions);
    }

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
                ProjectStructureActionShortcuts.Apply(actions);

                return new ProjectStructureInspectorCreateGroup(group.Key, group.Label, group.Description, index < 2, actions);
            })
            .ToList();
    }

    public static IReadOnlyList<ProjectStructureMutationTypeOption> BuildCommonBlockTypeOptions()
        => ResolveBlockLeafDefinitions()
            .Select(definition => new ProjectStructureMutationTypeOption(
                definition.ActionId,
                definition.ObjectSubtype,
                definition.Label,
                definition.Description,
                definition.Icon,
                definition.Tone))
            .ToList();

    public static IReadOnlyList<ProjectStructureMutationTypeOption> BuildNoteConversionOptions()
        => ResolveLeafDefinitions(
                "add-decision",
                "add-work-task",
                "add-work-issue",
                "add-work-feedback",
                "add-work-revision",
                "add-work-payment",
                "add-work-send",
                "add-block-feature",
                "add-block-architecture",
                "add-block-implementation",
                "add-block-revision",
                "add-block-testing",
                "add-block-prompting",
                "add-block-research",
                "add-block-financial",
                "add-block-marketing",
                "add-block-operations",
                "add-block-delivery",
                "add-block-risk",
                "add-block-compliance",
                "add-block-support",
                "add-block-deployment",
                "add-block-repos",
                "add-block-dockers",
                "add-block-task-flow",
                "add-block-backlog",
                "add-block-server",
                "add-block-computer",
                "add-block-router",
                "add-block-wifi")
            .Where(definition => ProjectNodeKindRegistry.CanReclassify(ProjectObjectType.Note, string.Empty, definition.ObjectType, definition.ObjectSubtype))
            .Select(definition => new ProjectStructureMutationTypeOption(
                definition.ActionId,
                definition.ObjectSubtype,
                definition.Label,
                definition.Description,
                definition.Icon,
                definition.Tone))
            .ToList();

    public static ProjectStructureNodeCatalogResponse BuildAgentNodeCatalog()
    {
        var items = CreateLeafDefinitions
            .OrderBy(item => item.GroupKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .Select(BuildCatalogItem)
            .ToList();
        var creatableSubtypesByType = items
            .GroupBy(item => item.ObjectType)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(item => item.ObjectSubtype)
                    .Where(subtype => !string.IsNullOrWhiteSpace(subtype))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(subtype => subtype, StringComparer.OrdinalIgnoreCase)
                    .ToList());
        var objectTypes = Enum.GetValues<ProjectObjectType>()
            .Select(objectType => new ProjectStructureNodeCatalogObjectType(
                objectType,
                ProjectNodeKindRegistry.ResolveLabel(objectType, string.Empty),
                items.Any(item => item.ObjectType == objectType),
                creatableSubtypesByType.TryGetValue(objectType, out var subtypes) ? subtypes : []))
            .ToList();
        var linkKinds = Enum.GetValues<ProjectObjectLinkKind>()
            .Select(linkKind => new ProjectStructureLinkKindCatalogItem(
                linkKind,
                linkKind.ToString(),
                ResolveLinkKindGuidance(linkKind)))
            .ToList();

        return new ProjectStructureNodeCatalogResponse(
            items,
            objectTypes,
            linkKinds,
            [
                "Use objectType WorkItem with objectSubtype task for work task nodes. Do not invent node enum names such as WorkTask or TaskNode.",
                "Use ProjectBlock plus a lowercase objectSubtype such as feature, implementation, testing, delivery, backlog, task-flow, risk, server, or wifi for typed blocks. For delivery targets, set metadata.projectBlock.outputRoot or metadata.projectBlock.targetRoot to the destination folder.",
                "Use Repository with objectSubtype folder and metadata.repository.localPath for local folder nodes; set metadata.repository.relativePath only when the node should point inside that folder.",
                "Use File plus a file subtype such as pdf, excel, docx, markdown, mermaid, screenshot, log, archive, or audio for generated or uploaded files; set metadata.file.externalPath when the file already exists on a local drive.",
                "Use Link for web links and Repository remote for source-control repositories. GitHub and GitLab URLs are recognized from link.url or repository.repositoryUrl, including SSH-style git@host:owner/repo.git addresses.",
                "Use Script with subtypes powershell, console, ef-migration, or tailwind-watch for runtime scripts; set metadata.script.command, arguments, scriptPath, and workingDirectory as needed.",
                "Use Environment with subtype python, dotnet-runtime, dotnet-watch, or dotnet-release for language runtimes; .NET nodes need metadata.environment.projectPath and workingDirectory when the project path is relative, while Python nodes need projectPath, pythonProvider, and environmentName.",
                "Do not store runnable commands as ProjectBlock delivery nodes. If a user should be able to double-click and run a command, use Script, Environment, or Infrastructure with the required runtime metadata.",
                "Use Infrastructure with objectSubtype docker-mode for Docker runtime nodes; set metadata.infrastructure.runtimeCommand, runtimeArguments, workingDirectory, and folderPath so double-click can offer Run normally and Run as administrator.",
                "Use Infrastructure with objectSubtype deployment-folder and metadata.infrastructure.folderPath for deployment folders that should open in File Explorer.",
                "When creating several task nodes, decide whether any task depends on another and create DependsOn links from dependent task to prerequisite task.",
                "Every user-created node must have a parentNodeKey. Use project:{projectId} for top-level nodes or an existing node id for child nodes."
            ]);
    }

    private static IReadOnlyList<string> BuildTopLevelMenuEntries(ProjectObjectType? sourceType) => sourceType switch
    {
        ProjectObjectType.PromptFlow or ProjectObjectType.PromptSession or ProjectObjectType.PromptStep =>
        ["add-note", "group:prompts", "group:work", "add-decision", "group:runtime", "group:assets", "group:assurance", "group:blocks", "add-milestone", "add-phase"],
        ProjectObjectType.Repository or ProjectObjectType.File or ProjectObjectType.ImageAsset or ProjectObjectType.VideoAsset or ProjectObjectType.Link or ProjectObjectType.Connector or ProjectObjectType.SecretReference =>
        ["add-note", "group:runtime", "group:assets", "group:infrastructure", "add-decision", "group:work", "group:assurance", "group:prompts", "add-milestone", "add-phase"],
        ProjectObjectType.ValidationRun or ProjectObjectType.TestPlan or ProjectObjectType.TestEvidence =>
        ["add-note", "group:assurance", "group:work", "add-decision", "group:assets", "group:blocks", "group:prompts", "add-milestone", "add-phase"],
        ProjectObjectType.Meeting or ProjectObjectType.Recording or ProjectObjectType.Transcript =>
        ["add-note", "group:meetings", "group:people", "group:work", "group:assets", "group:assurance", "group:blocks", "add-milestone", "add-phase"],
        ProjectObjectType.Participant =>
        ["add-note", "group:people", "group:meetings", "group:work", "group:assets", "group:blocks", "add-milestone", "add-phase"],
        ProjectObjectType.WorkItem =>
        ["add-note", "group:work", "group:people", "group:runtime", "group:assets", "group:assurance", "group:blocks", "add-milestone", "add-phase"],
        ProjectObjectType.Script or ProjectObjectType.Environment or ProjectObjectType.Infrastructure =>
        ["add-note", "group:runtime", "group:infrastructure", "group:work", "group:assets", "group:assurance", "group:blocks", "add-milestone", "add-phase"],
        _ =>
        ["add-note", "add-decision", "add-milestone", "add-phase", "group:blocks", "group:meetings", "group:people", "group:work", "group:prompts", "group:runtime", "group:assets", "group:infrastructure", "group:assurance"]
    };

    private static IReadOnlyList<string> ResolveInspectorGroupOrder(ProjectObjectType? sourceType) => sourceType switch
    {
        ProjectObjectType.PromptFlow or ProjectObjectType.PromptSession or ProjectObjectType.PromptStep =>
        ["capture", "prompts", "work", "runtime", "assets", "assurance", "planning", "blocks"],
        ProjectObjectType.Repository or ProjectObjectType.File or ProjectObjectType.ImageAsset or ProjectObjectType.VideoAsset or ProjectObjectType.Link or ProjectObjectType.Connector or ProjectObjectType.SecretReference =>
        ["capture", "runtime", "assets", "infrastructure", "planning", "blocks", "prompts", "assurance"],
        ProjectObjectType.ValidationRun or ProjectObjectType.TestPlan or ProjectObjectType.TestEvidence =>
        ["capture", "assurance", "work", "planning", "assets", "prompts", "blocks"],
        ProjectObjectType.Meeting or ProjectObjectType.Recording or ProjectObjectType.Transcript =>
        ["capture", "meetings", "people", "work", "assets", "assurance", "planning", "blocks"],
        ProjectObjectType.Participant =>
        ["capture", "people", "meetings", "work", "planning", "blocks", "assets"],
        ProjectObjectType.WorkItem =>
        ["capture", "work", "people", "runtime", "assets", "assurance", "planning", "blocks"],
        ProjectObjectType.Script or ProjectObjectType.Environment or ProjectObjectType.Infrastructure =>
        ["capture", "runtime", "infrastructure", "work", "assets", "assurance", "planning", "blocks"],
        _ =>
        ["capture", "planning", "blocks", "meetings", "people", "work", "prompts", "runtime", "assets", "infrastructure", "assurance"]
    };

    private static IReadOnlyList<ProjectStructureCreateLeafDefinition> ResolveGroupLeafDefinitions(string groupKey, ProjectObjectType? sourceType)
        => groupKey switch
        {
            "capture" => CreateLeafDefinitions.Where(item => item.GroupKey == "capture").ToList(),
            "planning" => CreateLeafDefinitions.Where(item => item.GroupKey == "planning").ToList(),
            "meetings" => ResolveMeetingLeafDefinitions(),
            "people" => ResolveParticipantLeafDefinitions(),
            "work" => ResolveWorkLeafDefinitions(),
            "prompts" => ResolvePromptLeafDefinitions(sourceType),
            "runtime" => ResolveRuntimeLeafDefinitions(),
            "assets" => ResolveAssetLeafDefinitions(sourceType),
            "infrastructure" => ResolveInfrastructureLeafDefinitions(),
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
            ? new[] { "add-file", "add-file-pdf", "add-file-excel", "add-file-docx", "add-file-markdown", "add-file-mermaid", "add-file-screenshot", "add-file-log", "add-file-audio", "add-link", "add-image-asset", "add-video-asset", "add-connector", "add-secret-reference" }
            : new[] { "add-file-pdf", "add-file-excel", "add-file-docx", "add-file-text", "add-file-json", "add-file-markdown", "add-file-mermaid", "add-file-screenshot", "add-file-log", "add-file-archive", "add-file-audio", "add-repository", "add-file", "add-image-asset", "add-video-asset", "add-link", "add-connector", "add-secret-reference" };

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
                "add-block-deployment",
                "add-block-repos",
                "add-block-dockers",
                "add-block-task-flow",
                "add-block-backlog",
                "add-block-server",
                "add-block-computer",
                "add-block-router",
                "add-block-wifi",
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
            MenuLabel = group.Label,
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
            MenuLabel = ResolveCreateMenuLabel(definition),
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
            ShowDefaultTextFields = definition.ShowDefaultTextFields,
            SubmitLabel = definition.SubmitLabel,
            RequiresFile = definition.RequiresFile,
            AcceptedFileTypes = definition.AcceptedFileTypes,
            FilePrompt = definition.FilePrompt,
            SupportsDragDrop = definition.RequiresFile,
            InputFields = definition.InputFields?.ToList() ?? [],
            DefaultInputValues = definition.DefaultInputValues?.ToList() ?? []
        };

    private static ProjectStructureNodeCatalogItem BuildCatalogItem(ProjectStructureCreateLeafDefinition definition)
        => new(
            definition.ActionId,
            definition.ObjectType,
            definition.ObjectSubtype,
            definition.GroupKey,
            definition.Label,
            definition.Description,
            definition.DefaultTitle,
            definition.TitleLabel,
            definition.SubtitleLabel,
            definition.NotesLabel,
            definition.RequiresFile,
            definition.AcceptedFileTypes,
            definition.InputFields?.Select(field => new ProjectStructureNodeCatalogField(
                field.Key,
                field.Label,
                field.InputMode,
                field.Placeholder,
                field.IsRequired,
                field.Options.Select(option => new ProjectStructureNodeCatalogOption(option.Value, option.Label)).ToList())).ToList() ?? [],
            definition.DefaultInputValues?.Select(item => new ProjectStructureNodeCatalogDefaultValue(item.Key, item.Value)).ToList() ?? [],
            BuildCatalogAliases(definition));

    private static IReadOnlyList<string> BuildCatalogAliases(ProjectStructureCreateLeafDefinition definition)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            definition.ActionId,
            definition.Label,
            definition.DefaultTitle,
            definition.ObjectType.ToString()
        };

        if (!string.IsNullOrWhiteSpace(definition.ObjectSubtype))
        {
            aliases.Add(definition.ObjectSubtype);
            aliases.Add($"{definition.ObjectType}:{definition.ObjectSubtype}");
            aliases.Add($"{definition.ObjectType} {definition.ObjectSubtype}");
        }

        if (definition.ObjectType == ProjectObjectType.WorkItem && string.Equals(definition.ObjectSubtype, "task", StringComparison.OrdinalIgnoreCase))
        {
            aliases.Add("task");
            aliases.Add("work task");
            aliases.Add("work item");
            aliases.Add("work item task");
        }

        if (definition.ObjectType == ProjectObjectType.File && string.Equals(definition.ObjectSubtype, "mermaid", StringComparison.OrdinalIgnoreCase))
        {
            aliases.Add("diagram");
            aliases.Add("mermaid diagram");
        }

        AddCatalogAliases(definition, aliases);

        return aliases
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Select(alias => alias.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(alias => alias, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddCatalogAliases(ProjectStructureCreateLeafDefinition definition, HashSet<string> aliases)
    {
        switch (definition.ObjectType)
        {
            case ProjectObjectType.Repository:
                aliases.Add("repository node");
                aliases.Add("repo node");
                if (string.Equals(definition.ObjectSubtype, "remote", StringComparison.OrdinalIgnoreCase))
                {
                    aliases.Add("remote repository");
                    aliases.Add("repository link");
                    aliases.Add("github repository");
                    aliases.Add("gitlab repository");
                    aliases.Add("github repo");
                    aliases.Add("gitlab repo");
                }
                else if (string.Equals(definition.ObjectSubtype, "folder", StringComparison.OrdinalIgnoreCase))
                {
                    aliases.Add("folder");
                    aliases.Add("folder node");
                    aliases.Add("local folder");
                    aliases.Add("directory");
                    aliases.Add("open folder");
                }

                break;
            case ProjectObjectType.File:
                aliases.Add("file node");
                aliases.Add("local file");
                aliases.Add("external file");
                if (!string.IsNullOrWhiteSpace(definition.ObjectSubtype))
                {
                    aliases.Add($"{definition.ObjectSubtype} file");
                }

                break;
            case ProjectObjectType.Link:
                aliases.Add("web link");
                aliases.Add("github link");
                aliases.Add("gitlab link");
                aliases.Add("url");
                break;
            case ProjectObjectType.Script:
                aliases.Add("runtime script");
                aliases.Add("script node");
                aliases.Add("command node");
                if (string.Equals(definition.ObjectSubtype, "powershell", StringComparison.OrdinalIgnoreCase))
                {
                    aliases.Add("powershell runtime");
                    aliases.Add("powershell script");
                    aliases.Add("ps1 runtime");
                }

                break;
            case ProjectObjectType.Environment:
                aliases.Add("runtime node");
                if (string.Equals(definition.ObjectSubtype, "python", StringComparison.OrdinalIgnoreCase))
                {
                    aliases.Add("python runtime");
                    aliases.Add("python environment");
                }
                else if (definition.ObjectSubtype.StartsWith("dotnet", StringComparison.OrdinalIgnoreCase))
                {
                    aliases.Add(".net runtime");
                    aliases.Add("dotnet runtime");
                }

                break;
            case ProjectObjectType.Infrastructure:
                if (string.Equals(definition.ObjectSubtype, "docker-mode", StringComparison.OrdinalIgnoreCase))
                {
                    aliases.Add("docker runtime");
                    aliases.Add("docker compose");
                    aliases.Add("container runtime");
                    aliases.Add("docker node");
                }
                else if (string.Equals(definition.ObjectSubtype, "deployment-folder", StringComparison.OrdinalIgnoreCase))
                {
                    aliases.Add("deployment folder");
                    aliases.Add("folder node");
                    aliases.Add("local folder");
                }

                break;
        }
    }

    private static string ResolveLinkKindGuidance(ProjectObjectLinkKind linkKind)
        => linkKind switch
        {
            ProjectObjectLinkKind.DependsOn => "Use for task and scheduling prerequisites. The source node depends on the target node.",
            ProjectObjectLinkKind.Contains => "System-owned parent/root containment. Prefer parentNodeKey or reparent tools instead of authoring this directly.",
            ProjectObjectLinkKind.BelongsTo => "System-owned editable parent relationship. Prefer parentNodeKey or reparent tools instead of authoring this directly.",
            ProjectObjectLinkKind.Uses => "Use when one node consumes, references, or needs another artifact or resource.",
            ProjectObjectLinkKind.Validates => "Use when validation evidence or a validation run validates another node.",
            ProjectObjectLinkKind.Tests => "Use when a test plan or test evidence tests another node.",
            ProjectObjectLinkKind.Blocks => "Use when a node blocks another node without representing a direct schedule prerequisite.",
            ProjectObjectLinkKind.DerivedFrom => "Use when a new asset or node is a revision or derivation of another node.",
            _ => string.Empty
        };

    private static string ResolveCreateMenuLabel(ProjectStructureCreateLeafDefinition definition)
        => definition.ActionId switch
        {
            "add-test-plan" => "Plan",
            "add-test-evidence" => "Evidence",
            "add-secret-reference" => "Secret",
            "add-prompt-session" => "Session",
            "add-prompt-flow" => "Flow",
            "add-prompt-step" => "Step",
            "add-image-asset" => "Image",
            "add-video-asset" => "Video",
            "add-link" => "Link",
            "add-file" => "File",
            "add-file-docx" => "Word",
            "add-meeting-online" => "Online",
            "add-meeting-onsite" => "Onsite",
            "add-recording" => "Recording",
            "add-transcript" => "Transcript",
            "add-validation-run" => "Validation",
            _ => TrimMenuLabel(definition.Label)
        };

    private static string TrimMenuLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return "Item";
        }

        var normalized = label.Trim();
        if (normalized.EndsWith(" block", StringComparison.OrdinalIgnoreCase))
        {
            return normalized[..^6];
        }

        if (normalized.Contains(' ', StringComparison.Ordinal))
        {
            var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length == 0 ? normalized : parts[^1] switch
            {
                "plan" => parts[0],
                "block" => parts[0],
                _ => parts[0]
            };
        }

        return normalized;
    }
}


