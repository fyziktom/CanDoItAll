using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Modules.Processes;

public enum ProcessArtifactProjectionSourceKind
{
    Unknown = 0,
    AgentExecutionArtifact = 1,
    WorkspaceWrite = 2,
    ExistingManagedFile = 3,
    AssistantResponse = 4,
    WorkflowRun = 5,
    WorkflowArtifact = 6,
    SubprocessArtifact = 7,
    CompletedDecision = 8,
    ProcessMock = 9,
    ProviderNativeBrowser = 10,
    Manual = 11
}

public sealed class ProcessArtifactProjectionLineage
{
    public ProcessArtifactProjectionSourceKind SourceKind { get; set; }

    public Guid? SourceExecutionRunId { get; set; }

    public Guid? RecoveryExecutionRunId { get; set; }

    public Guid? RecoveredForExecutionRunId { get; set; }

    public Guid? ProjectedExecutionRunId { get; set; }

    public Guid? WorkflowRunId { get; set; }

    public Guid? WorkflowArtifactId { get; set; }

    public Guid? SubprocessRunId { get; set; }

    public Guid? SourceArtifactId { get; set; }

    public Guid? ReworkPacketId { get; set; }

    public string SourceExternalReferenceKey { get; set; } = string.Empty;

    public string ContentHash { get; set; } = string.Empty;
}

internal static class ProcessArtifactProjectionLineageJson
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static string Serialize(ProcessArtifactProjectionLineage? lineage)
    {
        if (lineage is null || IsEmpty(lineage))
        {
            return string.Empty;
        }

        return JsonSerializer.Serialize(lineage, SerializerOptions);
    }

    public static ProcessArtifactProjectionLineage? Deserialize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : JsonSerializer.Deserialize<ProcessArtifactProjectionLineage>(value, SerializerOptions);
    }

    private static bool IsEmpty(ProcessArtifactProjectionLineage lineage)
    {
        return lineage.SourceKind == ProcessArtifactProjectionSourceKind.Unknown &&
               !lineage.SourceExecutionRunId.HasValue &&
               !lineage.RecoveryExecutionRunId.HasValue &&
               !lineage.RecoveredForExecutionRunId.HasValue &&
               !lineage.ProjectedExecutionRunId.HasValue &&
               !lineage.WorkflowRunId.HasValue &&
               !lineage.WorkflowArtifactId.HasValue &&
               !lineage.SubprocessRunId.HasValue &&
               !lineage.SourceArtifactId.HasValue &&
               !lineage.ReworkPacketId.HasValue &&
               string.IsNullOrWhiteSpace(lineage.SourceExternalReferenceKey) &&
               string.IsNullOrWhiteSpace(lineage.ContentHash);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
