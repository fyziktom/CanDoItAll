using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.Agents.SimpleChats;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class HrSimpleChatRequestScopedInputTests {
    [Fact]
    public async Task Attachment_turn_keeps_all_assigned_tools_and_exposes_a_precise_sensitive_operation_denial() {
        var fixture = new HrSimpleChatTestFixture();
        var context = fixture.Context with { ToolAdmissionSupport = AgentToolAdmissionSupport.RequestScopedInput };
        Assert.Equal(7, (await fixture.Provider.CreateToolsAsync(context, default)).Count);
        var metadata = fixture.Provider.GetToolMetadata(context);
        Assert.Equal(4, metadata.Count(item => item.Unavailability is not null));
        Assert.Equal(3, metadata.Count(item => item.Unavailability is null));
        Assert.All(metadata.Where(item => item.Unavailability is not null), item =>
            Assert.Equal("hr-simple-chat.request-scoped-input", item.Unavailability!.Code));
        Assert.NotEmpty((await fixture.Service.SearchAsync(context, new(), default)).Items);
        Assert.Equal(0, fixture.Receipts.Calls);
    }

    [Theory]
    [InlineData(HrSimpleChatOperation.Settings)]
    [InlineData(HrSimpleChatOperation.Create)]
    [InlineData(HrSimpleChatOperation.Update)]
    [InlineData(HrSimpleChatOperation.Status)]
    public async Task Attachment_sensitive_operation_is_denied_before_owner_read_write_or_receipt_dispatch(HrSimpleChatOperation operation) {
        var fixture = new HrSimpleChatTestFixture();
        var context = fixture.Context with { ToolAdmissionSupport = AgentToolAdmissionSupport.RequestScopedInput };
        var version = new HrSimpleChatDefinitionVersion(HrSimpleChatTestFixture.DefinitionId, 1, 0);
        Func<Task> invoke = operation switch {
            HrSimpleChatOperation.Settings => () => fixture.Service.SettingsAsync(context, version, default),
            HrSimpleChatOperation.Create => () => fixture.Service.CreateAsync(context, HrSimpleChatTestFixture.CreateCommand(), default),
            HrSimpleChatOperation.Update => () => fixture.Service.UpdateAsync(context, new(version, HrSimpleChatTestFixture.CreateCommand()), default),
            HrSimpleChatOperation.Status => () => fixture.Service.ChangeStatusAsync(context, new(version, LlmChatDefinitionStatus.Archived), default),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        var failure = await Assert.ThrowsAsync<HrSimpleChatAdministrationException>(invoke);
        Assert.Equal("hr-simple-chat.request-scoped-input", failure.Code);
        Assert.Equal(AgentToolEffectState.NotCommitted, ((IAgentToolFailureEffectEvidence)failure).EffectState);
        Assert.Equal(0, fixture.Definitions.Reads);
        Assert.Equal(0, fixture.Definitions.Writes);
        Assert.Equal(0, fixture.Receipts.Calls);
        Assert.Null(fixture.LeaseFactory.Active);
    }
}
