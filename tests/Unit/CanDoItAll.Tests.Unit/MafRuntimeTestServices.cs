using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.Tools.Documents;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

internal static class MafRuntimeTestServices
{
    public static ServiceCollection CreateProviderRuntimeServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMafProviderRuntimeGateway>(new UnavailableMafProviderRuntimeGateway());
        services.AddSingleton<IMafProviderStreamingDispatchGate>(NoOpMafProviderStreamingDispatchGate.Instance);
        services.AddSingleton<IAgentImageAnalysisService, UnavailableAgentImageAnalysisService>();
        services.AddSingleton<IWorkspaceRuntimeServicesFactory>(new WorkspaceRuntimeServicesFactory(
            [],
            new ManagedCodeMarkItDownDocumentMarkdownConverter()));
        services.AddMafRuntimeArchitectureServices();
        services.AddSingleton<IMafProviderAgentFactory>(serviceProvider => new MafProviderAgentFactory(
            serviceProvider.GetRequiredService<IMafProviderCredentialService>(),
            serviceProvider.GetRequiredService<IMafProviderStreamingDispatchGate>()));
        return services;
    }

    private sealed class UnavailableMafProviderRuntimeGateway : IMafProviderRuntimeGateway
    {
        public Task<ProviderHealthResult> TestProviderAsync(
            ProviderProfile provider,
            CancellationToken cancellationToken = default)
            => throw CreateUnavailableException();

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(
            ProviderProfile provider,
            ProviderTestChatRequest request,
            string model,
            CancellationToken cancellationToken = default)
            => throw CreateUnavailableException();

        public Task<ProviderTestChatResult> RunProviderImageChatAsync(
            ProviderProfile provider,
            ProviderTestChatRequest request,
            string model,
            IReadOnlyList<ProviderChatAttachment> attachments,
            string modelParameterConfigurationJson = "",
            CancellationToken cancellationToken = default)
            => throw CreateUnavailableException();

        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
            ProviderProfile provider,
            ProviderModelMaintenanceEditorRequest request,
            CancellationToken cancellationToken = default)
            => throw CreateUnavailableException();

        private static InvalidOperationException CreateUnavailableException()
            => new("This test fixture explicitly disables provider runtime operations.");
    }
}
