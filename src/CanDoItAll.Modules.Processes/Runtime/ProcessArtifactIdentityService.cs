using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessArtifactIdentityService
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static string SerializeProjectionLineage(ProcessArtifactProjectionLineage? lineage)
    {
        if (lineage is null || IsEmpty(lineage))
        {
            return string.Empty;
        }

        var normalized = NormalizeProjectionLineage(lineage);
        return JsonSerializer.Serialize(normalized, SerializerOptions);
    }

    public static string SerializeNormalizedProjectionLineage(ProcessArtifactProjectionLineage? lineage)
    {
        return lineage is null || IsEmpty(lineage)
            ? string.Empty
            : JsonSerializer.Serialize(lineage, SerializerOptions);
    }

    public static ProcessArtifactProjectionLineage? NormalizeProjectionLineage(ProcessArtifactProjectionLineage? lineage)
    {
        if (lineage is null || IsEmpty(lineage))
        {
            return null;
        }

        lineage.ProjectionIdentityHash = ComputeProjectionIdentityHash(lineage);
        return lineage;
    }

    public static string ComputeProjectionIdentityHash(ProcessArtifactProjectionLineage? lineage)
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

    public static string ComputeContentHash(byte[] content)
    {
        var hashBytes = SHA256.HashData(content);
        return "sha256:" + Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static ProcessArtifactProjectionLineage? DeserializeProjectionLineage(string? value)
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
