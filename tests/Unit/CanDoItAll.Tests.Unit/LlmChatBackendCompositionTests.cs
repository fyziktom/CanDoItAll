using System.Reflection;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.ProviderRuntime;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats;
using CanDoItAll.Modules.LlmChats.Persistence;
using CanDoItAll.Modules.LlmChats.Persistence.DatabaseTransfer;
using CanDoItAll.Modules.LlmChats.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.LlmChats;

public sealed class LlmChatBackendCompositionTests
{
    [Fact]
    public void Real_profile_scope_resolves_backend_without_generic_file_store_or_workflow_registration()
    {
        var services = CreateProfileServices(useExistingInvocationPort: true);
        services.AddLlmChatsApplication();
        services.AddLlmChatsPersistence();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ILlmChatDefinitionApplicationService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ILlmChatConversationApplicationService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ILlmChatOperationApplicationService>());
        Assert.IsType<LlmChatConversationEngine>(
            scope.ServiceProvider.GetRequiredService<ILlmChatConversationEngine>());
        Assert.Contains(
            scope.ServiceProvider.GetServices<IDatabaseTransferHandler>(),
            handler => handler is LlmChatsDatabaseTransferHandler);
        Assert.Null(scope.ServiceProvider.GetService<ILlmConversationService>());
        Assert.Null(scope.ServiceProvider.GetService<ILlmConversationStore>());
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType.Assembly.GetName().Name?.Contains(
                ".Workflows.",
                StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Persistence_composition_owns_provider_backed_port_without_workflow_composition()
    {
        var services = CreateProfileServices(useExistingInvocationPort: false);
        services.AddLlmChatsApplication();
        services.AddLlmChatsPersistence();
        using var provider = services.BuildServiceProvider();

        Assert.IsType<ProviderBackedLlmInvocationAdapter>(
            provider.GetRequiredService<ILlmInvocationPort>());
    }

    [Fact]
    public async Task Runtime_lease_unsubscribes_from_profile_notifications_when_disposed()
    {
        var notifications = new TestDatabaseSwitchNotificationService();
        var factory = new DatabaseProfileLlmChatRuntimeLeaseFactory(
            ProviderRuntimeTestData.CreateCanonicalRuntimeDatabase(ProviderRuntimeTestData.RuntimeIdentity),
            new MutableDatabaseRuntimeState(ProviderRuntimeTestData.RuntimeIdentity),
            notifications);
        var lease = await factory.AcquireAsync();
        Assert.Equal(1, notifications.SubscriberCount);

        await lease.DisposeAsync();

        Assert.Equal(0, notifications.SubscriberCount);
    }

    [Fact]
    public async Task Runtime_lease_ignores_a_profile_notification_captured_before_disposal()
    {
        var notifications = new TestDatabaseSwitchNotificationService();
        var factory = new DatabaseProfileLlmChatRuntimeLeaseFactory(
            ProviderRuntimeTestData.CreateCanonicalRuntimeDatabase(ProviderRuntimeTestData.RuntimeIdentity),
            new MutableDatabaseRuntimeState(ProviderRuntimeTestData.RuntimeIdentity),
            notifications);
        var lease = await factory.AcquireAsync();
        var capturedSubscriber = notifications.CaptureSubscriber();

        await lease.DisposeAsync();

        var identity = ProviderRuntimeTestData.RuntimeIdentity;
        var exception = Record.Exception(() => capturedSubscriber(
            notifications,
            new DatabaseProfileChangedNotification(
                identity.ActiveProfileId,
                identity.ActiveFingerprint,
                identity.ActiveProfileId!.Value,
                identity.ActiveFingerprint!,
                identity.Generation + 1)));
        Assert.Null(exception);
    }

    private static ServiceCollection CreateProfileServices(bool useExistingInvocationPort)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"llm-chat-composition-{Guid.NewGuid():N}")
            .Options;
        var providerProfile = ProviderRuntimeTestData.CreateProvider();
        services.AddScoped(_ => new AppDbContext(options));
        services.AddSingleton<IDbContextFactory<AppDbContext>>(new TestDbContextFactory(options));
        services.AddSingleton<IDatabaseRuntimeState>(
            new MutableDatabaseRuntimeState(ProviderRuntimeTestData.RuntimeIdentity));
        services.AddSingleton<IDatabaseRuntimeWriteFence, TestDatabaseRuntimeWriteFence>();
        services.AddSingleton<ICanonicalRuntimeDatabase>(
            ProviderRuntimeTestData.CreateCanonicalRuntimeDatabase(ProviderRuntimeTestData.RuntimeIdentity));
        services.AddSingleton<IDatabaseSwitchNotificationService, TestDatabaseSwitchNotificationService>();
        services.AddScoped<IProviderRuntimeProfileSource>(_ => new StaticProviderSource(providerProfile));
        services.AddSingleton(CreateInterfaceProxy<IProviderRuntimeDescriptorStore>());
        services.AddSingleton(CreateInterfaceProxy<IProviderRuntimePool>());
        if (useExistingInvocationPort)
        {
            services.AddSingleton<ILlmInvocationPort>(new DelegatingInvocationPort((request, _) =>
                Task.FromResult(new LlmInvocationResult(request.Model, "answer", LlmUsage.Zero))));
        }

        return services;
    }

    private static T CreateInterfaceProxy<T>() where T : class
        => DispatchProxy.Create<T, ThrowingDispatchProxy>();

    private sealed class StaticProviderSource(ProviderProfile provider) : IProviderRuntimeProfileSource
    {
        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProviderProfile>>([provider]);

        public Task<ProviderProfile?> GetProviderAsync(
            Guid providerId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(provider.Id == providerId ? provider : null);
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
            => new(options);
    }

    private class ThrowingDispatchProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => throw new NotSupportedException($"Test proxy member '{targetMethod?.Name}' must not be invoked.");
    }
}
