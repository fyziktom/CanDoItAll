using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

public enum ProjectMeetingChannel
{
    Unknown,
    MsTeams,
    GoogleMeet,
    Zoom,
    WhatsApp,
    Telegram
}

public enum ProjectRepeatCadence
{
    None,
    Daily,
    Weekly,
    BiWeekly,
    Monthly
}

public enum ProjectParticipantKind
{
    Hr,
    TeamBlock,
    TeamSection,
    Freelancer,
    Partner,
    AiAgent
}

public enum ProjectWorkItemKind
{
    Task,
    Issue,
    Revision,
    Feedback,
    Payment,
    Send
}

public enum ProjectSendKind
{
    File,
    Offer,
    Email,
    Message,
    Invoice,
    Money
}

public enum ProjectMessageChannel
{
    None,
    Email,
    WhatsApp,
    Telegram,
    Teams,
    Sms
}

public enum ProjectRepositoryMode
{
    ExistingResource,
    RemoteGitHub,
    LocalRepository,
    LocalFolder
}

public enum ProjectScriptKind
{
    PowerShell,
    Console,
    EfMigration,
    TailwindWatch
}

public enum ProjectEnvironmentKind
{
    PythonEnvironment,
    DotNetRuntime,
    DotNetWatch,
    DotNetRelease
}

public enum ProjectPythonProvider
{
    Python,
    Conda
}

public enum ProjectRuntimeProtocol
{
    Http,
    Https,
    Both
}

public enum ProjectInfrastructureKind
{
    RemoteServer,
    Domain,
    DnsRecord,
    DockerMode,
    ProxyProvider,
    Database,
    DeploymentFolder,
    StorageSystem,
    KeyReference,
    AiLink
}

public enum ProjectAiReferenceKind
{
    ChatGptConversation,
    CodexThread,
    LocalLlm
}

public enum ProjectFileSubtype
{
    Unknown,
    Pdf,
    Excel,
    Docx,
    Text,
    Json,
    Markdown,
    Mermaid,
    Screenshot,
    Log,
    Archive,
    Audio,
    Video,
    Image
}

public enum MermaidDiagramKind
{
    Unknown,
    Flowchart,
    SequenceDiagram,
    ClassDiagram,
    StateDiagram,
    ErDiagram,
    UserJourney,
    Gantt,
    Pie,
    Mindmap,
    Timeline,
    C4
}

public enum ProjectLlmActionKind
{
    Summarize,
    FindMyTasks,
    FindOthersDeliveries
}

public sealed class ProjectObjectMetadataEnvelope
{
    public ProjectMeetingMetadata? Meeting { get; set; }

    public ProjectRecordingMetadata? Recording { get; set; }

    public ProjectTranscriptMetadata? Transcript { get; set; }

    public ProjectParticipantMetadata? Participant { get; set; }

    public ProjectWorkItemMetadata? WorkItem { get; set; }

    public ProjectRepositoryMetadata? Repository { get; set; }

    public ProjectFileMetadata? File { get; set; }

    public ProjectScriptMetadata? Script { get; set; }

    public ProjectEnvironmentMetadata? Environment { get; set; }

    public ProjectInfrastructureMetadata? Infrastructure { get; set; }

    public ProjectLinkMetadata? Link { get; set; }

    public ProjectMarkerSetMetadata? MarkerSet { get; set; }
}

public sealed class ProjectMarkerSetMetadata
{
    public List<ProjectNodeMarker> Markers { get; set; } = [];
}

public sealed record ProjectNodeMarker(
    string Icon,
    string Tone,
    string Label);

public sealed class ProjectMeetingMetadata
{
    [ProjectStructurePreviewField("Channel", 10)]
    public ProjectMeetingChannel Channel { get; set; }

    [ProjectStructurePreviewField("Repeat", 20)]
    public ProjectRepeatCadence RepeatCadence { get; set; }

    [ProjectStructurePreviewField("Address", 30)]
    public string Address { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Meeting URL", 40)]
    public string MeetingUrl { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Map URL", 50)]
    public string MapUrl { get; set; } = string.Empty;

    [JsonPropertyName("relatedPartyNames")]
    [ProjectStructurePreviewField("Parties", 60)]
    public string RelatedPartySummary { get; set; } = string.Empty;

    public List<Guid> ParticipantIds { get; set; } = [];
}

public sealed class ProjectRecordingMetadata
{
    [ProjectStructurePreviewField("Source", 10)]
    public string RecordingSource { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Storage", 20)]
    public string StorageReference { get; set; } = string.Empty;

    public Guid? MeetingNodeArtifactId { get; set; }

    public Guid? TranscriptNodeArtifactId { get; set; }

    [ProjectStructurePreviewField("Duration (min)", 30)]
    public int DurationMinutes { get; set; }
}

public sealed class ProjectTranscriptMetadata
{
    public Guid? RecordingNodeArtifactId { get; set; }

    public Guid? LastProviderProfileId { get; set; }

    [ProjectStructurePreviewField("Last provider", 10)]
    public string LastProviderName { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Last action", 20)]
    public ProjectLlmActionKind? LastActionKind { get; set; }

    public string TranscriptText { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Summary", 30)]
    public string SummaryText { get; set; } = string.Empty;

    [ProjectStructurePreviewField("My tasks", 40)]
    public string MyTasksText { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Others to me", 50)]
    public string OthersDeliveriesText { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Generated", 60)]
    public DateTimeOffset? LastGeneratedAtUtc { get; set; }
}

public sealed class ProjectParticipantMetadata
{
    [ProjectStructurePreviewField("Kind", 10)]
    public ProjectParticipantKind ParticipantKind { get; set; }

    [ProjectStructurePreviewField("Role", 20)]
    public string Role { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Organization", 30)]
    public string Organization { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Email", 40)]
    public string Email { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Phone", 50)]
    public string Phone { get; set; } = string.Empty;

    [JsonPropertyName("linkedPartyName")]
    [ProjectStructurePreviewField("Directory party", 60)]
    public string LinkedPartyDisplayName { get; set; } = string.Empty;

    public Guid? ParentParticipantArtifactId { get; set; }
}

public sealed class ProjectWorkItemMetadata
{
    [ProjectStructurePreviewField("Kind", 10)]
    public ProjectWorkItemKind WorkItemKind { get; set; }

    public Guid? AssigneeParticipantArtifactId { get; set; }

    public Guid? RepositoryResourceId { get; set; }

    [ProjectStructurePreviewField("Send kind", 20)]
    public ProjectSendKind? SendKind { get; set; }

    [ProjectStructurePreviewField("Channel", 30)]
    public ProjectMessageChannel DeliveryChannel { get; set; }

    [ProjectStructurePreviewField("Amount", 40)]
    public decimal? Amount { get; set; }

    [ProjectStructurePreviewField("Currency", 50)]
    public string CurrencyCode { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Description", 60)]
    public string Description { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Due", 70)]
    public DateTimeOffset? DueUtc { get; set; }

    [JsonPropertyName("assigneePartyName")]
    [ProjectStructurePreviewField("Assignee party", 80)]
    public string AssigneePartyDisplayName { get; set; } = string.Empty;
}

public sealed class ProjectRepositoryMetadata
{
    [ProjectStructurePreviewField("Mode", 10)]
    public ProjectRepositoryMode RepositoryMode { get; set; }

    public Guid? ResourceId { get; set; }

    [ProjectStructurePreviewField("Repository URL", 20)]
    public string RepositoryUrl { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Local path", 30)]
    public string LocalPath { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Default branch", 40)]
    public string DefaultBranch { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Relative path", 50)]
    public string RelativePath { get; set; } = string.Empty;
}

public sealed class ProjectFileMetadata
{
    public ProjectFileSubtype FileSubtype { get; set; } = ProjectFileSubtype.Unknown;

    [ProjectStructurePreviewField("Diagram", 10)]
    public MermaidDiagramKind MermaidDiagramKind { get; set; } = MermaidDiagramKind.Unknown;

    [ProjectStructurePreviewField("Clipboard capture", 20)]
    public bool IsClipboardCapture { get; set; }

    [ProjectStructurePreviewField("Source hint", 30)]
    public string SourceHint { get; set; } = string.Empty;

    [ProjectStructurePreviewField("External path", 40)]
    public string ExternalPath { get; set; } = string.Empty;
}

public sealed class ProjectScriptMetadata
{
    [ProjectStructurePreviewField("Kind", 10)]
    public ProjectScriptKind ScriptKind { get; set; }

    [ProjectStructurePreviewField("Script path", 20)]
    public string ScriptPath { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Command", 30)]
    public string Command { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Arguments", 40)]
    public string Arguments { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Working directory", 50)]
    public string WorkingDirectory { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Terminal route", 60)]
    public string TerminalRoute { get; set; } = string.Empty;
}

public sealed class ProjectEnvironmentMetadata
{
    [ProjectStructurePreviewField("Kind", 10)]
    public ProjectEnvironmentKind EnvironmentKind { get; set; }

    [ProjectStructurePreviewField("Python provider", 20)]
    public ProjectPythonProvider? PythonProvider { get; set; }

    public Guid? RepositoryResourceId { get; set; }

    [ProjectStructurePreviewField("Environment name", 30)]
    public string EnvironmentName { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Project path", 40)]
    public string ProjectPath { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Launch profile", 50)]
    public string LaunchProfileName { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Runtime protocol", 60)]
    public ProjectRuntimeProtocol RuntimeProtocol { get; set; } = ProjectRuntimeProtocol.Https;

    [ProjectStructurePreviewField("Localhost URL", 70)]
    public string LocalhostUrl { get; set; } = string.Empty;
}

public sealed class ProjectInfrastructureMetadata
{
    [ProjectStructurePreviewField("Kind", 10)]
    public ProjectInfrastructureKind InfrastructureKind { get; set; }

    [ProjectStructurePreviewField("Host", 20)]
    public string Host { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Port", 30)]
    public int? Port { get; set; }

    [ProjectStructurePreviewField("Address", 40)]
    public string Address { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Provider", 50)]
    public string ProviderName { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Provider URL", 60)]
    public string ProviderUrl { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Login URL", 70)]
    public string LoginUrl { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Account", 80)]
    public string AccountName { get; set; } = string.Empty;

    [ProjectStructurePreviewField("CPU cores", 90)]
    public decimal? CpuCores { get; set; }

    [ProjectStructurePreviewField("Memory (GB)", 100)]
    public decimal? MemoryGb { get; set; }

    [ProjectStructurePreviewField("Storage (GB)", 110)]
    public decimal? StorageGb { get; set; }

    [ProjectStructurePreviewField("Monthly price", 120)]
    public decimal? MonthlyPrice { get; set; }

    public Guid? SecretReferenceArtifactId { get; set; }

    [ProjectStructurePreviewField("Domain", 130)]
    public string DomainName { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Owner", 140)]
    public string OwnerName { get; set; } = string.Empty;

    [ProjectStructurePreviewField("DNS record type", 150)]
    public string DnsRecordType { get; set; } = string.Empty;

    [ProjectStructurePreviewField("DNS record value", 160)]
    public string DnsRecordValue { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Docker mode", 170)]
    public string DockerMode { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Proxy provider", 180)]
    public string ProxyProvider { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Database", 190)]
    public string DatabaseType { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Connection reference", 200)]
    public string ConnectionReference { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Folder path", 210)]
    public string FolderPath { get; set; } = string.Empty;

    public Guid? StorageCatalogId { get; set; }

    [ProjectStructurePreviewField("Storage purpose", 220)]
    public string StoragePurpose { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Storage path prefix", 230)]
    public string StoragePathPrefix { get; set; } = string.Empty;

    [ProjectStructurePreviewField("AI reference kind", 240)]
    public ProjectAiReferenceKind? AiReferenceKind { get; set; }

    [ProjectStructurePreviewField("AI reference URL", 250)]
    public string AiReferenceUrl { get; set; } = string.Empty;
}

public sealed class ProjectLinkMetadata
{
    [ProjectStructurePreviewField("URL", 10)]
    public string Url { get; set; } = string.Empty;

    [ProjectStructurePreviewField("Channel", 20)]
    public ProjectMessageChannel Channel { get; set; }

    [ProjectStructurePreviewField("Display hint", 30)]
    public string DisplayHint { get; set; } = string.Empty;
}

public static class ProjectObjectMetadataSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = BuildSerializerOptions();

    public static ProjectObjectMetadataEnvelope Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ProjectObjectMetadataEnvelope();
        }

        try
        {
            return JsonSerializer.Deserialize<ProjectObjectMetadataEnvelope>(json, SerializerOptions)
                ?? new ProjectObjectMetadataEnvelope();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Invalid project object metadata payload.", ex);
        }
    }

    public static string Serialize(ProjectObjectMetadataEnvelope? metadata)
        => JsonSerializer.Serialize(metadata ?? new ProjectObjectMetadataEnvelope(), SerializerOptions);

    public static ProjectNodeMarker? NormalizeMarker(string? icon, string? tone, string? label)
    {
        var normalizedIcon = icon?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedIcon))
        {
            return null;
        }

        var normalizedTone = string.IsNullOrWhiteSpace(tone)
            ? "accent"
            : tone.Trim().ToLowerInvariant();
        var normalizedLabel = string.IsNullOrWhiteSpace(label)
            ? normalizedIcon
            : label.Trim();
        return new ProjectNodeMarker(normalizedIcon, normalizedTone, normalizedLabel);
    }

    public static IReadOnlyList<ProjectNodeMarker> ResolveMarkers(
        ProjectObjectMetadataEnvelope? metadata,
        string? legacyMarkerIcon,
        string? legacyMarkerTone,
        string? legacyMarkerLabel)
    {
        var markers = NormalizeMarkers(metadata?.MarkerSet?.Markers);
        if (markers.Count > 0)
        {
            return markers;
        }

        var legacyMarker = NormalizeMarker(legacyMarkerIcon, legacyMarkerTone, legacyMarkerLabel);
        return legacyMarker is null ? [] : [legacyMarker];
    }

    public static IReadOnlyList<ProjectNodeMarker> NormalizeMarkers(IEnumerable<ProjectNodeMarker>? markers)
    {
        if (markers is null)
        {
            return [];
        }

        var ordered = new List<ProjectNodeMarker>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var marker in markers)
        {
            var normalized = NormalizeMarker(marker?.Icon, marker?.Tone, marker?.Label);
            if (normalized is null)
            {
                continue;
            }

            if (!seen.Add(normalized.Icon))
            {
                continue;
            }

            ordered.Add(normalized);
        }

        return ordered;
    }

    public static void SetMarkers(ProjectObjectMetadataEnvelope metadata, IEnumerable<ProjectNodeMarker>? markers)
    {
        var normalized = NormalizeMarkers(markers);
        metadata.MarkerSet = normalized.Count == 0
            ? null
            : new ProjectMarkerSetMetadata
            {
                Markers = normalized.ToList()
            };
    }

    public static ProjectNodeMarker? ResolvePrimaryMarker(IEnumerable<ProjectNodeMarker>? markers)
    {
        var normalized = NormalizeMarkers(markers);
        return normalized.Count == 0 ? null : normalized[^1];
    }

    public static string ValidateAndSerialize(ProjectObjectType objectType, string objectSubtype, string? metadataJson)
    {
        var metadata = Parse(metadataJson);
        Validate(objectType, objectSubtype, metadata);
        return Serialize(metadata);
    }

    public static void Validate(ProjectObjectType objectType, string objectSubtype, ProjectObjectMetadataEnvelope metadata)
    {
        var familyCount = CountFamilies(metadata);
        if (familyCount > 1)
        {
            throw new InvalidOperationException("Project object metadata must use a single family payload.");
        }

        if (familyCount == 0)
        {
            return;
        }

        var expectedFamily = ProjectNodeKindRegistry.ResolveFamily(objectType, objectSubtype);
        var actualFamily = ResolveMetadataFamily(metadata);
        if (actualFamily != expectedFamily)
        {
            throw new InvalidOperationException($"Metadata payload does not match object type '{objectType}'.");
        }

        if (objectType == ProjectObjectType.Meeting &&
            string.Equals(objectSubtype, "onsite", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(metadata.Meeting?.Address))
        {
            throw new InvalidOperationException("Onsite meetings require an address.");
        }
    }

    public static ProjectFileSubtype InferFileSubtype(string objectSubtype, string fileName, string contentType)
    {
        if (!string.IsNullOrWhiteSpace(objectSubtype) &&
            Enum.TryParse<ProjectFileSubtype>(objectSubtype.Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase), true, out var subtypeFromObject))
        {
            return subtypeFromObject;
        }

        var extension = Path.GetExtension(fileName)?.Trim().ToLowerInvariant();
        if (contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) || extension == ".pdf")
        {
            return ProjectFileSubtype.Pdf;
        }

        if (contentType.Contains("spreadsheet", StringComparison.OrdinalIgnoreCase) || extension is ".xls" or ".xlsx" or ".csv")
        {
            return ProjectFileSubtype.Excel;
        }

        if (contentType.Contains("wordprocessingml", StringComparison.OrdinalIgnoreCase) || extension is ".doc" or ".docx")
        {
            return ProjectFileSubtype.Docx;
        }

        if (extension == ".json")
        {
            return ProjectFileSubtype.Json;
        }

        if (extension == ".md")
        {
            return ProjectFileSubtype.Markdown;
        }

        if (extension is ".mmd" or ".mermaid")
        {
            return ProjectFileSubtype.Mermaid;
        }

        if (extension is ".log")
        {
            return ProjectFileSubtype.Log;
        }

        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectFileSubtype.Image;
        }

        if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectFileSubtype.Video;
        }

        if (contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectFileSubtype.Audio;
        }

        return extension switch
        {
            ".txt" or ".xml" or ".yaml" or ".yml" => ProjectFileSubtype.Text,
            ".zip" or ".rar" or ".7z" => ProjectFileSubtype.Archive,
            _ => ProjectFileSubtype.Unknown
        };
    }

    public static MermaidDiagramKind DetectMermaidDiagramKind(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return MermaidDiagramKind.Unknown;
        }

        if (content.Contains("sequenceDiagram", StringComparison.OrdinalIgnoreCase))
        {
            return MermaidDiagramKind.SequenceDiagram;
        }

        if (content.Contains("classDiagram", StringComparison.OrdinalIgnoreCase))
        {
            return MermaidDiagramKind.ClassDiagram;
        }

        if (content.Contains("stateDiagram", StringComparison.OrdinalIgnoreCase))
        {
            return MermaidDiagramKind.StateDiagram;
        }

        if (content.Contains("erDiagram", StringComparison.OrdinalIgnoreCase))
        {
            return MermaidDiagramKind.ErDiagram;
        }

        if (content.Contains("journey", StringComparison.OrdinalIgnoreCase))
        {
            return MermaidDiagramKind.UserJourney;
        }

        if (content.Contains("gantt", StringComparison.OrdinalIgnoreCase))
        {
            return MermaidDiagramKind.Gantt;
        }

        if (content.Contains("pie", StringComparison.OrdinalIgnoreCase))
        {
            return MermaidDiagramKind.Pie;
        }

        if (content.Contains("mindmap", StringComparison.OrdinalIgnoreCase))
        {
            return MermaidDiagramKind.Mindmap;
        }

        if (content.Contains("timeline", StringComparison.OrdinalIgnoreCase))
        {
            return MermaidDiagramKind.Timeline;
        }

        if (content.Contains("C4Context", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("C4Container", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("C4Component", StringComparison.OrdinalIgnoreCase))
        {
            return MermaidDiagramKind.C4;
        }

        if (content.Contains("graph ", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("flowchart ", StringComparison.OrdinalIgnoreCase))
        {
            return MermaidDiagramKind.Flowchart;
        }

        return MermaidDiagramKind.Unknown;
    }

    private static JsonSerializerOptions BuildSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private static int CountFamilies(ProjectObjectMetadataEnvelope metadata)
    {
        var count = 0;
        if (metadata.Meeting is not null)
        {
            count++;
        }

        if (metadata.Recording is not null)
        {
            count++;
        }

        if (metadata.Transcript is not null)
        {
            count++;
        }

        if (metadata.Participant is not null)
        {
            count++;
        }

        if (metadata.WorkItem is not null)
        {
            count++;
        }

        if (metadata.Repository is not null)
        {
            count++;
        }

        if (metadata.File is not null)
        {
            count++;
        }

        if (metadata.Script is not null)
        {
            count++;
        }

        if (metadata.Environment is not null)
        {
            count++;
        }

        if (metadata.Infrastructure is not null)
        {
            count++;
        }

        if (metadata.Link is not null)
        {
            count++;
        }

        return count;
    }

    private static ProjectNodeKindFamily ResolveMetadataFamily(ProjectObjectMetadataEnvelope metadata)
    {
        if (metadata.Meeting is not null)
        {
            return ProjectNodeKindFamily.Meeting;
        }

        if (metadata.Recording is not null)
        {
            return ProjectNodeKindFamily.Recording;
        }

        if (metadata.Transcript is not null)
        {
            return ProjectNodeKindFamily.Transcript;
        }

        if (metadata.Participant is not null)
        {
            return ProjectNodeKindFamily.Participant;
        }

        if (metadata.WorkItem is not null)
        {
            return ProjectNodeKindFamily.WorkItem;
        }

        if (metadata.Repository is not null)
        {
            return ProjectNodeKindFamily.Repository;
        }

        if (metadata.File is not null)
        {
            return ProjectNodeKindFamily.File;
        }

        if (metadata.Script is not null)
        {
            return ProjectNodeKindFamily.Script;
        }

        if (metadata.Environment is not null)
        {
            return ProjectNodeKindFamily.Environment;
        }

        if (metadata.Infrastructure is not null)
        {
            return ProjectNodeKindFamily.Infrastructure;
        }

        return metadata.Link is not null
            ? ProjectNodeKindFamily.Link
            : ProjectNodeKindFamily.None;
    }
}
