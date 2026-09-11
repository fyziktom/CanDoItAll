using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class MafContextToolAttachmentTests {
    private const string OrdinaryToolName = "ordinary_context_read";
    private const string ForeignToolName = "foreign_context_read";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Installed_SDK_mixed_context_preserves_inherited_tools_and_isolates_its_owned_skill_functions(bool requireApproval) {
        var skill = new AgentInlineSkill("retained-skill", "Retained description", "Retained instructions")
            .AddResource("reference", "Retained resource")
            .AddScript("run", (string value) => value);
        var ordinary = AIFunctionFactory.Create(() => "Ordinary result", OrdinaryToolName);
        var hosted = new HostedWebSearchTool();
        using var baseline = Skills(skill, requireApproval);
        using var wrapped = new MafSkillsContextProvider(Skills(skill, requireApproval), Registration(requireApproval));
        using var client = new UnusedClient();
        var agent = new ChatClientAgent(client);
        var session = await agent.CreateSessionAsync();
#pragma warning disable MAAI001
        AIContextProvider.InvokingContext Context() => new(agent, session, new AIContext {
            Instructions = "Original caller instructions",
            Messages = [new ChatMessage(ChatRole.User, "Original caller message")],
            Tools = [ordinary, hosted]
        });
#pragma warning restore MAAI001
        var expected = await baseline.InvokingAsync(Context());
        var actual = await wrapped.InvokingAsync(Context());
        Assert.Equal(expected.Instructions, actual.Instructions);
        Assert.Equal(expected.Messages?.Select(message => message.Text), actual.Messages?.Select(message => message.Text));
        Assert.Contains("Original caller instructions", actual.Instructions, StringComparison.Ordinal);
        var tools = actual.Tools!.ToArray();
        Assert.Equal(5, tools.Length);
        Assert.Same(ordinary, tools[0]);
        Assert.Same(hosted, tools[1]);
        var contribution = Registration(requireApproval).RequireContribution([ordinary, hosted], tools);
        Assert.Equal(3, contribution.Length);
        Assert.DoesNotContain(contribution, tool => ReferenceEquals(tool, ordinary));
        Assert.Equal(expected.Tools!.OfType<AIFunction>().Select(Contract), tools.OfType<AIFunction>().Select(Contract));
        using var services = new ServiceCollection().BuildServiceProvider();
        Assert.Equal("Ordinary result", (await ordinary.InvokeAsync(new() { Services = services }))?.ToString());
        Assert.Equal("Retained resource", (await contribution.Single(tool => tool.Name == AgentSkillsProvider.ReadSkillResourceToolName)
            .InvokeAsync(new() { Services = services, ["skillName"] = "retained-skill", ["resourceName"] = "reference" }))?.ToString());
        static object Contract(AIFunction tool) => new {
            tool.Name, tool.Description, Schema = tool.JsonSchema.GetRawText(),
            Result = tool.ReturnJsonSchema?.GetRawText(), Approval = tool.GetService<ApprovalRequiredAIFunction>() is not null
        };
    }

    [Theory]
    [InlineData(ContributionChange.ForeignAdded)]
    [InlineData(ContributionChange.InheritedRemoved)]
    [InlineData(ContributionChange.InheritedReplaced)]
    [InlineData(ContributionChange.InheritedDuplicated)]
    [InlineData(ContributionChange.ContributionDuplicated)]
    [InlineData(ContributionChange.NativeAdded)]
    [InlineData(ContributionChange.ApprovalChanged)]
    [InlineData(ContributionChange.InheritedShadowsFamily)]
    public void Merged_context_rejects_changes_to_inherited_identity_cardinality_or_owned_function_contract(ContributionChange change) {
        var ordinary = AIFunctionFactory.Create(() => "Original", OrdinaryToolName);
        var script = AIFunctionFactory.Create(() => "Skill", AgentSkillsProvider.RunSkillScriptToolName);
        var approved = new ApprovalRequiredAIFunction(script);
        var registration = Registration(approval: true);
        AITool[] inherited = [ordinary];
        List<AITool> merged = [ordinary, approved];
        switch (change) {
            case ContributionChange.ForeignAdded:
                merged.Add(AIFunctionFactory.Create(() => "Foreign", ForeignToolName));
                break;
            case ContributionChange.InheritedRemoved:
                merged.RemoveAt(0);
                break;
            case ContributionChange.InheritedReplaced:
                merged[0] = AIFunctionFactory.Create(() => "Replacement", OrdinaryToolName);
                break;
            case ContributionChange.InheritedDuplicated:
                merged.Add(ordinary);
                break;
            case ContributionChange.ContributionDuplicated:
                merged.Add(approved);
                break;
            case ContributionChange.NativeAdded:
                merged.Add(new HostedWebSearchTool());
                break;
            case ContributionChange.ApprovalChanged:
                merged[1] = script;
                break;
            case ContributionChange.InheritedShadowsFamily:
                inherited[0] = AIFunctionFactory.Create(() => "Shadow", AgentSkillsProvider.LoadSkillToolName);
                merged[0] = inherited[0];
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(change));
        }
        var denied = Assert.Throws<AgentToolAdmissionException>(() => registration.RequireContribution(inherited, merged));
        Assert.Equal(change == ContributionChange.ApprovalChanged ? "tool-admission.context-tool-approval"
            : "tool-admission.context-tool-contract", denied.Code);
    }

    [Fact]
    public void Equal_inherited_instances_keep_their_exact_original_cardinality() {
        var inherited = AIFunctionFactory.Create(() => "Original", OrdinaryToolName);
        var contribution = AIFunctionFactory.Create(() => "Skill", AgentSkillsProvider.LoadSkillToolName);
        var registration = Registration(approval: false);
        Assert.Same(contribution, Assert.Single(registration.RequireContribution([inherited, inherited], [inherited, contribution, inherited])));
        Assert.Throws<AgentToolAdmissionException>(() => registration.RequireContribution([inherited, inherited], [inherited, contribution]));
        Assert.Throws<AgentToolAdmissionException>(() => registration.RequireContribution([inherited], [inherited, contribution, inherited]));
    }

    public enum ContributionChange {
        ForeignAdded,
        InheritedRemoved,
        InheritedReplaced,
        InheritedDuplicated,
        ContributionDuplicated,
        NativeAdded,
        ApprovalChanged,
        InheritedShadowsFamily
    }
}
