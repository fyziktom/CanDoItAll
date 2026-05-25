using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;

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

    public string ProjectionIdentityHash { get; set; } = string.Empty;
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

        var normalized = Normalize(lineage);
        return JsonSerializer.Serialize(normalized, SerializerOptions);
    }

    public static ProcessArtifactProjectionLineage? Normalize(ProcessArtifactProjectionLineage? lineage)
    {
        if (lineage is null || IsEmpty(lineage))
        {
            return null;
        }

        lineage.ProjectionIdentityHash = ComputeIdentityHash(lineage);
        return lineage;
    }

    public static string ComputeIdentityHash(ProcessArtifactProjectionLineage? lineage)
    {
        if (lineage is null || IsEmpty(lineage))
        {
            return string.Empty;
        }

        var identity = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["sourceKind"] = lineage.SourceKind.ToString()
        };
        AddGuid(identity, "sourceExecutionRunId", lineage.SourceExecutionRunId);
        AddGuid(identity, "recoveryExecutionRunId", lineage.RecoveryExecutionRunId);
        AddGuid(identity, "recoveredForExecutionRunId", lineage.RecoveredForExecutionRunId);
        AddGuid(identity, "projectedExecutionRunId", lineage.ProjectedExecutionRunId);
        AddGuid(identity, "workflowRunId", lineage.WorkflowRunId);
        AddGuid(identity, "workflowArtifactId", lineage.WorkflowArtifactId);
        AddGuid(identity, "subprocessRunId", lineage.SubprocessRunId);
        AddGuid(identity, "sourceArtifactId", lineage.SourceArtifactId);
        AddGuid(identity, "reworkPacketId", lineage.ReworkPacketId);
        AddText(identity, "sourceExternalReferenceKey", lineage.SourceExternalReferenceKey);
        AddText(identity, "contentHash", lineage.ContentHash);

        var material = string.Join('\n', identity.Select(item => $"{item.Key}={item.Value}"));
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return "sha256:" + Convert.ToHexString(hashBytes).ToLowerInvariant();
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

    private static void AddGuid(
        IDictionary<string, string> identity,
        string key,
        Guid? value)
    {
        if (value.HasValue)
        {
            identity[key] = value.Value.ToString("D");
        }
    }

    private static void AddText(
        IDictionary<string, string> identity,
        string key,
        string? value)
    {
        var normalized = value?.Trim();
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            identity[key] = normalized;
        }
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
