using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Modules.Memory.Services;

public sealed class MemoryProviderProfileConfigurationService(
    IMemoryProviderProfileStore profileStore,
    MemoryProviderProfileEditorMapper editorMapper,
    TimeProvider timeProvider) : IMemoryProviderProfileConfigurationService
{
    public async Task<IReadOnlyList<MemoryProviderProfileConfigurationSnapshot>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var profiles = await profileStore.ListAsync(cancellationToken).ConfigureAwait(false);
        return profiles.Select(ToSnapshot).ToArray();
    }

    public async Task<MemoryProviderProfileConfigurationSnapshot?> GetAsync(
        MemoryProviderInstanceId providerId,
        CancellationToken cancellationToken = default)
    {
        var profile = await profileStore.GetAsync(providerId, cancellationToken).ConfigureAwait(false);
        return profile is null ? null : ToSnapshot(profile);
    }

    public async Task<MemoryProviderProfileConfigurationSnapshot> SaveAsync(
        MemoryProviderInstanceId providerId,
        MemoryProviderProfileConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configuration.Capabilities);
        ValidateCapabilities(configuration.Capabilities);
        ValidateTransport(configuration);

        var existing = await GetAsync(providerId, cancellationToken).ConfigureAwait(false);
        var editor = existing is null
            ? new MemoryProviderProfileEditorModel()
            : editorMapper.FromProfile(MemoryProviderManagementProfile.FromProfile(existing.Profile));
        Apply(providerId, configuration, editor);
        MemoryProviderProfile saved;
        try
        {
            saved = editorMapper.ToProfile(editor);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            throw new MemoryProviderProfileConfigurationException(exception.Message, exception);
        }

        await profileStore
            .UpsertAsync(saved, timeProvider.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
        return ToSnapshot(saved);
    }

    private MemoryProviderProfileConfigurationSnapshot ToSnapshot(MemoryProviderProfile profile)
    {
        var editor = editorMapper.FromProfile(MemoryProviderManagementProfile.FromProfile(profile));
        return new MemoryProviderProfileConfigurationSnapshot(
            profile,
            new MemoryProviderProfileConfiguration(
                editor.DisplayName,
                editor.DriverKind,
                editor.IsEnabled,
                editor.FallbackBehavior,
                editor.ProviderKind,
                editor.SelectionTags.ToArray(),
                new MemoryProviderProfileCapabilityConfiguration(
                    editor.SupportsContextQuerySync,
                    editor.SupportsContextQueryAsync,
                    editor.SupportsOperationStatus),
                profile.DriverKind is MemoryProviderDriverKind.Http or MemoryProviderDriverKind.NativeRemote
                    ? new MemoryProviderHttpTransportConfiguration(
                        editor.Http.BaseUrl,
                        editor.Http.QueryPath,
                        editor.Http.HealthPath,
                        editor.Http.ApiKeyEnvironmentVariable,
                        editor.Http.AuthHeaderName,
                        editor.Http.AuthScheme,
                        editor.Http.TimeoutMilliseconds,
                        editor.Http.MaxRetryAttempts)
                    : null,
                profile.DriverKind == MemoryProviderDriverKind.Mcp
                    ? new MemoryProviderMcpTransportConfiguration(
                        editor.Mcp.DescriptorKind,
                        editor.Mcp.ServerKey,
                        editor.Mcp.DisplayName,
                        editor.Mcp.Description,
                        editor.Mcp.RemoteEndpoint,
                        editor.Mcp.AuthHeaderName,
                        editor.Mcp.AuthHeaderEnvironmentVariable,
                        editor.Mcp.ContextQueryTool,
                        editor.Mcp.OperationStatusTool)
                    : null));
    }

    private static void Apply(
        MemoryProviderInstanceId providerId,
        MemoryProviderProfileConfiguration configuration,
        MemoryProviderProfileEditorModel editor)
    {
        editor.InstanceId = providerId.Value;
        editor.DisplayName = configuration.DisplayName;
        editor.DriverKind = configuration.DriverKind;
        editor.IsEnabled = configuration.IsEnabled;
        editor.WorkspaceScope = MemoryProviderWorkspaceScope.AllWorkspaces;
        editor.FallbackBehavior = configuration.FallbackBehavior;
        editor.ProviderKind = configuration.ProviderKind;
        editor.SelectionTags = configuration.SelectionTags.ToList();
        editor.SupportsContextQuerySync = configuration.Capabilities.SupportsSynchronousQueries;
        editor.SupportsContextQueryAsync = configuration.Capabilities.SupportsAsynchronousQueries;
        editor.SupportsOperationStatus = configuration.Capabilities.SupportsOperationStatus;
        editor.SupportsSnapshotIngestion = false;
        editor.SupportsProviderRequestedSources = false;
        editor.SupportsImmediateFeedback = false;
        editor.SupportsDelayedFeedback = false;
        editor.SupportsProviderEvents = false;
        editor.SupportsHostEventPolling = false;

        if (configuration.Http is { } http)
        {
            editor.Http.BaseUrl = http.BaseUrl;
            editor.Http.QueryPath = http.QueryPath;
            editor.Http.HealthPath = http.HealthPath;
            editor.Http.ApiKeyEnvironmentVariable = http.ApiKeyEnvironmentVariable;
            editor.Http.AuthHeaderName = http.AuthHeaderName;
            editor.Http.AuthScheme = http.AuthScheme;
            editor.Http.TimeoutMilliseconds = http.TimeoutMilliseconds;
            editor.Http.MaxRetryAttempts = http.MaxRetryAttempts;
        }

        if (configuration.Mcp is { } mcp)
        {
            editor.Mcp.DescriptorKind = mcp.DescriptorKind;
            editor.Mcp.ServerKey = mcp.ServerKey;
            editor.Mcp.DisplayName = mcp.DisplayName;
            editor.Mcp.Description = mcp.Description;
            editor.Mcp.RemoteEndpoint = mcp.RemoteEndpoint;
            editor.Mcp.AuthHeaderName = mcp.AuthHeaderName;
            editor.Mcp.AuthHeaderEnvironmentVariable = mcp.AuthHeaderEnvironmentVariable;
            editor.Mcp.ContextQueryTool = mcp.ContextQueryTool;
            editor.Mcp.OperationStatusTool = mcp.OperationStatusTool;
        }
    }

    private static void ValidateTransport(MemoryProviderProfileConfiguration configuration)
    {
        switch (configuration.DriverKind)
        {
            case MemoryProviderDriverKind.Mock:
                RequireAbsent(configuration.Http, "HTTP", configuration.DriverKind);
                RequireAbsent(configuration.Mcp, "MCP", configuration.DriverKind);
                return;
            case MemoryProviderDriverKind.Http:
            case MemoryProviderDriverKind.NativeRemote:
                RequirePresent(configuration.Http, "HTTP", configuration.DriverKind);
                RequireAbsent(configuration.Mcp, "MCP", configuration.DriverKind);
                return;
            case MemoryProviderDriverKind.Mcp:
                RequirePresent(configuration.Mcp, "MCP", configuration.DriverKind);
                RequireAbsent(configuration.Http, "HTTP", configuration.DriverKind);
                return;
            default:
                throw new MemoryProviderProfileConfigurationException(
                    $"Memory provider driver '{configuration.DriverKind}' is not supported by the provider API.");
        }
    }

    private static void ValidateCapabilities(
        MemoryProviderProfileCapabilityConfiguration capabilities)
    {
        if (capabilities.SupportsAsynchronousQueries &&
            !capabilities.SupportsOperationStatus)
        {
            throw new MemoryProviderProfileConfigurationException(
                "Asynchronous memory provider queries require operation-status support because this API exposes polling as the completion path.");
        }
    }

    private static void RequirePresent<T>(
        T? configuration,
        string transportName,
        MemoryProviderDriverKind driverKind)
        where T : class
    {
        if (configuration is null)
        {
            throw new MemoryProviderProfileConfigurationException(
                $"{driverKind} memory providers require an {transportName} transport configuration.");
        }
    }

    private static void RequireAbsent<T>(
        T? configuration,
        string transportName,
        MemoryProviderDriverKind driverKind)
        where T : class
    {
        if (configuration is not null)
        {
            throw new MemoryProviderProfileConfigurationException(
                $"{driverKind} memory providers must not include an {transportName} transport configuration.");
        }
    }
}
