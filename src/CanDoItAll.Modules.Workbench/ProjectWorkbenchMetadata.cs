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
}

public sealed class ProjectMeetingMetadata
{
    public ProjectMeetingChannel Channel { get; set; }

    public ProjectRepeatCadence RepeatCadence { get; set; }

    public string Address { get; set; } = string.Empty;

    public string MeetingUrl { get; set; } = string.Empty;

    public string MapUrl { get; set; } = string.Empty;

    public List<Guid> ParticipantIds { get; set; } = [];
}

public sealed class ProjectRecordingMetadata
{
    public string RecordingSource { get; set; } = string.Empty;

    public string StorageReference { get; set; } = string.Empty;

    public Guid? MeetingNodeArtifactId { get; set; }

    public Guid? TranscriptNodeArtifactId { get; set; }

    public int DurationMinutes { get; set; }
}

public sealed class ProjectTranscriptMetadata
{
    public Guid? RecordingNodeArtifactId { get; set; }

    public Guid? LastProviderProfileId { get; set; }

    public string LastProviderName { get; set; } = string.Empty;

    public ProjectLlmActionKind? LastActionKind { get; set; }

    public string TranscriptText { get; set; } = string.Empty;

    public string SummaryText { get; set; } = string.Empty;

    public string MyTasksText { get; set; } = string.Empty;

    public string OthersDeliveriesText { get; set; } = string.Empty;

    public DateTimeOffset? LastGeneratedAtUtc { get; set; }
}

public sealed class ProjectParticipantMetadata
{
    public ProjectParticipantKind ParticipantKind { get; set; }

    public string Role { get; set; } = string.Empty;

    public string Organization { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public Guid? ParentParticipantArtifactId { get; set; }
}

public sealed class ProjectWorkItemMetadata
{
    public ProjectWorkItemKind WorkItemKind { get; set; }

    public Guid? AssigneeParticipantArtifactId { get; set; }

    public Guid? RepositoryResourceId { get; set; }

    public ProjectSendKind? SendKind { get; set; }

    public ProjectMessageChannel DeliveryChannel { get; set; }

    public decimal? Amount { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTimeOffset? DueUtc { get; set; }
}

public sealed class ProjectRepositoryMetadata
{
    public ProjectRepositoryMode RepositoryMode { get; set; }

    public Guid? ResourceId { get; set; }

    public string RepositoryUrl { get; set; } = string.Empty;

    public string LocalPath { get; set; } = string.Empty;

    public string DefaultBranch { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;
}

public sealed class ProjectFileMetadata
{
    public ProjectFileSubtype FileSubtype { get; set; } = ProjectFileSubtype.Unknown;

    public MermaidDiagramKind MermaidDiagramKind { get; set; } = MermaidDiagramKind.Unknown;

    public bool IsClipboardCapture { get; set; }

    public string SourceHint { get; set; } = string.Empty;

    public string ExternalPath { get; set; } = string.Empty;
}

public sealed class ProjectScriptMetadata
{
    public ProjectScriptKind ScriptKind { get; set; }

    public string ScriptPath { get; set; } = string.Empty;

    public string Command { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public string TerminalRoute { get; set; } = string.Empty;
}

public sealed class ProjectEnvironmentMetadata
{
    public ProjectEnvironmentKind EnvironmentKind { get; set; }

    public ProjectPythonProvider? PythonProvider { get; set; }

    public Guid? RepositoryResourceId { get; set; }

    public string EnvironmentName { get; set; } = string.Empty;

    public string ProjectPath { get; set; } = string.Empty;

    public string LaunchProfileName { get; set; } = string.Empty;

    public ProjectRuntimeProtocol RuntimeProtocol { get; set; } = ProjectRuntimeProtocol.Https;

    public string LocalhostUrl { get; set; } = string.Empty;
}

public sealed class ProjectInfrastructureMetadata
{
    public ProjectInfrastructureKind InfrastructureKind { get; set; }

    public string Host { get; set; } = string.Empty;

    public int? Port { get; set; }

    public string Address { get; set; } = string.Empty;

    public string ProviderName { get; set; } = string.Empty;

    public string ProviderUrl { get; set; } = string.Empty;

    public string LoginUrl { get; set; } = string.Empty;

    public string AccountName { get; set; } = string.Empty;

    public decimal? CpuCores { get; set; }

    public decimal? MemoryGb { get; set; }

    public decimal? StorageGb { get; set; }

    public decimal? MonthlyPrice { get; set; }

    public Guid? SecretReferenceArtifactId { get; set; }

    public string DomainName { get; set; } = string.Empty;

    public string OwnerName { get; set; } = string.Empty;

    public string DnsRecordType { get; set; } = string.Empty;

    public string DnsRecordValue { get; set; } = string.Empty;

    public string DockerMode { get; set; } = string.Empty;

    public string ProxyProvider { get; set; } = string.Empty;

    public string DatabaseType { get; set; } = string.Empty;

    public string ConnectionReference { get; set; } = string.Empty;

    public string FolderPath { get; set; } = string.Empty;

    public ProjectAiReferenceKind? AiReferenceKind { get; set; }

    public string AiReferenceUrl { get; set; } = string.Empty;
}

public sealed class ProjectLinkMetadata
{
    public string Url { get; set; } = string.Empty;

    public ProjectMessageChannel Channel { get; set; }

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

        switch (objectType)
        {
            case ProjectObjectType.Meeting when metadata.Meeting is null:
            case ProjectObjectType.Recording when metadata.Recording is null:
            case ProjectObjectType.Transcript when metadata.Transcript is null:
            case ProjectObjectType.Participant when metadata.Participant is null:
            case ProjectObjectType.WorkItem when metadata.WorkItem is null:
            case ProjectObjectType.Repository when metadata.Repository is null:
            case ProjectObjectType.File when metadata.File is null:
            case ProjectObjectType.Script when metadata.Script is null:
            case ProjectObjectType.Environment when metadata.Environment is null:
            case ProjectObjectType.Infrastructure when metadata.Infrastructure is null:
            case ProjectObjectType.Link when metadata.Link is null:
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
}
