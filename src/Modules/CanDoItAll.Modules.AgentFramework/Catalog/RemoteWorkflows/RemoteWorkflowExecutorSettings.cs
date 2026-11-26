using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.RemoteInstances;

namespace CanDoItAll.Modules.AgentFramework;

internal static class RemoteWorkflowExecutorSettings {
    private static readonly JsonSerializerOptions Json = new(WorkflowExecutorJson.Options) {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    internal sealed record Portable(string Settings, ProviderProfilePurpose? Purpose = null, Guid? Provider = null, string? Model = null, bool Vision = false);

    internal static Portable Read(WorkflowNode node, RemoteTemplateModelChoice? choice = null) {
        var settings = node.Settings;
        if (settings.ExecutorId is not { } id) {
            if (!string.IsNullOrWhiteSpace(settings.ExecutorSettingsJson) && settings.ExecutorSettingsJson.Trim() != "{}") {
                throw Error(node, "Remove settings belonging to an unassigned executor.");
            }
            return new(settings.ExecutorSettingsJson);
        }
        var json = string.IsNullOrWhiteSpace(settings.ExecutorSettingsJson) ? "{}" : settings.ExecutorSettingsJson;
        try {
            if (id == WorkflowExecutorIds.ImageGeneration) {
                var value = Parse<WorkflowImageGenerationExecutorSettings>(json);
                RequireWorkspacePaths(node, value.OutputWorkspacePath);
                return new(Serialize(value with { ProviderProfileId = choice?.ProviderId, Model = choice?.Model ?? string.Empty }),
                    ProviderProfilePurpose.ImageGeneration, value.ProviderProfileId, value.Model);
            }
            if (id == WorkflowExecutorIds.ImageAnalyze) {
                var value = Parse<WorkflowImageAnalyzeExecutorSettings>(json);
                RequireWorkspacePaths(node, value.Path);
                RemoteTemplateModelParameters.RequirePortable(value.ModelParameterConfigurationJson);
                return new(Serialize(value with { ProviderProfileId = choice?.ProviderId, Model = choice?.Model ?? string.Empty }),
                    ProviderProfilePurpose.Chat, value.ProviderProfileId, value.Model, Vision: true);
            }
            if (id == WorkflowExecutorIds.ProjectStructure) {
                var value = Parse<WorkflowProjectStructureExecutorSettings>(json);
                if (value.ProjectId.HasValue || !string.IsNullOrWhiteSpace(value.NodeId)) {
                    throw Error(node, "Bind project and node identifiers through workflow inputs before sharing.");
                }
                RequireWorkspacePaths(node, value.SourceWorkspacePath);
                return new(Serialize(value));
            }
            if (id == WorkflowExecutorIds.HttpFetch) {
                var value = Parse<WorkflowHttpExecutorSettings>(json);
                if (value.SecretHeader is null || value.SecretHeader.SecretId.HasValue ||
                    !string.IsNullOrWhiteSpace(value.SecretHeader.SecretNameSnapshot) || value.Headers is not { Count: 0 } ||
                    value.QueryParameters is not { Count: 0 } || !string.IsNullOrWhiteSpace(value.Body) || value.AllowPrivateNetworkTargets ||
                    !string.IsNullOrWhiteSpace(value.Url) && (!Uri.TryCreate(value.Url, UriKind.Absolute, out var uri) ||
                        uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)) {
                    throw Error(node, "The HTTP request contains private connection settings. Supply them locally through workflow inputs.");
                }
                RequireWorkspacePaths(node, value.OutputPath);
                return new(Serialize(value with { SecretHeader = new() }));
            }
            if (id == WorkflowExecutorIds.SourceIngestion) {
                var value = Parse<WorkflowSourceIngestionExecutorSettings>(json);
                if (value.AllowAbsoluteInputPaths) {
                    throw Error(node, "Use workspace-relative source paths before sharing.");
                }
                return new(Serialize(value));
            }
            if (id == WorkflowExecutorIds.StorageFile) {
                var value = Parse<WorkflowStorageFileExecutorSettings>(json);
                RequireWorkspacePaths(node, value.Path, value.DestinationPath);
                return new(Serialize(value));
            }
            if (id == WorkflowExecutorIds.ImageInspect) {
                var value = Parse<WorkflowImageInspectExecutorSettings>(json);
                RequireWorkspacePaths(node, value.Path);
                return new(Serialize(value));
            }
            if (id == WorkflowExecutorIds.DocumentToMarkdown) {
                var value = Parse<WorkflowDocumentToMarkdownExecutorSettings>(json);
                RequireWorkspacePaths(node, value.SourcePath, value.OutputPath);
                return new(Serialize(value));
            }
            if (id == WorkflowExecutorIds.MarkdownRender) {
                var value = Parse<WorkflowMarkdownRenderExecutorSettings>(json);
                RequireWorkspacePaths(node, value.TemplatePath, value.OutputPath);
                return new(Serialize(value));
            }
            if (id == WorkflowExecutorIds.Spreadsheet) {
                var value = Parse<WorkflowSpreadsheetExecutorSettings>(json);
                RequireWorkspacePaths(node, value.WorkbookPath, value.OutputWorkbookPath);
                return new(Serialize(value));
            }
            if (id == WorkflowExecutorIds.JsonTransform) {
                return new(Serialize(Parse<WorkflowJsonTransformExecutorSettings>(json)));
            }
            if (id == WorkflowExecutorIds.ApprovalRequest) {
                return new(Serialize(Parse<WorkflowApprovalExecutorSettings>(json)));
            }
            if (id == WorkflowExecutorIds.Delay) {
                return new(Serialize(Parse<WorkflowDelayExecutorSettings>(json)));
            }
        } catch (JsonException) {
            throw Error(node, "The executor settings cannot be read with its portable schema.");
        }
        throw Error(node, $"Executor '{id.Value}' requires an installation-specific configuration. Replace it before sharing.");
    }

    internal static RemoteInstanceReadException Error(WorkflowNode node, string detail) =>
        new($"Workflow step '{node.Name}' ({node.Id.Value}). {detail}");

    private static T Parse<T>(string json) where T : class =>
        JsonSerializer.Deserialize<T>(json, Json) ?? throw new JsonException();

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);

    private static void RequireWorkspacePaths(WorkflowNode node, params string[] paths) {
        if (paths.Any(path => path is null || path.StartsWith('/') || path.StartsWith('\\') ||
            path.Contains(':') || path.Split('/', '\\').Contains("..", StringComparer.Ordinal))) {
            throw Error(node, "Use a path relative to the receiving workflow workspace.");
        }
    }
}
