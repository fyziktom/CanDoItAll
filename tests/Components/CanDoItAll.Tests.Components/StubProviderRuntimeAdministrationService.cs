using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using Microsoft.Extensions.DependencyInjection;
using RuntimeProviderProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;
using RuntimeProviderProfileEditorModel = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;

namespace CanDoItAll.Tests.Components.AgentFramework;

internal static class ProviderRuntimeAdministrationTestServices {
    public static IServiceCollection AddStubProviderRuntimeAdministration(
        this IServiceCollection services,
        IReadOnlyList<RuntimeProviderProfile>? providers = null) {
        return services.AddSingleton<IProviderRuntimeAdministrationService>(
            new StubProviderRuntimeAdministrationService(providers ?? []));
    }
}

internal sealed class StubProviderRuntimeAdministrationService(
    IReadOnlyList<RuntimeProviderProfile> providers) : IProviderRuntimeAdministrationService {
    public Task<IReadOnlyList<RuntimeProviderProfile>> ListProvidersAsync(
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(providers);
    }

    public Task<RuntimeProviderProfileEditorModel> GetProviderEditorAsync(
        Guid? providerId = null,
        CancellationToken cancellationToken = default) => Unexpected<RuntimeProviderProfileEditorModel>();

    public Task<Guid> SaveProviderAsync(
        RuntimeProviderProfileEditorModel model,
        CancellationToken cancellationToken = default) => Unexpected<Guid>();

    public Task DeleteProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken = default) => Unexpected();

    public Task<ProviderHealthResult> TestProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken = default) => Unexpected<ProviderHealthResult>();

    public Task<ProviderTestChatResult> RunProviderTestChatAsync(
        Guid providerId,
        ProviderTestChatRequest request,
        CancellationToken cancellationToken = default) => Unexpected<ProviderTestChatResult>();

    public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
        Guid providerId,
        ProviderModelMaintenanceEditorRequest request,
        CancellationToken cancellationToken = default) => Unexpected<ProviderModelMaintenanceEditorResult>();

    private static Task Unexpected() => Task.FromException(UnexpectedCall());

    private static Task<T> Unexpected<T>() => Task.FromException<T>(UnexpectedCall());

    private static InvalidOperationException UnexpectedCall() =>
        new("Provider runtime mutation was not expected in this component test.");
}