using CanDoItAll.Security.Abstractions;

namespace CanDoItAll.Modules.Security;

public enum PluginSecretResolutionPurpose
{
    ConnectionHealthCheck,
    WorkflowExecutor,
    SettingsValidation
}

public sealed record PluginSecretReference(
    Guid SecretId,
    string PluginId,
    string ConnectionId,
    string BindingName = "");

public sealed record PluginSecretResolveRequest(
    PluginSecretReference Reference,
    PluginSecretResolutionPurpose Purpose);

public sealed record PluginSecretBindingSummary(
    Guid SecretId,
    string NameSnapshot,
    SecretKind Kind,
    string Scope,
    string Purpose);

public interface IPluginSecretBroker
{
    Task<string?> ResolveSecretAsync(
        PluginSecretResolveRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PluginSecretBindingSummary>> ListAllowedSecretsAsync(
        string pluginId,
        string connectionId,
        CancellationToken cancellationToken = default);
}

public sealed class PluginSecretBroker(
    ISecretRuntimeResolver secretRuntimeResolver,
    SecretService secretService) : IPluginSecretBroker
{
    public async Task<string?> ResolveSecretAsync(
        PluginSecretResolveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reference = request.Reference;
        if (reference.SecretId == Guid.Empty)
        {
            throw new ArgumentException("Plugin secret id is required.", nameof(request));
        }

        var runtimePurpose = ToRuntimePurpose(request.Purpose);
        return await secretRuntimeResolver.ResolveValueAsync(
            new SecretRuntimeRequest(
                reference.SecretId,
                runtimePurpose,
                AllowedSecretIds: null,
                ConsumerType: SecretRuntimeConsumerTypes.PluginConnection,
                ConsumerId: SecretRuntimeConsumerIds.PluginConnection(reference.PluginId, reference.ConnectionId)),
            cancellationToken);
    }

    public async Task<IReadOnlyList<PluginSecretBindingSummary>> ListAllowedSecretsAsync(
        string pluginId,
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        var summaries = await secretService.ListBindingsAsync(
            SecretRuntimeConsumerTypes.PluginConnection,
            SecretRuntimeConsumerIds.PluginConnection(pluginId, connectionId),
            cancellationToken: cancellationToken);

        return summaries
            .Select(summary => new PluginSecretBindingSummary(
                summary.SecretId,
                summary.NameSnapshot,
                summary.Kind,
                summary.Scope,
                summary.Purpose))
            .ToList();
    }

    private static string ToRuntimePurpose(PluginSecretResolutionPurpose purpose)
        => purpose switch
        {
            PluginSecretResolutionPurpose.ConnectionHealthCheck => SecretRuntimePurposes.PluginConnectionSecret,
            PluginSecretResolutionPurpose.WorkflowExecutor => SecretRuntimePurposes.PluginWorkflowExecutorSecret,
            PluginSecretResolutionPurpose.SettingsValidation => SecretRuntimePurposes.PluginSettingsValidation,
            _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, "Unsupported plugin secret resolution purpose.")
        };
}
