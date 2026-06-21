using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal enum ProjectNodeKindFamily
{
    None,
    ProjectBlock,
    Meeting,
    Recording,
    Transcript,
    Participant,
    WorkItem,
    Repository,
    File,
    Script,
    Environment,
    Infrastructure,
    SecretReference,
    Link,
    Workflow
}

internal sealed record ProjectNodeKindDescriptor(
    ProjectObjectType ObjectType,
    string ObjectSubtype,
    ProjectNodeKindFamily Family,
    string Label,
    string PluginOwner,
    bool SupportsSubtypeMutation,
    bool AllowNotePromotion,
    bool IncludeInCommonBlockOptions,
    Func<string, ProjectObjectVisualProfile> BuildVisualProfile,
    Action<ProjectObjectMetadataEnvelope, ProjectNodeMetadataNormalizationContext>? NormalizeMetadata = null,
    IReadOnlyList<ProjectPartyAssignmentRole>? AllowedAssignmentRoles = null,
    IReadOnlyList<ProjectPartyAssignmentRole>? ReplacementRoles = null,
    ProjectPartyAssignmentRole? PreferredRole = null);

internal sealed record ProjectNodeMetadataNormalizationContext(
    ProjectObjectType ObjectType,
    string ObjectSubtype,
    string Notes,
    SavedMediaDescriptor? Media);

internal static class ProjectNodeKindRegistry
{
    private static readonly IReadOnlyDictionary<(ProjectObjectType ObjectType, string ObjectSubtype), ProjectNodeKindDescriptor> Descriptors =
        BuildDescriptors();
    private static readonly IReadOnlySet<ProjectPartyAssignmentRole> CanonicalNodeOptionalRoles =
        new HashSet<ProjectPartyAssignmentRole>
        {
            ProjectPartyAssignmentRole.TeamMember,
            ProjectPartyAssignmentRole.DeliveryUnit,
            ProjectPartyAssignmentRole.Partner,
            ProjectPartyAssignmentRole.AiAgent
        };
    private static readonly IReadOnlySet<ProjectPartyAssignmentRole> CanonicalNodeRequiredRoles =
        new HashSet<ProjectPartyAssignmentRole>
        {
            ProjectPartyAssignmentRole.MeetingParticipant,
            ProjectPartyAssignmentRole.WorkItemAssignee
        };
    private static readonly IReadOnlyList<ProjectPartyAssignmentRole> ParticipantReplacementRoles =
    [
        ProjectPartyAssignmentRole.TeamMember,
        ProjectPartyAssignmentRole.DeliveryUnit,
        ProjectPartyAssignmentRole.Partner,
        ProjectPartyAssignmentRole.AiAgent
    ];
    private static readonly IReadOnlyList<ProjectPartyAssignmentRole> MeetingAssignmentRoles =
    [
        ProjectPartyAssignmentRole.MeetingParticipant
    ];
    private static readonly IReadOnlyList<ProjectPartyAssignmentRole> WorkItemAssignmentRoles =
    [
        ProjectPartyAssignmentRole.WorkItemAssignee
    ];

    public static ProjectNodeKindDescriptor ResolveDescriptor(ProjectObjectType objectType, string? objectSubtype)
    {
        var normalizedSubtype = NormalizeSubtype(objectSubtype);
        return Descriptors.TryGetValue((objectType, normalizedSubtype), out var descriptor)
            ? descriptor
            : Descriptors.GetValueOrDefault((objectType, string.Empty))
                ?? new ProjectNodeKindDescriptor(
                    objectType,
                    string.Empty,
                    ProjectNodeKindFamily.None,
                    objectType.ToString(),
                    "core",
                    false,
                    false,
                    false,
                    _ => Profile("rect", "#d97706", "NT", "Note", ProjectObjectPaletteKeys.Warning));
    }

    public static string ResolveLabel(ProjectObjectType objectType, string? objectSubtype)
    {
        var descriptor = ResolveDescriptor(objectType, objectSubtype);
        return string.IsNullOrWhiteSpace(descriptor.ObjectSubtype) && !string.IsNullOrWhiteSpace(objectSubtype)
            ? objectSubtype!.Trim()
            : descriptor.Label;
    }

    public static string ResolveSubtypeBadge(ProjectObjectType objectType, string? objectSubtype)
    {
        if (string.IsNullOrWhiteSpace(objectSubtype))
        {
            return string.Empty;
        }

        var descriptor = ResolveDescriptor(objectType, objectSubtype);
        return string.IsNullOrWhiteSpace(descriptor.ObjectSubtype)
            ? objectSubtype!.Trim()
            : descriptor.Label;
    }

    public static ProjectObjectVisualProfile ResolveVisualProfile(ProjectObjectType objectType, string? objectSubtype, string? status)
        => ResolveDescriptor(objectType, objectSubtype).BuildVisualProfile(status ?? string.Empty);

    public static bool CanReclassify(
        ProjectObjectType currentType,
        string? currentSubtype,
        ProjectObjectType targetType,
        string? targetSubtype)
    {
        var current = ResolveDescriptor(currentType, currentSubtype);
        var target = ResolveDescriptor(targetType, targetSubtype);

        if (currentType == targetType)
        {
            return current.SupportsSubtypeMutation &&
                target.SupportsSubtypeMutation &&
                current.Family != ProjectNodeKindFamily.None &&
                current.Family == target.Family;
        }

        return currentType == ProjectObjectType.Note && target.AllowNotePromotion;
    }

    public static ProjectNodeKindFamily ResolveFamily(ProjectObjectType objectType, string? objectSubtype)
        => ResolveDescriptor(objectType, objectSubtype).Family;

    public static IReadOnlyList<ProjectPartyAssignmentRole> ResolveAllowedAssignmentRoles(ProjectObjectType objectType, string? objectSubtype)
    {
        return objectType switch
        {
            ProjectObjectType.Participant =>
            [
                ResolveParticipantAssignmentRole(ResolveParticipantKind(objectSubtype))
            ],
            ProjectObjectType.Meeting => MeetingAssignmentRoles,
            ProjectObjectType.WorkItem => WorkItemAssignmentRoles,
            _ => ResolveDescriptor(objectType, objectSubtype).AllowedAssignmentRoles ?? []
        };
    }

    public static IReadOnlyList<ProjectPartyAssignmentRole> ResolveReplacementRoles(ProjectObjectType objectType, string? objectSubtype)
    {
        return objectType switch
        {
            ProjectObjectType.Participant => ParticipantReplacementRoles,
            ProjectObjectType.Meeting => MeetingAssignmentRoles,
            ProjectObjectType.WorkItem => WorkItemAssignmentRoles,
            _ =>
                ResolveDescriptor(objectType, objectSubtype).ReplacementRoles ??
                ResolveDescriptor(objectType, objectSubtype).AllowedAssignmentRoles ??
                []
        };
    }

    public static ProjectPartyAssignmentRole? ResolvePreferredRole(ProjectObjectType objectType, string? objectSubtype)
    {
        switch (objectType)
        {
            case ProjectObjectType.Participant:
                return ResolveParticipantAssignmentRole(ResolveParticipantKind(objectSubtype));
            case ProjectObjectType.Meeting:
                return ProjectPartyAssignmentRole.MeetingParticipant;
            case ProjectObjectType.WorkItem:
                return ProjectPartyAssignmentRole.WorkItemAssignee;
        }

        var descriptor = ResolveDescriptor(objectType, objectSubtype);
        if (descriptor.PreferredRole.HasValue)
        {
            return descriptor.PreferredRole.Value;
        }

        return descriptor.AllowedAssignmentRoles is { Count: > 0 }
            ? descriptor.AllowedAssignmentRoles[0]
            : null;
    }

    public static bool AllowsAssignmentRole(ProjectObjectType objectType, string? objectSubtype, ProjectPartyAssignmentRole role)
        => ResolveAllowedAssignmentRoles(objectType, objectSubtype).Contains(role);

    public static bool SupportsCanonicalNodeScope(ProjectPartyAssignmentRole role)
        => CanonicalNodeRequiredRoles.Contains(role) || CanonicalNodeOptionalRoles.Contains(role);

    public static bool RequiresCanonicalNodeScope(ProjectPartyAssignmentRole role)
        => CanonicalNodeRequiredRoles.Contains(role);

    public static ProjectParticipantKind ResolveParticipantKind(string? objectSubtype)
    {
        var metadata = NormalizeMetadata(ProjectObjectType.Participant, objectSubtype, new ProjectObjectMetadataEnvelope(), string.Empty, null);
        return metadata.Participant?.ParticipantKind ?? ProjectParticipantKind.Hr;
    }

    public static ProjectWorkItemKind ResolveWorkItemKind(string? objectSubtype)
    {
        var metadata = NormalizeMetadata(ProjectObjectType.WorkItem, objectSubtype, new ProjectObjectMetadataEnvelope(), string.Empty, null);
        return metadata.WorkItem?.WorkItemKind ?? ProjectWorkItemKind.Task;
    }

    public static ProjectRepositoryMode ResolveRepositoryMode(string? objectSubtype)
    {
        var metadata = NormalizeMetadata(ProjectObjectType.Repository, objectSubtype, new ProjectObjectMetadataEnvelope(), string.Empty, null);
        return metadata.Repository?.RepositoryMode ?? ProjectRepositoryMode.LocalRepository;
    }

    public static ProjectFileSubtype ResolveFileSubtype(ProjectObjectType objectType, string? objectSubtype)
    {
        var metadata = NormalizeMetadata(objectType, objectSubtype, new ProjectObjectMetadataEnvelope(), string.Empty, null);
        return metadata.File?.FileSubtype ?? ProjectFileSubtype.Unknown;
    }

    public static ProjectScriptKind ResolveScriptKind(string? objectSubtype)
    {
        var metadata = NormalizeMetadata(ProjectObjectType.Script, objectSubtype, new ProjectObjectMetadataEnvelope(), string.Empty, null);
        return metadata.Script?.ScriptKind ?? ProjectScriptKind.Console;
    }

    public static ProjectEnvironmentKind ResolveEnvironmentKind(string? objectSubtype)
    {
        var metadata = NormalizeMetadata(ProjectObjectType.Environment, objectSubtype, new ProjectObjectMetadataEnvelope(), string.Empty, null);
        return metadata.Environment?.EnvironmentKind ?? ProjectEnvironmentKind.DotNetRuntime;
    }

    public static ProjectInfrastructureKind ResolveInfrastructureKind(string? objectSubtype)
    {
        var metadata = NormalizeMetadata(ProjectObjectType.Infrastructure, objectSubtype, new ProjectObjectMetadataEnvelope(), string.Empty, null);
        return metadata.Infrastructure?.InfrastructureKind ?? ProjectInfrastructureKind.RemoteServer;
    }

    public static ProjectObjectMetadataEnvelope NormalizeMetadata(
        ProjectObjectType objectType,
        string? objectSubtype,
        ProjectObjectMetadataEnvelope metadata,
        string? notes,
        SavedMediaDescriptor? media)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var descriptor = ResolveDescriptor(objectType, objectSubtype);
        var scoped = ScopeMetadata(metadata, descriptor.Family);
        descriptor.NormalizeMetadata?.Invoke(
            scoped,
            new ProjectNodeMetadataNormalizationContext(
                objectType,
                NormalizeSubtype(objectSubtype),
                notes?.Trim() ?? string.Empty,
                media));
        return scoped;
    }

    private static string NormalizeSubtype(string? objectSubtype)
        => objectSubtype?.Trim() ?? string.Empty;

    private static ProjectObjectMetadataEnvelope ScopeMetadata(ProjectObjectMetadataEnvelope metadata, ProjectNodeKindFamily family)
        => new()
        {
            WorkflowProjectWrite = metadata.WorkflowProjectWrite,
            ProjectBlock = family == ProjectNodeKindFamily.ProjectBlock ? metadata.ProjectBlock : null,
            Meeting = family == ProjectNodeKindFamily.Meeting ? metadata.Meeting : null,
            Recording = family == ProjectNodeKindFamily.Recording ? metadata.Recording : null,
            Transcript = family == ProjectNodeKindFamily.Transcript ? metadata.Transcript : null,
            Participant = family == ProjectNodeKindFamily.Participant ? metadata.Participant : null,
            WorkItem = family == ProjectNodeKindFamily.WorkItem ? metadata.WorkItem : null,
            Repository = family == ProjectNodeKindFamily.Repository ? metadata.Repository : null,
            File = family == ProjectNodeKindFamily.File ? metadata.File : null,
            Script = family == ProjectNodeKindFamily.Script ? metadata.Script : null,
            Environment = family == ProjectNodeKindFamily.Environment ? metadata.Environment : null,
            Infrastructure = family == ProjectNodeKindFamily.Infrastructure ? metadata.Infrastructure : null,
            SecretReference = family == ProjectNodeKindFamily.SecretReference ? metadata.SecretReference : null,
            Link = family == ProjectNodeKindFamily.Link ? metadata.Link : null,
            Workflow = family == ProjectNodeKindFamily.Workflow ? metadata.Workflow : null
        };

    private static IReadOnlyDictionary<(ProjectObjectType ObjectType, string ObjectSubtype), ProjectNodeKindDescriptor> BuildDescriptors()
    {
        List<ProjectNodeKindDescriptor> descriptors =
        [
            Simple(ProjectObjectType.ProjectRoot, "Project", Profile("hex", "#0f172a", "PR", "Project", ProjectObjectPaletteKeys.Primary)),
            Simple(ProjectObjectType.Phase, "Phase", Profile("pill", "#2563eb", "PH", "Phase", ProjectObjectPaletteKeys.Info)),
            Simple(ProjectObjectType.Milestone, "Milestone", Profile("diamond", "#d97706", "MS", "Milestone", ProjectObjectPaletteKeys.Warning)),
            Simple(ProjectObjectType.Note, "Note", Profile("rect", "#d97706", "NT", "Note", ProjectObjectPaletteKeys.Warning)),
            Simple(ProjectObjectType.Decision, "Decision", Profile("hex", "#ea580c", "DC", "Decision", ProjectObjectPaletteKeys.Warning), allowNotePromotion: true),
            Simple(ProjectObjectType.PromptFlow, "Prompt flow", Profile("hex", "#0f766e", "PF", "Prompt", ProjectObjectPaletteKeys.Success)),
            Simple(ProjectObjectType.PromptSession, "Prompt session", Profile("hex", "#0f766e", "PF", "Prompt", ProjectObjectPaletteKeys.Success)),
            Simple(ProjectObjectType.PromptStep, "Prompt step", Profile("pill", "#14b8a6", "ST", "Step", ProjectObjectPaletteKeys.Success)),
            Simple(ProjectObjectType.ProcessDefinition, "Process definition", Profile("hex", "#0f766e", "PR", "Process", ProjectObjectPaletteKeys.Success)),
            Simple(
                ProjectObjectType.ProcessRun,
                "Process run",
                Profile("diamond", "#0284c7", "RN", "Run", ProjectObjectPaletteKeys.Info),
                buildVisualProfile: status => status switch
                {
                    var candidate when candidate.Contains("Active", StringComparison.OrdinalIgnoreCase) =>
                        Profile("diamond", "#0f766e", "RN", "Run", ProjectObjectPaletteKeys.Success),
                    var candidate when candidate.Contains("Completed", StringComparison.OrdinalIgnoreCase) =>
                        Profile("diamond", "#16a34a", "RN", "Run", ProjectObjectPaletteKeys.Success),
                    var candidate when candidate.Contains("Blocked", StringComparison.OrdinalIgnoreCase) ||
                                       candidate.Contains("Failed", StringComparison.OrdinalIgnoreCase) =>
                        Profile("diamond", "#dc2626", "RN", "Run", ProjectObjectPaletteKeys.Danger),
                    _ =>
                        Profile("diamond", "#0284c7", "RN", "Run", ProjectObjectPaletteKeys.Info)
                }),
            Workflow(
                ProjectObjectType.WorkflowDefinition,
                "Workflow",
                Profile("hex", "#7c3aed", "WF", "Workflow", ProjectObjectPaletteKeys.Secondary),
                status => status.Contains("Active", StringComparison.OrdinalIgnoreCase)
                    ? Profile("hex", "#0f766e", "WF", "Workflow", ProjectObjectPaletteKeys.Success)
                    : Profile("hex", "#7c3aed", "WF", "Workflow", ProjectObjectPaletteKeys.Secondary)),
            Workflow(
                ProjectObjectType.WorkflowRun,
                "Workflow run",
                Profile("diamond", "#0284c7", "WR", "Run", ProjectObjectPaletteKeys.Info),
                status => status switch
                {
                    var candidate when candidate.Contains("Completed", StringComparison.OrdinalIgnoreCase) =>
                        Profile("diamond", "#16a34a", "WR", "Run", ProjectObjectPaletteKeys.Success),
                    var candidate when candidate.Contains("Failed", StringComparison.OrdinalIgnoreCase) ||
                                       candidate.Contains("Cancelled", StringComparison.OrdinalIgnoreCase) =>
                        Profile("diamond", "#dc2626", "WR", "Run", ProjectObjectPaletteKeys.Danger),
                    var candidate when candidate.Contains("Running", StringComparison.OrdinalIgnoreCase) ||
                                       candidate.Contains("Waiting", StringComparison.OrdinalIgnoreCase) =>
                        Profile("diamond", "#0f766e", "WR", "Run", ProjectObjectPaletteKeys.Success),
                    _ =>
                        Profile("diamond", "#0284c7", "WR", "Run", ProjectObjectPaletteKeys.Info)
                }),
            Simple(
                ProjectObjectType.ValidationRun,
                "Validation",
                Profile("diamond", "#dc2626", "VL", "Validate", ProjectObjectPaletteKeys.Danger),
                buildVisualProfile: status => status.Contains("Approved", StringComparison.OrdinalIgnoreCase)
                    ? Profile("diamond", "#16a34a", "VL", "Validate", ProjectObjectPaletteKeys.Success)
                    : Profile("diamond", "#dc2626", "VL", "Validate", ProjectObjectPaletteKeys.Danger)),
            Simple(ProjectObjectType.TestPlan, "Test plan", Profile("diamond", "#7c3aed", "TS", "Test", ProjectObjectPaletteKeys.Secondary)),
            Simple(ProjectObjectType.TestEvidence, "Test evidence", Profile("diamond", "#7c3aed", "TS", "Test", ProjectObjectPaletteKeys.Secondary)),
            SecretReference(ProjectObjectType.SecretReference, "Secret", Profile("shield", "#be123c", "SC", "Secret", ProjectObjectPaletteKeys.Danger)),

            ProjectBlock("feature", "Feature block", Profile("hex", "#2563eb", "FB", "Feature", ProjectObjectPaletteKeys.Info)),
            ProjectBlock("architecture", "Architecture block", Profile("hex", "#4f46e5", "AR", "Architecture", ProjectObjectPaletteKeys.Secondary)),
            ProjectBlock("implementation", "Implementation block", Profile("hex", "#0f766e", "IM", "Implementation", ProjectObjectPaletteKeys.Success)),
            ProjectBlock("revision", "Revision block", Profile("hex", "#f97316", "RB", "Revision", ProjectObjectPaletteKeys.Warning)),
            ProjectBlock("testing", "Testing block", Profile("hex", "#7c3aed", "TB", "Testing", ProjectObjectPaletteKeys.Secondary)),
            ProjectBlock("prompting", "Prompting block", Profile("hex", "#0f766e", "PB", "Prompting", ProjectObjectPaletteKeys.Success)),
            ProjectBlock("research", "Research block", Profile("hex", "#0891b2", "RS", "Research", ProjectObjectPaletteKeys.Info)),
            ProjectBlock("financial", "Financial block", Profile("hex", "#16a34a", "FN", "Financial", ProjectObjectPaletteKeys.Success)),
            ProjectBlock("marketing", "Marketing block", Profile("hex", "#db2777", "MK", "Marketing", ProjectObjectPaletteKeys.Danger)),
            ProjectBlock("operations", "Operations block", Profile("hex", "#475569", "OP", "Operations", ProjectObjectPaletteKeys.Neutral)),
            ProjectBlock("delivery", "Delivery block", Profile("hex", "#d97706", "DL", "Delivery", ProjectObjectPaletteKeys.Warning)),
            ProjectBlock("risk", "Risk block", Profile("hex", "#dc2626", "RK", "Risk", ProjectObjectPaletteKeys.Danger)),
            ProjectBlock("compliance", "Compliance block", Profile("hex", "#7c2d12", "CP", "Compliance", ProjectObjectPaletteKeys.Warning)),
            ProjectBlock("support", "Support block", Profile("hex", "#0284c7", "SP", "Support", ProjectObjectPaletteKeys.Info)),
            ProjectBlock("deployment", "Deployment block", Profile("hex", "#2563eb", "DP", "Deployment", ProjectObjectPaletteKeys.Info)),
            ProjectBlock("repos", "Repos block", Profile("hex", "#0284c7", "RP", "Repos", ProjectObjectPaletteKeys.Info)),
            ProjectBlock("dockers", "Dockers block", Profile("hex", "#2563eb", "DK", "Dockers", ProjectObjectPaletteKeys.Info)),
            ProjectBlock("task-flow", "Task flow block", Profile("hex", "#2563eb", "TF", "Task flow", ProjectObjectPaletteKeys.Info)),
            ProjectBlock("backlog", "Backlog block", Profile("hex", "#7c3aed", "BG", "Backlog", ProjectObjectPaletteKeys.Secondary)),
            ProjectBlock("server", "Server block", Profile("hex", "#b91c1c", "SV", "Server", ProjectObjectPaletteKeys.Danger)),
            ProjectBlock("computer", "Computer block", Profile("hex", "#334155", "PC", "Computer", ProjectObjectPaletteKeys.Neutral)),
            ProjectBlock("router", "Router block", Profile("hex", "#2563eb", "RT", "Router", ProjectObjectPaletteKeys.Info)),
            ProjectBlock("wifi", "WiFi block", Profile("hex", "#0ea5e9", "WF", "WiFi", ProjectObjectPaletteKeys.Info)),
            new ProjectNodeKindDescriptor(
                ProjectObjectType.ProjectBlock,
                string.Empty,
                ProjectNodeKindFamily.ProjectBlock,
                "Project block",
                "core",
                true,
                true,
                false,
                _ => Profile("hex", "#334155", "BL", "Block", ProjectObjectPaletteKeys.Primary)),

            Meeting("online", "Online meeting", Profile("diamond", "#0ea5e9", "ME", "Meeting", ProjectObjectPaletteKeys.Info)),
            Meeting("onsite", "Onsite meeting", Profile("diamond", "#d97706", "ME", "Onsite", ProjectObjectPaletteKeys.Warning)),
            new ProjectNodeKindDescriptor(
                ProjectObjectType.Meeting,
                string.Empty,
                ProjectNodeKindFamily.Meeting,
                "Meeting",
                "core",
                true,
                false,
                false,
                _ => Profile("diamond", "#0ea5e9", "ME", "Meeting", ProjectObjectPaletteKeys.Info),
                (metadata, _) => metadata.Meeting ??= new ProjectMeetingMetadata(),
                AllowedAssignmentRoles: MeetingAssignmentRoles,
                PreferredRole: ProjectPartyAssignmentRole.MeetingParticipant),

            Recording(ProjectObjectType.Recording, string.Empty, "Recording", Profile("pill", "#8b5cf6", "RC", "Recording", ProjectObjectPaletteKeys.Secondary)),
            Recording(ProjectObjectType.Transcript, string.Empty, "Transcript", Profile("rect", "#14b8a6", "TR", "Transcript", ProjectObjectPaletteKeys.Success), ProjectNodeKindFamily.Transcript, (metadata, context) =>
            {
                metadata.Transcript ??= new ProjectTranscriptMetadata();
                if (string.IsNullOrWhiteSpace(metadata.Transcript.TranscriptText) && !string.IsNullOrWhiteSpace(context.Notes))
                {
                    metadata.Transcript.TranscriptText = context.Notes;
                }
            }),

            Participant("hr", "HR", Profile("hex", "#38bdf8", "HR", "HR", ProjectObjectPaletteKeys.Info), ProjectParticipantKind.Hr),
            Participant("team-block", "Team block", Profile("hex", "#2563eb", "TB", "Team", ProjectObjectPaletteKeys.Info), ProjectParticipantKind.TeamBlock),
            Participant("team-section", "Team section", Profile("hex", "#1d4ed8", "TS", "Section", ProjectObjectPaletteKeys.Info), ProjectParticipantKind.TeamSection),
            Participant("freelancer", "Freelancer", Profile("hex", "#a855f7", "FR", "Freelancer", ProjectObjectPaletteKeys.Secondary), ProjectParticipantKind.Freelancer),
            Participant("partner", "Partner", Profile("hex", "#16a34a", "PA", "Partner", ProjectObjectPaletteKeys.Success), ProjectParticipantKind.Partner),
            Participant("ai-agent", "AI agent", Profile("hex", "#0f766e", "AI", "AI", ProjectObjectPaletteKeys.Success), ProjectParticipantKind.AiAgent),
            new ProjectNodeKindDescriptor(
                ProjectObjectType.Participant,
                string.Empty,
                ProjectNodeKindFamily.Participant,
                "Participant",
                "core",
                true,
                false,
                false,
                _ => Profile("hex", "#475569", "PT", "Participant", ProjectObjectPaletteKeys.Primary),
                (metadata, _) =>
                {
                    metadata.Participant ??= new ProjectParticipantMetadata();
                    metadata.Participant.ParticipantKind = ProjectParticipantKind.Hr;
                },
                AllowedAssignmentRoles: ParticipantReplacementRoles,
                ReplacementRoles: ParticipantReplacementRoles,
                PreferredRole: ProjectPartyAssignmentRole.TeamMember),

            WorkItem("task", "Task", Profile("pill", "#d97706", "TK", "Task", ProjectObjectPaletteKeys.Warning), ProjectWorkItemKind.Task, true),
            WorkItem("issue", "Issue", Profile("pill", "#dc2626", "IS", "Issue", ProjectObjectPaletteKeys.Danger), ProjectWorkItemKind.Issue, true),
            WorkItem("revision", "Revision", Profile("pill", "#8b5cf6", "RV", "Revision", ProjectObjectPaletteKeys.Secondary), ProjectWorkItemKind.Revision, true),
            WorkItem("feedback", "Feedback", Profile("pill", "#0284c7", "FB", "Feedback", ProjectObjectPaletteKeys.Info), ProjectWorkItemKind.Feedback, true),
            WorkItem("payment", "Payment", Profile("pill", "#16a34a", "PM", "Payment", ProjectObjectPaletteKeys.Success), ProjectWorkItemKind.Payment, true),
            WorkItem("send", "Send", Profile("pill", "#2563eb", "SD", "Send", ProjectObjectPaletteKeys.Primary), ProjectWorkItemKind.Send, true),
            new ProjectNodeKindDescriptor(
                ProjectObjectType.WorkItem,
                string.Empty,
                ProjectNodeKindFamily.WorkItem,
                "Work item",
                "core",
                true,
                true,
                false,
                _ => Profile("pill", "#475569", "WK", "Work", ProjectObjectPaletteKeys.Neutral),
                (metadata, context) =>
                {
                    metadata.WorkItem ??= new ProjectWorkItemMetadata();
                    metadata.WorkItem.WorkItemKind = ProjectWorkItemKind.Task;
                    if (string.IsNullOrWhiteSpace(metadata.WorkItem.Description) && !string.IsNullOrWhiteSpace(context.Notes))
                    {
                        metadata.WorkItem.Description = context.Notes;
                    }
                },
                AllowedAssignmentRoles: WorkItemAssignmentRoles,
                PreferredRole: ProjectPartyAssignmentRole.WorkItemAssignee),

            Repository("remote", "Remote repo", Profile("rect", "#0f766e", "GH", "Remote", ProjectObjectPaletteKeys.Success), ProjectRepositoryMode.RemoteGitHub),
            Repository("local", "Local repo", Profile("rect", "#0891b2", "RE", "Local", ProjectObjectPaletteKeys.Info), ProjectRepositoryMode.LocalRepository),
            Repository("folder", "Local folder", Profile("rect", "#2563eb", "FD", "Folder", ProjectObjectPaletteKeys.Primary), ProjectRepositoryMode.LocalFolder),
            new ProjectNodeKindDescriptor(
                ProjectObjectType.Repository,
                string.Empty,
                ProjectNodeKindFamily.Repository,
                "Repository",
                "core",
                true,
                false,
                false,
                _ => Profile("rect", "#0891b2", "RE", "Repo", ProjectObjectPaletteKeys.Info),
                (metadata, _) =>
                {
                    metadata.Repository ??= new ProjectRepositoryMetadata();
                    metadata.Repository.RepositoryMode = ProjectRepositoryMode.LocalRepository;
                }),

            File("pdf", "PDF", Profile("rect", "#dc2626", "PDF", "PDF", ProjectObjectPaletteKeys.Danger), ProjectFileSubtype.Pdf),
            File("excel", "Excel", Profile("rect", "#16a34a", "XLS", "Excel", ProjectObjectPaletteKeys.Success), ProjectFileSubtype.Excel),
            File("docx", "Docx", Profile("rect", "#2563eb", "DOC", "Docx", ProjectObjectPaletteKeys.Info), ProjectFileSubtype.Docx),
            File("text", "Text", Profile("rect", "#64748b", "TXT", "Text", ProjectObjectPaletteKeys.Neutral), ProjectFileSubtype.Text),
            File("json", "JSON", Profile("rect", "#64748b", "JS", "JSON", ProjectObjectPaletteKeys.Neutral), ProjectFileSubtype.Json),
            File("markdown", "Markdown", Profile("rect", "#0284c7", "MD", "Markdown", ProjectObjectPaletteKeys.Info), ProjectFileSubtype.Markdown),
            File("mermaid", "Mermaid", Profile("rect", "#7c3aed", "MMD", "Mermaid", ProjectObjectPaletteKeys.Secondary), ProjectFileSubtype.Mermaid),
            File("screenshot", "Screenshot", Profile("rect", "#db2777", "SS", "Screenshot", ProjectObjectPaletteKeys.Danger), ProjectFileSubtype.Screenshot, metadata =>
            {
                metadata.File!.IsClipboardCapture = true;
            }),
            File("log", "Log", Profile("rect", "#475569", "LOG", "Log", ProjectObjectPaletteKeys.Neutral), ProjectFileSubtype.Log),
            File("archive", "Archive", Profile("rect", "#4338ca", "ZIP", "Archive", ProjectObjectPaletteKeys.Primary), ProjectFileSubtype.Archive),
            File("audio", "Audio", Profile("rect", "#0f766e", "AUD", "Audio", ProjectObjectPaletteKeys.Success), ProjectFileSubtype.Audio),
            new ProjectNodeKindDescriptor(
                ProjectObjectType.File,
                string.Empty,
                ProjectNodeKindFamily.File,
                "File",
                "core",
                true,
                false,
                false,
                _ => Profile("rect", "#14b8a6", "FI", "File", ProjectObjectPaletteKeys.Info),
                (metadata, context) =>
                {
                    metadata.File ??= new ProjectFileMetadata();
                    if (metadata.File.FileSubtype == ProjectFileSubtype.Unknown)
                    {
                        metadata.File.FileSubtype = ProjectObjectMetadataSerializer.InferFileSubtype(
                            context.ObjectSubtype,
                            context.Media?.OriginalFileName ?? string.Empty,
                            context.Media?.ContentType ?? string.Empty);
                    }
                }),
            FileObject(ProjectObjectType.ImageAsset, "Image", Profile("rect", "#ec4899", "IM", "Image", ProjectObjectPaletteKeys.Danger), ProjectFileSubtype.Image),
            FileObject(ProjectObjectType.VideoAsset, "Video", Profile("rect", "#7c3aed", "VD", "Video", ProjectObjectPaletteKeys.Secondary), ProjectFileSubtype.Video),

            Script("powershell", "PowerShell", Profile("diamond", "#2563eb", "PS", "PowerShell", ProjectObjectPaletteKeys.Info), ProjectScriptKind.PowerShell),
            Script("console", "Console script", Profile("diamond", "#0f766e", "CS", "Console", ProjectObjectPaletteKeys.Success), ProjectScriptKind.Console),
            Script("ef-migration", "EF migration", Profile("diamond", "#d97706", "EF", "Migration", ProjectObjectPaletteKeys.Warning), ProjectScriptKind.EfMigration),
            Script("tailwind-watch", "Tailwind watch", Profile("diamond", "#0ea5e9", "TW", "Tailwind", ProjectObjectPaletteKeys.Info), ProjectScriptKind.TailwindWatch),
            new ProjectNodeKindDescriptor(
                ProjectObjectType.Script,
                string.Empty,
                ProjectNodeKindFamily.Script,
                "Script",
                "core",
                true,
                false,
                false,
                _ => Profile("diamond", "#475569", "SC", "Script", ProjectObjectPaletteKeys.Neutral),
                (metadata, _) =>
                {
                    metadata.Script ??= new ProjectScriptMetadata();
                    metadata.Script.ScriptKind = ProjectScriptKind.Console;
                }),

            Environment("python", "Python env", Profile("hex", "#16a34a", "PY", "Python", ProjectObjectPaletteKeys.Success), ProjectEnvironmentKind.PythonEnvironment),
            Environment("dotnet-runtime", ".NET runtime", Profile("hex", "#2563eb", ".NET", "Runtime", ProjectObjectPaletteKeys.Info), ProjectEnvironmentKind.DotNetRuntime),
            Environment("dotnet-watch", "dotnet watch", Profile("hex", "#0ea5e9", "DW", "Watch", ProjectObjectPaletteKeys.Info), ProjectEnvironmentKind.DotNetWatch),
            Environment("dotnet-release", "Release run", Profile("hex", "#d97706", "REL", "Release", ProjectObjectPaletteKeys.Warning), ProjectEnvironmentKind.DotNetRelease),
            new ProjectNodeKindDescriptor(
                ProjectObjectType.Environment,
                string.Empty,
                ProjectNodeKindFamily.Environment,
                "Environment",
                "core",
                true,
                false,
                false,
                _ => Profile("hex", "#475569", "ENV", "Environment", ProjectObjectPaletteKeys.Neutral),
                (metadata, _) =>
                {
                    metadata.Environment ??= new ProjectEnvironmentMetadata();
                    metadata.Environment.EnvironmentKind = ProjectEnvironmentKind.DotNetRuntime;
                }),

            Infrastructure("remote-server", "Remote server", Profile("hex", "#b91c1c", "SV", "Server", ProjectObjectPaletteKeys.Danger), ProjectInfrastructureKind.RemoteServer),
            Infrastructure("domain", "Domain", Profile("hex", "#0284c7", "DNS", "Domain", ProjectObjectPaletteKeys.Info), ProjectInfrastructureKind.Domain),
            Infrastructure("dns-record", "DNS record", Profile("hex", "#0ea5e9", "DNS", "DNS", ProjectObjectPaletteKeys.Info), ProjectInfrastructureKind.DnsRecord),
            Infrastructure("docker-mode", "Docker", Profile("hex", "#2563eb", "DK", "Docker", ProjectObjectPaletteKeys.Info), ProjectInfrastructureKind.DockerMode),
            Infrastructure("database", "Database", Profile("hex", "#7c3aed", "DB", "Database", ProjectObjectPaletteKeys.Secondary), ProjectInfrastructureKind.Database),
            Infrastructure("deployment-folder", "Deployment folder", Profile("hex", "#2563eb", "FD", "Folder", ProjectObjectPaletteKeys.Info), ProjectInfrastructureKind.DeploymentFolder),
            Infrastructure("storage-system", "Storage", Profile("hex", "#0f766e", "ST", "Storage", ProjectObjectPaletteKeys.Success), ProjectInfrastructureKind.StorageSystem),
            Infrastructure("key-reference", "Key reference", Profile("hex", "#be123c", "KEY", "Key", ProjectObjectPaletteKeys.Danger), ProjectInfrastructureKind.KeyReference),
            Infrastructure("ai-link", "AI link", Profile("hex", "#0f766e", "AI", "AI", ProjectObjectPaletteKeys.Success), ProjectInfrastructureKind.AiLink),
            new ProjectNodeKindDescriptor(
                ProjectObjectType.Infrastructure,
                string.Empty,
                ProjectNodeKindFamily.Infrastructure,
                "Infrastructure",
                "core",
                true,
                false,
                false,
                _ => Profile("hex", "#475569", "INF", "Infrastructure", ProjectObjectPaletteKeys.Neutral),
                (metadata, _) =>
                {
                    metadata.Infrastructure ??= new ProjectInfrastructureMetadata();
                    metadata.Infrastructure.InfrastructureKind = ProjectInfrastructureKind.RemoteServer;
                }),

            new ProjectNodeKindDescriptor(
                ProjectObjectType.Link,
                string.Empty,
                ProjectNodeKindFamily.Link,
                "Link",
                "core",
                false,
                false,
                false,
                _ => Profile("circle", "#38bdf8", "LN", "Link", ProjectObjectPaletteKeys.Info),
                (metadata, _) => metadata.Link ??= new ProjectLinkMetadata()),
            Simple(ProjectObjectType.Connector, "Connector", Profile("circle", "#8b5cf6", "CN", "Connector", ProjectObjectPaletteKeys.Secondary))
        ];

        return descriptors.ToDictionary(
            item => (item.ObjectType, item.ObjectSubtype),
            item => item);
    }

    private static ProjectNodeKindDescriptor Simple(
        ProjectObjectType objectType,
        string label,
        ProjectObjectVisualProfile visualProfile,
        bool allowNotePromotion = false,
        Func<string, ProjectObjectVisualProfile>? buildVisualProfile = null)
        => new(
            objectType,
            string.Empty,
            ProjectNodeKindFamily.None,
            label,
            "core",
            false,
            allowNotePromotion,
            false,
            buildVisualProfile ?? (_ => visualProfile));

    private static ProjectNodeKindDescriptor Workflow(
        ProjectObjectType objectType,
        string label,
        ProjectObjectVisualProfile visualProfile,
        Func<string, ProjectObjectVisualProfile>? buildVisualProfile = null)
        => new(
            objectType,
            string.Empty,
            ProjectNodeKindFamily.Workflow,
            label,
            "agent-framework",
            false,
            false,
            false,
            buildVisualProfile ?? (_ => visualProfile),
            (metadata, _) => metadata.Workflow ??= new ProjectWorkflowNodeMetadata());

    private static ProjectNodeKindDescriptor SecretReference(
        ProjectObjectType objectType,
        string label,
        ProjectObjectVisualProfile visualProfile)
        => new(
            objectType,
            string.Empty,
            ProjectNodeKindFamily.SecretReference,
            label,
            "security",
            false,
            false,
            false,
            _ => visualProfile,
            (metadata, _) => metadata.SecretReference ??= new ProjectSecretReferenceMetadata());

    private static ProjectNodeKindDescriptor ProjectBlock(string objectSubtype, string label, ProjectObjectVisualProfile visualProfile)
        => new(
            ProjectObjectType.ProjectBlock,
            objectSubtype,
            ProjectNodeKindFamily.ProjectBlock,
            label,
            "core",
            true,
            true,
            true,
            _ => visualProfile);

    private static ProjectNodeKindDescriptor Meeting(string objectSubtype, string label, ProjectObjectVisualProfile visualProfile)
        => new(
            ProjectObjectType.Meeting,
            objectSubtype,
            ProjectNodeKindFamily.Meeting,
            label,
            "core",
            true,
            false,
            false,
            _ => visualProfile,
            (metadata, _) => metadata.Meeting ??= new ProjectMeetingMetadata(),
            AllowedAssignmentRoles: MeetingAssignmentRoles,
            PreferredRole: ProjectPartyAssignmentRole.MeetingParticipant);

    private static ProjectNodeKindDescriptor Recording(
        ProjectObjectType objectType,
        string objectSubtype,
        string label,
        ProjectObjectVisualProfile visualProfile,
        ProjectNodeKindFamily family = ProjectNodeKindFamily.Recording,
        Action<ProjectObjectMetadataEnvelope, ProjectNodeMetadataNormalizationContext>? normalizeMetadata = null)
        => new(
            objectType,
            objectSubtype,
            family,
            label,
            "core",
            false,
            false,
            false,
            _ => visualProfile,
            normalizeMetadata ?? ((metadata, _) =>
            {
                if (objectType == ProjectObjectType.Recording)
                {
                    metadata.Recording ??= new ProjectRecordingMetadata();
                }
            }));

    private static ProjectNodeKindDescriptor Participant(
        string objectSubtype,
        string label,
        ProjectObjectVisualProfile visualProfile,
        ProjectParticipantKind kind)
        => new(
            ProjectObjectType.Participant,
            objectSubtype,
            ProjectNodeKindFamily.Participant,
            label,
            "core",
            true,
            false,
            false,
            _ => visualProfile,
            (metadata, _) =>
            {
                metadata.Participant ??= new ProjectParticipantMetadata();
                metadata.Participant.ParticipantKind = kind;
            },
            AllowedAssignmentRoles:
            [
                ResolveParticipantAssignmentRole(kind)
            ],
            ReplacementRoles: ParticipantReplacementRoles,
            PreferredRole: ResolveParticipantAssignmentRole(kind));

    private static ProjectNodeKindDescriptor WorkItem(
        string objectSubtype,
        string label,
        ProjectObjectVisualProfile visualProfile,
        ProjectWorkItemKind kind,
        bool allowNotePromotion)
        => new(
            ProjectObjectType.WorkItem,
            objectSubtype,
            ProjectNodeKindFamily.WorkItem,
            label,
            "core",
            true,
            allowNotePromotion,
            false,
            _ => visualProfile,
            (metadata, context) =>
            {
                metadata.WorkItem ??= new ProjectWorkItemMetadata();
                metadata.WorkItem.WorkItemKind = kind;
                if (string.IsNullOrWhiteSpace(metadata.WorkItem.Description) && !string.IsNullOrWhiteSpace(context.Notes))
                {
                    metadata.WorkItem.Description = context.Notes;
                }
            },
            AllowedAssignmentRoles: WorkItemAssignmentRoles,
            PreferredRole: ProjectPartyAssignmentRole.WorkItemAssignee);

    private static ProjectNodeKindDescriptor Repository(
        string objectSubtype,
        string label,
        ProjectObjectVisualProfile visualProfile,
        ProjectRepositoryMode mode)
        => new(
            ProjectObjectType.Repository,
            objectSubtype,
            ProjectNodeKindFamily.Repository,
            label,
            "core",
            true,
            false,
            false,
            _ => visualProfile,
            (metadata, _) =>
            {
                metadata.Repository ??= new ProjectRepositoryMetadata();
                metadata.Repository.RepositoryMode = mode;
            });

    private static ProjectNodeKindDescriptor File(
        string objectSubtype,
        string label,
        ProjectObjectVisualProfile visualProfile,
        ProjectFileSubtype fileSubtype,
        Action<ProjectObjectMetadataEnvelope>? afterNormalize = null)
        => new(
            ProjectObjectType.File,
            objectSubtype,
            ProjectNodeKindFamily.File,
            label,
            "core",
            true,
            false,
            false,
            _ => visualProfile,
            (metadata, context) =>
            {
                metadata.File ??= new ProjectFileMetadata();
                metadata.File.FileSubtype = fileSubtype;
                afterNormalize?.Invoke(metadata);
                if (fileSubtype == ProjectFileSubtype.Mermaid && !string.IsNullOrWhiteSpace(context.Notes))
                {
                    metadata.File.MermaidDiagramKind = ProjectObjectMetadataSerializer.DetectMermaidDiagramKind(context.Notes);
                }
            });

    private static ProjectNodeKindDescriptor FileObject(
        ProjectObjectType objectType,
        string label,
        ProjectObjectVisualProfile visualProfile,
        ProjectFileSubtype fileSubtype)
        => new(
            objectType,
            string.Empty,
            ProjectNodeKindFamily.File,
            label,
            "core",
            false,
            false,
            false,
            _ => visualProfile,
            (metadata, _) =>
            {
                metadata.File ??= new ProjectFileMetadata();
                metadata.File.FileSubtype = fileSubtype;
            });

    private static ProjectNodeKindDescriptor Script(
        string objectSubtype,
        string label,
        ProjectObjectVisualProfile visualProfile,
        ProjectScriptKind kind)
        => new(
            ProjectObjectType.Script,
            objectSubtype,
            ProjectNodeKindFamily.Script,
            label,
            "core",
            true,
            false,
            false,
            _ => visualProfile,
            (metadata, _) =>
            {
                metadata.Script ??= new ProjectScriptMetadata();
                metadata.Script.ScriptKind = kind;
            });

    private static ProjectNodeKindDescriptor Environment(
        string objectSubtype,
        string label,
        ProjectObjectVisualProfile visualProfile,
        ProjectEnvironmentKind kind)
        => new(
            ProjectObjectType.Environment,
            objectSubtype,
            ProjectNodeKindFamily.Environment,
            label,
            "core",
            true,
            false,
            false,
            _ => visualProfile,
            (metadata, _) =>
            {
                metadata.Environment ??= new ProjectEnvironmentMetadata();
                metadata.Environment.EnvironmentKind = kind;
            });

    private static ProjectNodeKindDescriptor Infrastructure(
        string objectSubtype,
        string label,
        ProjectObjectVisualProfile visualProfile,
        ProjectInfrastructureKind kind)
        => new(
            ProjectObjectType.Infrastructure,
            objectSubtype,
            ProjectNodeKindFamily.Infrastructure,
            label,
            "core",
            true,
            false,
            false,
            _ => visualProfile,
            (metadata, _) =>
            {
                metadata.Infrastructure ??= new ProjectInfrastructureMetadata();
                metadata.Infrastructure.InfrastructureKind = kind;
            });

    private static ProjectObjectVisualProfile Profile(string shape, string accentColor, string icon, string accentBadge, string paletteKey)
        => new(shape, accentColor, icon, accentBadge, paletteKey);

    private static ProjectPartyAssignmentRole ResolveParticipantAssignmentRole(ProjectParticipantKind kind)
    {
        return kind switch
        {
            ProjectParticipantKind.TeamBlock or ProjectParticipantKind.TeamSection => ProjectPartyAssignmentRole.DeliveryUnit,
            ProjectParticipantKind.Partner => ProjectPartyAssignmentRole.Partner,
            ProjectParticipantKind.AiAgent => ProjectPartyAssignmentRole.AiAgent,
            _ => ProjectPartyAssignmentRole.TeamMember
        };
    }
}
