using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class MafContextToolAttachmentTests {
    [Fact]
    public void Declaration_is_immutable_and_rejects_ambiguous_names() {
        MafContextToolDeclaration[] original = [new(AgentSkillsProvider.LoadSkillToolName, false)];
        var registration = new MafContextToolRegistration(original);
        original[0] = new(AgentSkillsProvider.RunSkillScriptToolName, true);
        Assert.Equal(AgentSkillsProvider.LoadSkillToolName, Assert.Single(registration.ToolNames));
        Assert.Throws<ArgumentException>(() => new MafContextToolRegistration([]));
        Assert.Throws<ArgumentException>(() => new MafContextToolRegistration([new("", false)]));
        Assert.Throws<ArgumentException>(() => new MafContextToolRegistration([
            new(AgentSkillsProvider.LoadSkillToolName, false), new(AgentSkillsProvider.LoadSkillToolName, true)]));
    }

    [Fact]
    public void Late_attachment_rejects_unknown_duplicate_and_native_tools() {
        var function = AIFunctionFactory.Create(() => "skill", AgentSkillsProvider.LoadSkillToolName);
        var registration = new MafContextToolRegistration([new(function.Name, false)]);
        Assert.Same(function, Assert.Single(registration.RequireTools([function])));
        Assert.Empty(registration.RequireTools([]));
        Assert.Throws<AgentToolAdmissionException>(() => registration.RequireTools([function, function]));
        Assert.Throws<AgentToolAdmissionException>(() => registration.RequireTools([
            AIFunctionFactory.Create(() => "foreign", AgentSkillsProvider.ReadSkillResourceToolName)]));
        Assert.Throws<AgentToolAdmissionException>(() => registration.RequireTools([new HostedWebSearchTool()]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Late_attachment_requires_the_original_approval_marker(bool required) {
        var function = AIFunctionFactory.Create(() => "skill", AgentSkillsProvider.RunSkillScriptToolName);
        var registration = new MafContextToolRegistration([new(function.Name, required)]);
        var approved = new ApprovalRequiredAIFunction(function);
        Assert.Same(required ? approved : function, Assert.Single(registration.RequireTools([required ? approved : function])));
        Assert.Throws<AgentToolAdmissionException>(() => registration.RequireTools([required ? function : approved]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Wrapper_preserves_the_installed_SDK_context_schemas_and_approval_objects(bool requireApproval) {
        var skill = new AgentInlineSkill("retained-skill", "Retained description", "Retained instructions")
            .AddResource("reference", "Retained resource")
            .AddScript("run", (string value) => value);
        using var baseline = Skills(skill, requireApproval);
        using var inner = Skills(skill, requireApproval);
        using var wrapped = new MafSkillsContextProvider(inner, Registration(requireApproval));
        using var client = new UnusedClient();
        var agent = new ChatClientAgent(client);
#pragma warning disable MAAI001
        var context = new AIContextProvider.InvokingContext(agent, await agent.CreateSessionAsync(), new AIContext());
#pragma warning restore MAAI001
        var expected = await baseline.InvokingAsync(context);
        var actual = await wrapped.InvokingAsync(context);
        Assert.Equal(expected.Instructions, actual.Instructions);
        Assert.Equal(expected.Messages?.Select(message => message.Text), actual.Messages?.Select(message => message.Text));
        Assert.Equal(baseline.StateKeys, wrapped.StateKeys);
        Assert.Same(inner, wrapped.GetService<AgentSkillsProvider>());
        var expectedTools = expected.Tools!.OfType<AIFunction>().ToArray();
        var actualTools = actual.Tools!.OfType<AIFunction>().ToArray();
        Assert.Equal(3, actualTools.Length);
        Assert.Equal(expectedTools.Select(Contract), actualTools.Select(Contract));
        using var services = new ServiceCollection().BuildServiceProvider();
        Assert.Equal("Retained resource", (await actualTools.Single(tool => tool.Name == AgentSkillsProvider.ReadSkillResourceToolName)
            .InvokeAsync(new() { Services = services, ["skillName"] = "retained-skill", ["resourceName"] = "reference" }))?.ToString());
        static object Contract(AIFunction tool) => new {
            tool.Name, tool.Description, Schema = tool.JsonSchema.GetRawText(),
            Result = tool.ReturnJsonSchema?.GetRawText(), Approval = tool.GetService<ApprovalRequiredAIFunction>() is not null
        };
    }

    [Fact]
    public void Context_contracts_activate_durable_tools_before_the_SDK_has_created_functions() {
        var state = new RuntimeCapabilityState();
        Assert.False(state.HasToolContracts);
        state.ContextToolRegistrations.Add(Registration(true));
        Assert.True(state.HasToolContracts);
        Assert.Empty(state.Tools);
    }

    [Fact]
    public void Empty_context_contracts_preserve_existing_fingerprint_bytes() {
        AITool[] tools = [AIFunctionFactory.Create(() => "content", AgentToolInvocationPolicyMetadata.LoadSkill)];
        Assert.Equal(MafToolsetFingerprint.ComputeContractFingerprint(tools),
            MafToolsetFingerprint.ComputeContractFingerprint(tools, contextTools: []));
    }

    [Fact]
    public void Context_fingerprint_binds_names_classification_and_approval_without_guessing_SDK_schemas() {
        var original = Registration(true).Declarations;
        var fingerprint = MafToolsetFingerprint.ComputeContractFingerprint([], contextTools: original);
        Assert.Equal(fingerprint, MafToolsetFingerprint.ComputeContractFingerprint([], contextTools: original.Reverse()));
        Assert.NotEqual(fingerprint, MafToolsetFingerprint.ComputeContractFingerprint([], contextTools: Registration(false).Declarations));
        Assert.NotEqual(fingerprint, MafToolsetFingerprint.ComputeContractFingerprint([]));
        Assert.NotEqual(fingerprint, MafToolsetFingerprint.ComputeContractFingerprint([], contextTools: original.Take(1)));
    }

    private static MafContextToolRegistration Registration(bool approval) => new([
        new(AgentSkillsProvider.LoadSkillToolName, false), new(AgentSkillsProvider.ReadSkillResourceToolName, false),
        new(AgentSkillsProvider.RunSkillScriptToolName, approval)]);

    private static AgentSkillsProvider Skills(AgentSkill skill, bool approval) => new AgentSkillsProviderBuilder()
        .UseSkills(skill).UseOptions(options => {
            options.DisableLoadSkillApproval = true;
            options.DisableReadSkillResourceApproval = true;
            options.DisableRunSkillScriptApproval = !approval;
        }).Build();

    private sealed class UnusedClient : IChatClient {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("No model request is part of SDK context inspection.");
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("No model request is part of SDK context inspection.");
        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
        public void Dispose() { }
    }
}
