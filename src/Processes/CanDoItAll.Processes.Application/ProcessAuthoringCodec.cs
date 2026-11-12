using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Processes.Application;

public static class ProcessAuthoringCodec {
    public static string Write(ProcessAuthoringContent content) {
        Validate(content);
        return JsonSerializer.Serialize(content, ProcessAuthoringJsonContext.Default.ProcessAuthoringContent);
    }

    public static ProcessAuthoringContent Read(string json) {
        var content = JsonSerializer.Deserialize(json, ProcessAuthoringJsonContext.Default.ProcessAuthoringContent)
            ?? throw new InvalidOperationException("The authored definition has no content.");
        Validate(content);
        foreach (var step in content.Definition.Steps) {
            step.ResolvedExecutionGuidance = content.Guidance.TryGetValue(step.Key, out var guidance) ? guidance : [];
            if (step.ExecutionGuidanceRefs.Any(reference => !step.ResolvedExecutionGuidance.Any(item => item.Reference == reference))) {
                throw new InvalidOperationException("The authored definition is missing pinned execution guidance.");
            }
        }
        return content;
    }

    public static string Hash(string value) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static string WriteReceipt(ProcessAuthoringReceipt receipt)
        => JsonSerializer.Serialize(receipt, ProcessAuthoringJsonContext.Default.ProcessAuthoringReceipt);

    public static ProcessAuthoringReceipt ReadReceipt(string json) {
        var receipt = JsonSerializer.Deserialize(json, ProcessAuthoringJsonContext.Default.ProcessAuthoringReceipt)
            ?? throw new InvalidOperationException("The authored operation has no receipt.");
        return receipt.Snapshot is { } snapshot ? receipt with { Snapshot = snapshot with { Content = Read(Write(snapshot.Content)) } } : receipt;
    }

    private static void Validate(ProcessAuthoringContent content) {
        if (content.SchemaVersion != ProcessAuthoringContent.CurrentSchemaVersion) {
            throw new NotSupportedException($"Authoring schema {content.SchemaVersion} is unsupported.");
        }
        if (string.IsNullOrWhiteSpace(content.Definition.Key) ||
                content.Definition.Steps.Select(step => step.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != content.Definition.Steps.Count ||
                content.Definition.RoleUsages.Select(role => role.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != content.Definition.RoleUsages.Count) {
            throw new InvalidOperationException("The authored definition contains an absent or ambiguous identity.");
        }
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(ProcessAuthoringContent))]
[JsonSerializable(typeof(ProcessAuthoringReceipt))]
internal sealed partial class ProcessAuthoringJsonContext : JsonSerializerContext;
