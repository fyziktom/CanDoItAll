using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Definitions;

namespace CanDoItAll.Tests.Unit;

public sealed class LlmChatDefinitionServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-14T12:00:00Z");

    [Fact]
    public async Task Create_and_update_resolve_provider_and_append_an_immutable_revision()
    {
        var repository = new InMemoryLlmChatDefinitionRepository();
        var resolver = new StubLlmChatProviderResolver();
        var service = CreateService(repository, resolver);
        var providerId = Guid.NewGuid();

        var created = await service.CreateAsync(new CreateLlmChatDefinitionCommand(
            "Support assistant",
            "Answers support questions",
            "https://example.test/support.png",
            "Be concise.",
            providerId,
            "gpt-5",
            new LlmModelSettings(0.2) { ThinkingEffort = AgentReasoningEffortLevel.High },
            TimeSpan.FromMinutes(2),
            null,
            "Initial"));

        Assert.True(created.IsSuccess);
        Assert.Equal(LlmChatDefinitionStatus.Draft, created.Value!.Definition.Status);
        Assert.Equal(1, created.Value.Revision.Revision.Value);
        Assert.Equal(ProviderKind.OpenAi, created.Value.Revision.ProviderKind);
        Assert.Equal(AgentReasoningEffortLevel.High, created.Value.Revision.Settings.ThinkingEffort);

        var updated = await service.UpdateAsync(new UpdateLlmChatDefinitionCommand(
            created.Value.Definition.Id,
            "Support assistant v2",
            "Updated",
            string.Empty,
            "Use short answers.",
            providerId,
            "gpt-5",
            new LlmModelSettings(0.1) { ThinkingEffort = AgentReasoningEffortLevel.Low },
            TimeSpan.FromMinutes(1),
            null,
            "Tune",
            created.Value.Definition.ConcurrencyToken));

        Assert.True(updated.IsSuccess);
        Assert.Equal(2, updated.Value!.Revision.Revision.Value);
        Assert.Equal(2, updated.Value.Definition.CurrentRevision.Value);
        Assert.Equal("Support assistant", created.Value.Definition.Name);
        Assert.Equal("Support assistant v2", updated.Value.Definition.Name);
        Assert.Equal(2, resolver.Requests.Count);
    }

    [Fact]
    public async Task Archived_definition_is_read_only_and_cannot_be_reactivated()
    {
        var repository = new InMemoryLlmChatDefinitionRepository();
        var service = CreateService(repository, new StubLlmChatProviderResolver());
        var created = await CreateDefinitionAsync(service);
        var archived = await service.ChangeStatusAsync(new ChangeLlmChatDefinitionStatusCommand(
            created.Definition.Id,
            LlmChatDefinitionStatus.Archived,
            created.Definition.ConcurrencyToken));

        var update = await service.UpdateAsync(new UpdateLlmChatDefinitionCommand(
            created.Definition.Id,
            "Changed",
            string.Empty,
            string.Empty,
            string.Empty,
            created.Revision.ProviderProfileId,
            created.Revision.Model,
            new LlmModelSettings(),
            null,
            null,
            "Should fail",
            archived.Value!.Definition.ConcurrencyToken));
        var reactivate = await service.ChangeStatusAsync(new ChangeLlmChatDefinitionStatusCommand(
            created.Definition.Id,
            LlmChatDefinitionStatus.Active,
            archived.Value.Definition.ConcurrencyToken));

        Assert.True(update.IsFailure);
        Assert.Contains(update.Errors, error => error.Code == LlmChatErrorCodes.DefinitionNotActive);
        Assert.True(reactivate.IsFailure);
        Assert.Contains(reactivate.Errors, error => error.Code == LlmChatErrorCodes.DefinitionNotActive);
    }

    [Fact]
    public async Task Suspended_definition_can_be_reactivated_but_blocks_conversation_start()
    {
        var repository = new InMemoryLlmChatDefinitionRepository();
        var service = CreateService(repository, new StubLlmChatProviderResolver());
        var created = await CreateDefinitionAsync(service);
        var active = await service.ChangeStatusAsync(new ChangeLlmChatDefinitionStatusCommand(
            created.Definition.Id,
            LlmChatDefinitionStatus.Active,
            created.Definition.ConcurrencyToken));
        var suspended = await service.ChangeStatusAsync(new ChangeLlmChatDefinitionStatusCommand(
            created.Definition.Id,
            LlmChatDefinitionStatus.Suspended,
            active.Value!.Definition.ConcurrencyToken));
        var reactivated = await service.ChangeStatusAsync(new ChangeLlmChatDefinitionStatusCommand(
            created.Definition.Id,
            LlmChatDefinitionStatus.Active,
            suspended.Value!.Definition.ConcurrencyToken));

        Assert.True(reactivated.IsSuccess);
        Assert.Equal(LlmChatDefinitionStatus.Active, reactivated.Value!.Definition.Status);
    }

    private static LlmChatDefinitionApplicationService CreateService(
        InMemoryLlmChatDefinitionRepository repository,
        StubLlmChatProviderResolver resolver)
        => new(repository, new InlineLlmChatUnitOfWork(), resolver, new FixedTimeProvider(Now));

    internal static async Task<LlmChatDefinitionDetails> CreateDefinitionAsync(
        LlmChatDefinitionApplicationService service)
    {
        var result = await service.CreateAsync(new CreateLlmChatDefinitionCommand(
            "Support assistant",
            string.Empty,
            string.Empty,
            "Be concise.",
            Guid.NewGuid(),
            "gpt-5",
            new LlmModelSettings(),
            null,
            null,
            "Initial"));
        return result.Value!;
    }
}
