using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.Conversations;
using CanDoItAll.AgentFramework.Llm.ProviderRuntime;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.AgentFramework.Workflows.Runtime;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Persistence;
using CanDoItAll.Modules.LlmChats.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class ProviderRuntimeContractOwnershipTests
{
    [Fact]
    public void Provider_read_and_capability_contracts_are_provider_runtime_owned()
    {
        Assert.Equal(
            typeof(IProviderRuntimePool).Assembly,
            typeof(IProviderRuntimeProfileSource).Assembly);
        Assert.Equal(
            typeof(IProviderRuntimePool).Assembly,
            typeof(IProviderModelCapabilityResolver).Assembly);

        var persistenceReferences = typeof(CanonicalLlmChatProviderResolver).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("CanDoItAll.AgentFramework.Core", persistenceReferences);
        Assert.DoesNotContain("CanDoItAll.Modules.AgentFramework", persistenceReferences);
    }
}

public sealed class LlmInvocationPortCompositionTests
{
    [Fact]
    public void Workflow_and_product_registration_share_one_idempotent_port_owner()
    {
        var services = new ServiceCollection();

        services.AddProviderBackedLlmInvocationPort();
        services.AddWorkflowLlmInvocation();
        services.AddProviderBackedLlmInvocationPort();

        var port = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ILlmInvocationPort));
        Assert.Equal(ServiceLifetime.Singleton, port.Lifetime);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ILlmConversationService));
    }

    [Fact]
    public async Task Typed_thinking_effort_is_written_to_the_provider_parameter_envelope()
    {
        ProviderChatCompletionRequest? captured = null;
        var driver = new DelegatingChatDriver((request, _) =>
        {
            captured = request;
            return Task.FromResult(new ProviderChatCompletionResult("model", "ok", 1, 1));
        });
        var adapter = ProviderRuntimeTestData.CreateInvocationAdapter(driver);
        var settings = new LlmModelSettings(0.2, """{"maxOutputTokens":123}""")
        {
            ThinkingEffort = AgentReasoningEffortLevel.None
        };

        await adapter.InvokeAsync(new LlmInvocationRequest(
            ProviderRuntimeTestData.CreateProvider(),
            "model",
            [new LlmMessage(LlmMessageRole.User, "hello")],
            settings: settings));

        Assert.NotNull(captured);
        Assert.Equal(
            AgentReasoningEffortLevel.None,
            AgentThinkingEffortPolicy.ReadConfiguredEffort(
                captured.ModelParameterConfigurationJson,
                "test"));
        Assert.Contains("maxOutputTokens", captured.ModelParameterConfigurationJson, StringComparison.Ordinal);
    }
}

public sealed class LlmChatProviderResolutionTests
{
    [Fact]
    public async Task Options_project_distinct_effort_sets_for_models_without_sensitive_profile_fields()
    {
        var provider = ProviderRuntimeTestData.CreateProvider("Renamed provider");
        var resolver = ProviderRuntimeTestData.CreateResolver(provider);

        var result = await resolver.ListOptionsAsync();

        Assert.True(result.IsSuccess);
        var option = Assert.Single(result.Value!);
        Assert.Equal(provider.Id, option.ProviderProfileId);
        Assert.Equal("Renamed provider", option.ProviderName);
        Assert.Equal(
            [AgentReasoningEffortLevel.None, AgentReasoningEffortLevel.Low],
            option.Models.Single(model => model.Model == "model-fast").ThinkingEffort.AllowedEfforts);
        Assert.Equal(
            [AgentReasoningEffortLevel.Low, AgentReasoningEffortLevel.Medium, AgentReasoningEffortLevel.High],
            option.Models.Single(model => model.Model == "model-deep").ThinkingEffort.AllowedEfforts);
    }

    [Fact]
    public async Task Stable_id_survives_rename_but_kind_model_and_effort_mismatches_fail_typed()
    {
        var provider = ProviderRuntimeTestData.CreateProvider("Renamed provider");
        var resolver = ProviderRuntimeTestData.CreateResolver(provider);

        var resolved = await resolver.ResolveAsync(
            provider.Id,
            provider.Kind,
            "model-fast",
            AgentReasoningEffortLevel.None);
        var wrongKind = await resolver.ResolveAsync(
            provider.Id,
            ProviderKind.OpenAi,
            "model-fast",
            null);
        var wrongModel = await resolver.ResolveAsync(
            provider.Id,
            provider.Kind,
            "unknown",
            null);
        var wrongEffort = await resolver.ResolveAsync(
            provider.Id,
            provider.Kind,
            "model-fast",
            AgentReasoningEffortLevel.High);

        Assert.True(resolved.IsSuccess);
        Assert.Equal("Renamed provider", resolved.Value!.ProviderName);
        Assert.Equal(AgentReasoningEffortLevel.Low, resolved.Value.ProviderDefaultThinkingEffort);
        Assert.Equal(LlmChatErrorCodes.ProviderKindMismatch, Assert.Single(wrongKind.Errors).Code);
        Assert.Equal(LlmChatErrorCodes.ModelNotSupported, Assert.Single(wrongModel.Errors).Code);
        Assert.Equal(LlmChatErrorCodes.ThinkingEffortNotSupported, Assert.Single(wrongEffort.Errors).Code);
    }
}

public sealed class LlmChatRuntimeFenceTests
{
    [Fact]
    public async Task Stale_generation_is_rejected_before_provider_dispatch()
    {
        var initial = ProviderRuntimeTestData.RuntimeIdentity;
        var state = new MutableDatabaseRuntimeState(initial);
        var scope = new LlmChatOperationScopeAccessor();
        var invocationCount = 0;
        var fenced = new ProfileFencedLlmChatInvocationPort(
            new DelegatingInvocationPort((request, cancellationToken) =>
            {
                invocationCount++;
                return Task.FromResult(new LlmInvocationResult(request.Model, "answer", LlmUsage.Zero));
            }),
            state,
            scope);
        using var operation = scope.Push(new LlmChatOperationExecutionContext(
            LlmChatOperationId.New(),
            new LlmChatRuntimeIdentity(
                initial.ActiveProfileId!.Value,
                initial.ActiveFingerprint!,
                initial.Generation)));
        state.Change(initial with { Generation = initial.Generation + 1 });

        await Assert.ThrowsAsync<LlmChatRuntimeProfileChangedException>(() => fenced.InvokeAsync(
            new LlmInvocationRequest(
                ProviderRuntimeTestData.CreateProvider(),
                "model-fast",
                [new LlmMessage(LlmMessageRole.User, "hello")])));

        Assert.Equal(0, invocationCount);
    }

    [Fact]
    public async Task Lease_cancels_and_fails_when_profile_generation_changes()
    {
        var initial = ProviderRuntimeTestData.RuntimeIdentity;
        var state = new MutableDatabaseRuntimeState(initial);
        var notifications = new TestDatabaseSwitchNotificationService();
        var factory = new DatabaseProfileLlmChatRuntimeLeaseFactory(state, notifications);
        await using var lease = await factory.AcquireAsync();

        state.Change(initial with { Generation = initial.Generation + 1 });
        notifications.Publish(new DatabaseProfileChangedNotification(
            initial.ActiveProfileId,
            initial.ActiveFingerprint,
            initial.ActiveProfileId!.Value,
            initial.ActiveFingerprint!,
            initial.Generation + 1));

        Assert.True(lease.CancellationToken.IsCancellationRequested);
        var result = lease.EnsureCurrent();
        Assert.True(result.IsFailure);
        Assert.Equal(LlmChatErrorCodes.RuntimeProfileChanged, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task Profile_switch_during_dispatch_cancels_the_invocation_and_prevents_assistant_commit()
    {
        var initial = ProviderRuntimeTestData.RuntimeIdentity;
        var state = new MutableDatabaseRuntimeState(initial);
        var notifications = new TestDatabaseSwitchNotificationService();
        var scope = new LlmChatOperationScopeAccessor();
        var dispatchStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var innerStore = new InMemoryLlmConversationStore();
        var store = new ProfileFencedLlmConversationStore(innerStore, state, scope);
        var invocation = new DelegatingInvocationPort(async (request, cancellationToken) =>
        {
            dispatchStarted.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new LlmInvocationResult(request.Model, "must not commit", LlmUsage.Zero);
        });
        var fencedInvocation = new ProfileFencedLlmChatInvocationPort(invocation, state, scope);
        var generic = new LlmConversationService(
            fencedInvocation,
            store,
            new RecencyBoundedContextWindowPolicy(),
            TimeProvider.System);
        var provider = ProviderRuntimeTestData.CreateProvider();
        var engine = new LlmChatConversationEngine(
            generic,
            fencedInvocation,
            ProviderRuntimeTestData.CreateResolver(provider),
            new DatabaseProfileLlmChatRuntimeLeaseFactory(state, notifications),
            scope);
        var definitionId = new LlmChatDefinitionId(Guid.NewGuid());
        var revision = ProviderRuntimeTestData.CreateRevision(definitionId, 1, provider, null);
        var definition = ProviderRuntimeTestData.CreateDefinition(
            definitionId,
            1,
            LlmChatDefinitionStatus.Active);
        var conversationId = LlmChatConversationId.New();
        var created = await engine.CreateAsync(conversationId, revision, "title");
        var send = engine.SendAsync(
            conversationId,
            LlmChatOperationId.New(),
            definition,
            revision,
            "hello",
            created.TranscriptRevision);
        await dispatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        state.Change(initial with { Generation = initial.Generation + 1 });
        notifications.Publish(new DatabaseProfileChangedNotification(
            initial.ActiveProfileId,
            initial.ActiveFingerprint,
            initial.ActiveProfileId!.Value,
            initial.ActiveFingerprint!,
            initial.Generation + 1));

        await Assert.ThrowsAsync<LlmChatRuntimeProfileChangedException>(() => send);
        var stored = await innerStore.TryGetAsync(conversationId.Value);
        Assert.NotNull(stored);
        Assert.DoesNotContain(stored.Entries, entry => entry.Role == LlmMessageRole.Assistant);
    }

    [Fact]
    public async Task Stale_generation_is_rejected_before_conversation_store_mutation()
    {
        var initial = ProviderRuntimeTestData.RuntimeIdentity;
        var state = new MutableDatabaseRuntimeState(initial);
        var scope = new LlmChatOperationScopeAccessor();
        var inner = new InMemoryLlmConversationStore();
        var fenced = new ProfileFencedLlmConversationStore(inner, state, scope);
        var provider = ProviderRuntimeTestData.CreateProvider();
        var now = DateTimeOffset.UtcNow;
        var document = new LlmConversationDocument(
            Guid.NewGuid(),
            "title",
            LlmConversationProviderSnapshot.FromProfile(provider, "model-fast"),
            now,
            now,
            0,
            []);
        using var operation = scope.Push(new LlmChatOperationExecutionContext(
            LlmChatOperationId.New(),
            new LlmChatRuntimeIdentity(
                initial.ActiveProfileId!.Value,
                initial.ActiveFingerprint!,
                initial.Generation)));
        state.Change(initial with { Generation = initial.Generation + 1 });

        await Assert.ThrowsAsync<LlmChatRuntimeProfileChangedException>(() => fenced.CreateAsync(document));

        Assert.Null(await inner.TryGetAsync(document.ConversationId));
    }

    [Fact]
    public async Task Result_returned_after_generation_change_is_rejected_before_assistant_commit()
    {
        var initial = ProviderRuntimeTestData.RuntimeIdentity;
        var state = new MutableDatabaseRuntimeState(initial);
        var scope = new LlmChatOperationScopeAccessor();
        var inner = new DelegatingInvocationPort((_, _) =>
        {
            state.Change(initial with { Generation = initial.Generation + 1 });
            return Task.FromResult(new LlmInvocationResult("model", "must not commit", new LlmUsage(2, 3)));
        });
        var fenced = new ProfileFencedLlmChatInvocationPort(inner, state, scope);
        var store = new InMemoryLlmConversationStore();
        var service = new LlmConversationService(
            fenced,
            store,
            new RecencyBoundedContextWindowPolicy(),
            TimeProvider.System);
        var provider = ProviderRuntimeTestData.CreateProvider();
        var conversation = await service.StartAsync(new LlmConversationStartRequest(provider, "model"));
        using var operation = scope.Push(new LlmChatOperationExecutionContext(
            LlmChatOperationId.New(),
            new LlmChatRuntimeIdentity(
                initial.ActiveProfileId!.Value,
                initial.ActiveFingerprint!,
                initial.Generation)));

        await Assert.ThrowsAsync<LlmChatRuntimeProfileChangedException>(() => service.SendAsync(
            new LlmConversationTurnRequest(
                conversation.ConversationId,
                conversation.TranscriptRevision,
                "hello",
                provider,
                "model")));

        var stored = await store.TryGetAsync(conversation.ConversationId);
        Assert.NotNull(stored);
        Assert.DoesNotContain(stored.Entries, entry => entry.Role == LlmMessageRole.Assistant);
    }
}

public sealed class LlmChatDefinitionRevisionExecutionTests
{
    [Fact]
    public async Task Turn_uses_the_pinned_revision_settings_including_explicit_effort()
    {
        var provider = ProviderRuntimeTestData.CreateProvider();
        var state = new MutableDatabaseRuntimeState(ProviderRuntimeTestData.RuntimeIdentity);
        var notifications = new TestDatabaseSwitchNotificationService();
        var scope = new LlmChatOperationScopeAccessor();
        var capture = new DelegatingInvocationPort((request, _) =>
        {
            ProviderRuntimeTestData.CapturedRequest = request;
            return Task.FromResult(new LlmInvocationResult(request.Model, "answer", new LlmUsage(2, 3)));
        });
        var fenced = new ProfileFencedLlmChatInvocationPort(capture, state, scope);
        var generic = new LlmConversationService(
            fenced,
            new InMemoryLlmConversationStore(),
            new RecencyBoundedContextWindowPolicy(),
            TimeProvider.System);
        var engine = new LlmChatConversationEngine(
            generic,
            fenced,
            ProviderRuntimeTestData.CreateResolver(provider),
            new DatabaseProfileLlmChatRuntimeLeaseFactory(state, notifications),
            scope);
        var definitionId = new LlmChatDefinitionId(Guid.NewGuid());
        var revision = ProviderRuntimeTestData.CreateRevision(
            definitionId,
            1,
            provider,
            AgentReasoningEffortLevel.None);
        var definition = ProviderRuntimeTestData.CreateDefinition(
            definitionId,
            currentRevision: 2,
            LlmChatDefinitionStatus.Active);
        var conversationId = LlmChatConversationId.New();
        var created = await engine.CreateAsync(conversationId, revision, "title");

        await engine.SendAsync(
            conversationId,
            LlmChatOperationId.New(),
            definition,
            revision,
            "hello",
            created.TranscriptRevision);

        Assert.NotNull(ProviderRuntimeTestData.CapturedRequest);
        Assert.Equal("model-fast", ProviderRuntimeTestData.CapturedRequest.Model);
        Assert.Equal(AgentReasoningEffortLevel.None, ProviderRuntimeTestData.CapturedRequest.Settings!.ThinkingEffort);
    }

    [Theory]
    [InlineData(LlmChatDefinitionStatus.Suspended)]
    [InlineData(LlmChatDefinitionStatus.Archived)]
    public async Task Suspended_or_archived_definition_blocks_dispatch(LlmChatDefinitionStatus status)
    {
        var provider = ProviderRuntimeTestData.CreateProvider();
        var state = new MutableDatabaseRuntimeState(ProviderRuntimeTestData.RuntimeIdentity);
        var notifications = new TestDatabaseSwitchNotificationService();
        var scope = new LlmChatOperationScopeAccessor();
        var invocationCount = 0;
        var invocation = new DelegatingInvocationPort((_, _) =>
            {
                invocationCount++;
                return Task.FromResult(new LlmInvocationResult("model-fast", "answer", LlmUsage.Zero));
            });
        var generic = new LlmConversationService(
            invocation,
            new InMemoryLlmConversationStore(),
            new RecencyBoundedContextWindowPolicy(),
            TimeProvider.System);
        var engine = new LlmChatConversationEngine(
            generic,
            invocation,
            ProviderRuntimeTestData.CreateResolver(provider),
            new DatabaseProfileLlmChatRuntimeLeaseFactory(state, notifications),
            scope);
        var definitionId = new LlmChatDefinitionId(Guid.NewGuid());
        var revision = ProviderRuntimeTestData.CreateRevision(definitionId, 1, provider, null);
        var definition = ProviderRuntimeTestData.CreateDefinition(definitionId, 1, status);
        var conversationId = LlmChatConversationId.New();
        var created = await engine.CreateAsync(conversationId, revision, "title");

        var exception = await Assert.ThrowsAsync<LlmChatConversationEngineException>(() => engine.SendAsync(
            conversationId,
            LlmChatOperationId.New(),
            definition,
            revision,
            "hello",
            created.TranscriptRevision));

        Assert.Equal(LlmChatErrorCodes.DefinitionNotActive, exception.Code);
        Assert.Equal(0, invocationCount);
    }
}

internal static class ProviderRuntimeTestData
{
    public static DatabaseRuntimeSnapshot RuntimeIdentity { get; } = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        "fingerprint",
        4);

    public static LlmInvocationRequest? CapturedRequest { get; set; }

    public static ProviderProfile CreateProvider(string name = "Provider")
        => new(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            name,
            ProviderKind.AzureOpenAi,
            "https://provider.invalid",
            "PROVIDER_KEY",
            "model-fast",
            ProviderTransportKind.Responses,
            true,
            true,
            true,
            false,
            false,
            AgentThinkingEffortPolicy.WriteProviderDefault("{}", AgentReasoningEffortLevel.Low),
            "",
            "Healthy",
            null,
            ["model-fast", "model-deep"])
        {
            ModelThinkingEffortCapabilities =
            [
                new ProviderModelThinkingEffortCapability(
                    "model-fast",
                    AgentThinkingEffortSupportStatus.Supported,
                    AgentThinkingEffortCapabilitySource.Discovered,
                    [AgentReasoningEffortLevel.None, AgentReasoningEffortLevel.Low],
                    ControlMode: AgentThinkingEffortControlMode.EffortLevels),
                new ProviderModelThinkingEffortCapability(
                    "model-deep",
                    AgentThinkingEffortSupportStatus.Supported,
                    AgentThinkingEffortCapabilitySource.Discovered,
                    [AgentReasoningEffortLevel.Low, AgentReasoningEffortLevel.Medium, AgentReasoningEffortLevel.High],
                    ControlMode: AgentThinkingEffortControlMode.EffortLevels)
            ]
        };

    public static CanonicalLlmChatProviderResolver CreateResolver(ProviderProfile provider)
        => new(new StaticProviderSource(provider), new ProviderModelCapabilityResolver());

    public static ProviderBackedLlmInvocationAdapter CreateInvocationAdapter(IProviderChatCompletionDriver driver)
    {
        var factory = new AgentProviderDriverRegistryBuilder()
            .AddDriver(driver)
            .Build();
        var store = new ProviderProfileRuntimeDescriptorStore();
        var pool = new ProviderRuntimePool(store, new ProviderRuntimeHandleFactory(factory));
        return new ProviderBackedLlmInvocationAdapter(store, pool);
    }

    public static LlmChatDefinitionRevision CreateRevision(
        LlmChatDefinitionId definitionId,
        int revision,
        ProviderProfile provider,
        AgentReasoningEffortLevel? effort)
        => new(
            definitionId,
            new LlmChatDefinitionRevisionNumber(revision),
            "Definition",
            "",
            "",
            "System prompt",
            provider.Id,
            provider.Kind,
            provider.Name,
            "model-fast",
            new LlmModelSettings(0.2, "{}") { ThinkingEffort = effort },
            TimeSpan.FromMinutes(2),
            null,
            DateTimeOffset.UtcNow,
            "test");

    public static LlmChatDefinition CreateDefinition(
        LlmChatDefinitionId definitionId,
        int currentRevision,
        LlmChatDefinitionStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        return new LlmChatDefinition(
            definitionId,
            "Definition",
            "",
            "",
            status,
            new LlmChatDefinitionRevisionNumber(currentRevision),
            now,
            now,
            0);
    }

    private sealed class StaticProviderSource(ProviderProfile provider) : IProviderRuntimeProfileSource
    {
        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProviderProfile>>([provider]);

        public Task<ProviderProfile?> GetProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
            => Task.FromResult(provider.Id == providerId ? provider : null);
    }
}

internal sealed class MutableDatabaseRuntimeState(DatabaseRuntimeSnapshot snapshot) : IDatabaseRuntimeState
{
    private DatabaseRuntimeSnapshot current = snapshot;

    public DatabaseRuntimeSnapshot GetSnapshot()
        => current;

    public void MarkCurrentProfile(ResolvedDatabaseProfile profile)
        => throw new NotSupportedException();

    public void Change(DatabaseRuntimeSnapshot changed)
        => current = changed;
}

internal sealed class TestDatabaseSwitchNotificationService : IDatabaseSwitchNotificationService
{
    private EventHandler<DatabaseProfileChangedNotification>? changed;

    public event EventHandler<DatabaseProfileChangedNotification>? Changed
    {
        add => changed += value;
        remove => changed -= value;
    }

    public int SubscriberCount => changed?.GetInvocationList().Length ?? 0;

    public void Publish(DatabaseProfileChangedNotification notification)
        => changed?.Invoke(this, notification);
}

internal sealed class DelegatingInvocationPort(
    Func<LlmInvocationRequest, CancellationToken, Task<LlmInvocationResult>> invoke) : ILlmInvocationPort
{
    public Task<LlmInvocationResult> InvokeAsync(
        LlmInvocationRequest request,
        CancellationToken cancellationToken = default)
        => invoke(request, cancellationToken);
}

internal sealed class DelegatingChatDriver(
    Func<ProviderChatCompletionRequest, CancellationToken, Task<ProviderChatCompletionResult>> complete) :
    IProviderChatCompletionDriver
{
    public ProviderKind ProviderKind => ProviderKind.AzureOpenAi;

    public IReadOnlySet<AgentProviderCapabilityKind> Capabilities { get; } =
        new HashSet<AgentProviderCapabilityKind> { AgentProviderCapabilityKind.ChatCompletion };

    public ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query)
        => ProviderDispatchLimits.Unbatched(TimeSpan.FromSeconds(5));

    public Task<ProviderChatCompletionResult> CompleteChatAsync(
        ProviderChatCompletionRequest request,
        CancellationToken cancellationToken = default)
        => complete(request, cancellationToken);
}
