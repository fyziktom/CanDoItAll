using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace CanDoItAll.Tests.Components;

public sealed class HrAgentCompositionTests
{
    [Fact]
    public async Task Application_composition_seeds_identity_bound_HR_governance_agent_and_provider()
    {
        await using var environment = CanDoItAllTestEnvironment.Create("hr-agent-composition");
        var profile = environment.CreateInMemoryProfile("primary");
        var configuration = TestApplicationBootstrap.BuildConfiguration(profile);
        var services = new ServiceCollection();
        TestApplicationBootstrap.ConfigureDefaultServices(
            services,
            configuration,
            environment.CreateHostEnvironment("CanDoItAll.HrAgentCompositionTests"));
        await using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using var scope = serviceProvider.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspace = workspaceFactory.GetOrganizationWorkspaceService();
        var agents = await workspace.ListAgentsAsync(includeTemplates: true);
        var providers = await workspace.ListProvidersAsync();
        var capabilities = await workspace.ListCapabilitiesAsync();
        var agent = Assert.Single(agents, HrAgentIdentity.Matches);
        var expectedCapabilityKeys = HrAgentCapabilityKeys.ToolNameToCapabilityKey.Values
            .Append(HrAgentCapabilityKeys.GovernanceSkill)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        var imageAccess = AgentImageGenerationAccessMetadata.Read(agent.ConfigurationJson);
        var memoryAccess = AgentMemoryAccessMetadata.Read(agent.ConfigurationJson);
        var imageProvider = Assert.Single(
            providers,
            provider => provider.Id == imageAccess.PreferredProviderProfileId);
        var runtimeProviders = scope.ServiceProvider
            .GetServices<IAgentRuntimeToolProvider>()
            .OfType<HrAgentRuntimeToolProvider>()
            .ToArray();
        var chatProvider = Assert.Single(
            providers,
            provider => provider.IsEnabled &&
                        provider.Purpose == ProviderProfilePurpose.Chat &&
                        string.Equals(provider.Name, "OpenAI default", StringComparison.Ordinal));

        Assert.Equal(HrAgentIdentity.AgentId, agent.Id);
        Assert.Equal(HrAgentIdentity.TemplateKey, agent.TemplateKey);
        Assert.False(agent.IsTemplate);
        Assert.Equal(AgentLifecycleStatus.Active, agent.Status);
        Assert.True(agent.Permissions.CanUseTools);
        Assert.True(agent.Permissions.CanObserveOtherAgents);
        Assert.True(agent.Permissions.RequiresApprovalForExternalCalls);
        Assert.False(agent.Permissions.AutoApproveExternalCallsByDefault);
        Assert.Equal(
            expectedCapabilityKeys,
            agent.Capabilities
                .Select(capability => capability.CapabilityKey)
                .OrderBy(key => key, StringComparer.Ordinal));
        Assert.True(imageAccess.CanGenerateImages);
        Assert.Equal(ProviderProfilePurpose.ImageGeneration, imageProvider.Purpose);
        Assert.Equal("gpt-image-1-mini", imageAccess.DefaultModel);
        Assert.False(imageAccess.CanStoreImagesAsProjectAssets);
        Assert.False(memoryAccess.CanUseMemoryTools);
        Assert.Contains(MemorySourceScope.Crm, memoryAccess.AllowedSourceScopes);
        var runtimeProvider = Assert.Single(runtimeProviders);
        var runtimeContext = CreateRuntimeToolContext(agent, chatProvider, capabilities);
        var runtimeTools = await runtimeProvider.CreateToolsAsync(runtimeContext, CancellationToken.None);
        var spoofedContext = CreateRuntimeToolContext(
            agent with { Id = Guid.NewGuid() },
            chatProvider,
            capabilities);

        Assert.Equal(
            HrAgentCapabilityKeys.ToolNameToCapabilityKey.Keys.OrderBy(name => name, StringComparer.Ordinal),
            runtimeTools.Select(tool => tool.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Empty(await runtimeProvider.CreateToolsAsync(spoofedContext, CancellationToken.None));

        var administration = new HrAgentAdministrationService(
            workspace,
            NullLogger<HrAgentAdministrationService>.Instance);
        var createResult = await administration.CreateAsync(
            HrAgentIdentity.AgentId,
            new HrAgentCreateInput(
                "Temporary specialist",
                "Focused validation specialist",
                "Validates one focused concern.",
                "Inspect the supplied concern, report evidence, and stop.",
                chatProvider.Id,
                chatProvider.DefaultModel),
            CancellationToken.None);
        var createdAgent = Assert.Single(
            await workspace.ListAgentsAsync(includeTemplates: true),
            candidate => candidate.Id == createResult.AgentId);

        Assert.Equal(AgentLifecycleStatus.Draft, createdAgent.Status);
        Assert.StartsWith("hr-created-", createdAgent.TemplateKey, StringComparison.Ordinal);
        Assert.Empty(createdAgent.Permissions.NormalizedAllowedSecrets);
        Assert.Empty(createdAgent.Capabilities);
        Assert.Contains(createResult.Warnings, warning => warning.Contains("No capabilities", StringComparison.Ordinal));
        await Assert.ThrowsAsync<AgentCatalogConcurrencyException>(() => administration.UpdateAsync(
            HrAgentIdentity.AgentId,
            new HrAgentSettingsUpdateInput(
                createdAgent.Id,
                createdAgent.UpdatedAtUtc.AddTicks(-1),
                Summary: "Stale overwrite"),
            CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => administration.UpdateAsync(
            HrAgentIdentity.AgentId,
            new HrAgentSettingsUpdateInput(
                HrAgentIdentity.AgentId,
                agent.UpdatedAtUtc,
                Summary: "Unauthorized self change"),
            CancellationToken.None));

        var oversizedGenerator = new StubImageGenerationService(
            new byte[AgentAvatarImagePolicy.MaxAvatarBytes + 1]);
        var oversizedAvatarService = new HrAgentAvatarGenerationService(
            workspace,
            oversizedGenerator,
            NullLogger<HrAgentAvatarGenerationService>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => oversizedAvatarService.GenerateAsync(
            HrAgentIdentity.AgentId,
            new HrAgentAvatarGenerateInput(
                createdAgent.Id,
                createdAgent.UpdatedAtUtc,
                "Abstract blue validation compass"),
            CancellationToken.None));
        var afterFailedAvatar = Assert.Single(
            await workspace.ListAgentsAsync(includeTemplates: true),
            candidate => candidate.Id == createdAgent.Id);
        Assert.True(string.IsNullOrWhiteSpace(afterFailedAvatar.AvatarImageUrl));

        var corruptAvatarService = new HrAgentAvatarGenerationService(
            workspace,
            new StubImageGenerationService([0xff, 0xd8, 0xff, 0xd9]),
            NullLogger<HrAgentAvatarGenerationService>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => corruptAvatarService.GenerateAsync(
            HrAgentIdentity.AgentId,
            new HrAgentAvatarGenerateInput(
                createdAgent.Id,
                afterFailedAvatar.UpdatedAtUtc,
                "Abstract blue validation compass"),
            CancellationToken.None));

        var validSquareJpeg = Convert.FromBase64String(ValidSquareJpegBase64);
        var mimeMismatchAvatarService = new HrAgentAvatarGenerationService(
            workspace,
            new StubImageGenerationService(validSquareJpeg, "image/png"),
            NullLogger<HrAgentAvatarGenerationService>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => mimeMismatchAvatarService.GenerateAsync(
            HrAgentIdentity.AgentId,
            new HrAgentAvatarGenerateInput(
                createdAgent.Id,
                afterFailedAvatar.UpdatedAtUtc,
                "Abstract blue validation compass"),
            CancellationToken.None));

        var nonSquareAvatarService = new HrAgentAvatarGenerationService(
            workspace,
            new StubImageGenerationService(
                Convert.FromBase64String(NonSquareJpegBase64)),
            NullLogger<HrAgentAvatarGenerationService>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => nonSquareAvatarService.GenerateAsync(
            HrAgentIdentity.AgentId,
            new HrAgentAvatarGenerateInput(
                createdAgent.Id,
                afterFailedAvatar.UpdatedAtUtc,
                "Abstract blue validation compass"),
            CancellationToken.None));

        var validGenerator = new StubImageGenerationService(validSquareJpeg);
        var avatarService = new HrAgentAvatarGenerationService(
            workspace,
            validGenerator,
            NullLogger<HrAgentAvatarGenerationService>.Instance);
        var avatarResult = await avatarService.GenerateAsync(
            HrAgentIdentity.AgentId,
            new HrAgentAvatarGenerateInput(
                createdAgent.Id,
                afterFailedAvatar.UpdatedAtUtc,
                "Abstract blue validation compass",
                OutputCompression: 40),
            CancellationToken.None);
        var afterValidAvatar = Assert.Single(
            await workspace.ListAgentsAsync(includeTemplates: true),
            candidate => candidate.Id == createdAgent.Id);

        Assert.StartsWith("data:image/jpeg;base64,", afterValidAvatar.AvatarImageUrl, StringComparison.Ordinal);
        Assert.Equal(validSquareJpeg.Length, avatarResult.ContentLength);
        Assert.Equal(40, validGenerator.LastRequest?.OutputCompression);
        Assert.Equal(AgentGeneratedImageFormat.Jpeg, validGenerator.LastRequest?.Format);
        Assert.Equal("1024x1024", validGenerator.LastRequest?.Size);
        var safeSettings = await administration.GetSettingsAsync(createdAgent.Id, CancellationToken.None);

        Assert.True(safeSettings.Avatar.IsPresent);
        Assert.Equal(HrAgentAvatarKind.EmbeddedData, safeSettings.Avatar.Kind);
        Assert.Equal("image/jpeg", safeSettings.Avatar.ContentType);
        Assert.Equal(validSquareJpeg.Length, safeSettings.Avatar.ByteCount);

        var processRunId = Guid.NewGuid();
        var firstAttempt = CreateExecutionRun(
            createdAgent.Id,
            processRunId,
            "validate-output",
            ExecutionState.Failed,
            RunOutcome.Failed,
            DateTimeOffset.UtcNow);
        var secondAttempt = CreateExecutionRun(
            createdAgent.Id,
            processRunId,
            "validate-output",
            ExecutionState.Completed,
            RunOutcome.Succeeded,
            firstAttempt.CreatedAtUtc.AddMinutes(1));
        var managerReview = CreateExecutionRun(
            createdAgent.Id,
            processRunId,
            string.Empty,
            ExecutionState.Completed,
            RunOutcome.Succeeded,
            firstAttempt.CreatedAtUtc.AddMinutes(2)) with
        {
            SourceKind = HrAgentExecutionLineage.ManagerReviewSourceKind
        };
        var additionalAttempts = Enumerable.Range(0, 30)
            .Select(index => CreateExecutionRun(
                createdAgent.Id,
                processRunId,
                "bulk-validation",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                firstAttempt.CreatedAtUtc.AddMinutes(3 + index)))
            .ToArray();
        var executionStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionStore>();
        await executionStore.UpdateExecutionAsync(state => state with
        {
            ExecutionRuns = state.ExecutionRuns
                .Concat([firstAttempt, secondAttempt, managerReview])
                .Concat(additionalAttempts)
                .ToArray()
        });
        var processReview = new HrAgentProcessReviewService(
            executionStore,
            scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>(),
            workspace,
            NullLogger<HrAgentProcessReviewService>.Instance);
        var history = await processReview.GetHistoryAsync(
            new HrAgentProcessHistoryInput(createdAgent.Id),
            CancellationToken.None);
        var processReviewItem = Assert.Single(history.ProcessRuns);
        var failedAttempt = Assert.Single(
            processReviewItem.Attempts,
            attempt => attempt.Outcome == RunOutcome.Failed);

        Assert.Equal(processRunId, processReviewItem.ProcessRunId);
        Assert.Equal(32, processReviewItem.AttemptCount);
        Assert.Equal(25, processReviewItem.ReturnedAttemptCount);
        Assert.True(processReviewItem.AttemptsTruncated);
        Assert.Equal(2, processReviewItem.RepeatedStepCount);
        Assert.Equal(1, processReviewItem.FailedAttemptCount);
        Assert.Equal(1, processReviewItem.ParticipantCount);
        Assert.Equal(1, processReviewItem.ReturnedParticipantCount);
        Assert.False(processReviewItem.ParticipantsTruncated);
        Assert.Contains("failedLogRecorded=False", failedAttempt.FailureEvidence, StringComparison.Ordinal);
        Assert.DoesNotContain("Private raw failure", failedAttempt.FailureEvidence, StringComparison.Ordinal);

        var largeAvatarBytes = new byte[AgentAvatarImagePolicy.MaxAvatarBytes];
        var largeAvatarPayload = Convert.ToBase64String(largeAvatarBytes);
        var largeAvatarEditor = await workspace.GetAgentEditorAsync(createdAgent.Id);
        largeAvatarEditor.ExpectedUpdatedAtUtc = afterValidAvatar.UpdatedAtUtc;
        largeAvatarEditor.AvatarImageUrl = $"data:image/png;base64,{largeAvatarPayload}";
        await workspace.SaveAgentAsync(largeAvatarEditor);
        var largeSafeSettings = await administration.GetSettingsAsync(createdAgent.Id, CancellationToken.None);
        var serializedSafeSettings = JsonSerializer.Serialize(largeSafeSettings);

        Assert.True(largeSafeSettings.Avatar.IsPresent);
        Assert.Equal(HrAgentAvatarKind.EmbeddedData, largeSafeSettings.Avatar.Kind);
        Assert.Equal("image/png", largeSafeSettings.Avatar.ContentType);
        Assert.Equal(largeAvatarBytes.Length, largeSafeSettings.Avatar.ByteCount);
        Assert.DoesNotContain("AvatarImageUrl", serializedSafeSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("base64", serializedSafeSettings, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(largeAvatarPayload[..64], serializedSafeSettings, StringComparison.Ordinal);
    }

    private static ExecutionRunRecord CreateExecutionRun(
        Guid agentId,
        Guid processRunId,
        string processStepId,
        ExecutionState state,
        RunOutcome outcome,
        DateTimeOffset createdAtUtc)
    {
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: agentId,
            ChatSessionId: null,
            Title: "HR process evidence",
            SourceKind: HrAgentExecutionLineage.ProcessStepSourceKind,
            SourceId: processStepId,
            CorrelationId: processRunId.ToString("D"),
            CausationId: string.Empty,
            RequestedBy: "test",
            RequestedByKind: "test",
            MetadataJson: "{}",
            InputSummary: string.Empty,
            ResultSummary: "Private raw failure should not leak",
            ProviderName: "OpenAI default",
            Model: "gpt-5-mini",
            State: state,
            Outcome: outcome,
            CreatedAtUtc: createdAtUtc,
            UpdatedAtUtc: createdAtUtc,
            StartedAtUtc: createdAtUtc,
            CompletedAtUtc: createdAtUtc,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: [],
            ProcessRunId: processRunId.ToString("D"),
            ProcessStepId: processStepId);
    }

    private static AgentRuntimeToolProviderContext CreateRuntimeToolContext(
        AgentDefinition agent,
        ProviderProfile provider,
        IReadOnlyList<CapabilityCatalogItem> capabilities)
    {
        return new AgentRuntimeToolProviderContext(
            agent,
            provider,
            capabilities,
            SuppressApprovalRequirements: false,
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            RuntimeSessionKey: "hr-composition-test",
            AgentRuntimeContextIntent.Empty,
            Tags: new Dictionary<string, string>());
    }

    private const string ValidSquareJpegBase64 =
        "/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCAAgACADASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwDEooor9dPyQKKKKACiiigAooooA//Z";

    private const string NonSquareJpegBase64 =
        "/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCABAACADASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD3iiiiv8+j+mwooooAKKKKACiiigAooooAKKKKACiiigAooooA/9k=";

    private sealed class StubImageGenerationService(
        byte[] imageBytes,
        string contentType = "image/jpeg") : IAgentImageGenerationService
    {
        public AgentImageGenerationRequest? LastRequest { get; private set; }

        public Task<AgentImageGenerationResult> GenerateAsync(
            AgentImageGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new AgentImageGenerationResult(
                request.Model,
                request.Format,
                [new AgentGeneratedImage(contentType, imageBytes)]));
        }
    }
}
