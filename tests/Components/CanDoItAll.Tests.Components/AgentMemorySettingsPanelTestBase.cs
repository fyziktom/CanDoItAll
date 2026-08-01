using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Components;

public abstract class AgentMemorySettingsPanelTestBase
{
    protected static BunitContext CreateContext(
        IMemoryProviderProfileStore store,
        RecordingLogger<AgentMemorySettingsPanel>? logger = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton(store);
        context.Services.AddSingleton<ILogger<AgentMemorySettingsPanel>>(
            logger ?? new RecordingLogger<AgentMemorySettingsPanel>());
        context.Services.AddSingleton<IMemoryProviderDriver>(new TestMemoryProviderDriver(MemoryProviderDriverKind.Mock));
        return context;
    }

    protected static IRenderedComponent<AgentMemorySettingsPanel> Render(
        BunitContext context,
        AgentMemoryAccessSettings settings) =>
        context.Render<AgentMemorySettingsPanel>(parameters => parameters
            .Add(component => component.Value, settings));

    protected static AgentMemoryAccessSettings CreateSettingsWithBinding(
        string alias,
        string providerId) => new()
    {
        InvocationMode = AgentMemoryInvocationMode.Automatic,
        CanUseMemoryTools = true,
        ProviderBindings =
        [
            new AgentMemoryProviderBindingSetting(
                AgentMemoryProviderAlias.Parse(alias),
                MemoryProviderInstanceId.Parse(providerId))
        ]
    };

    protected static MemoryProviderProfile CreateProvider(
        string instanceId,
        string displayName,
        bool isEnabled,
        bool supportsSyncQuery = true,
        MemoryProviderDriverKind driverKind = MemoryProviderDriverKind.Mock,
        MemoryProviderHealthState healthState = MemoryProviderHealthState.Healthy) =>
        new(
            MemoryProviderInstanceId.Parse(instanceId),
            displayName,
            driverKind,
            isEnabled,
            healthState,
            MemoryProviderWorkspaceScope.AllWorkspaces,
            SelectionTags: [],
            MemoryProviderProfilePolicy.Default,
            new MemoryProviderManifest(
                MemoryProviderKind.Parse("memory.mock"),
                MemoryProtocolVersion.Current,
                [new MemoryCapabilityDescriptor(
                    supportsSyncQuery ? MemoryCapabilityIds.ContextQuerySync : MemoryCapabilityIds.OperationStatus,
                    "1",
                    Supported: true)],
                MemoryProviderInteractionSupport.SyncQueryOnly,
                UiSurfaces: [],
                MemoryProviderLimits.Default,
                MemoryExtensionData.Empty));

    protected sealed class TestProfileStore(params MemoryProviderProfile[] profiles) : IMemoryProviderProfileStore
    {
        public Task UpsertAsync(
            MemoryProviderProfile profile,
            DateTimeOffset updatedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<MemoryProviderProfile?> GetAsync(
            MemoryProviderInstanceId providerId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(profiles.FirstOrDefault(profile => profile.InstanceId == providerId));

        public Task<IReadOnlyList<MemoryProviderProfile>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MemoryProviderProfile>>(profiles);
    }

    protected sealed class ThrowingProfileStore : IMemoryProviderProfileStore
    {
        public Task UpsertAsync(
            MemoryProviderProfile profile,
            DateTimeOffset updatedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<MemoryProviderProfile?> GetAsync(
            MemoryProviderInstanceId providerId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Profile store unavailable with secret=do-not-render.");

        public Task<IReadOnlyList<MemoryProviderProfile>> ListAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Profile store unavailable with secret=do-not-render.");
    }

    protected sealed class TestMemoryProviderDriver(MemoryProviderDriverKind driverKind) : IMemoryProviderDriver
    {
        public MemoryProviderDriverKind DriverKind { get; } = driverKind;

        public Task<MemoryProviderDriverResult> ExecuteContextQueryAsync(
            MemoryProviderProfile provider,
            MemoryOperationRecord operation,
            MemoryContextQueryRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    protected sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, exception, formatter(state, exception)));
        }
    }

    protected sealed record LogEntry(LogLevel Level, Exception? Exception, string Message);
}
