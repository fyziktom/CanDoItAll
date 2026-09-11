using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProjectStructureProcessProposalTests {
    private static readonly Guid ProjectId = Guid.Parse("186977db-27b1-462b-b82c-19d917ddf48e");

    [Fact]
    public void Reordered_wire_arguments_and_explicit_defaults_keep_one_exact_owner_receipt_payload() {
        var codec = new ProjectStructureProcessProposalCodec();
        var input = new ProjectStructureProcessStartProposal(ProjectId, "block:origin", new(Execute: true), 8);
        var prepared = codec.Prepare(input);
        using var wire = JsonDocument.Parse($$"""
            {"estimatedMinutes":8,"request":{"execute":true},"nodeId":"block:origin","projectId":"{{ProjectId:D}}"}
            """);
        Assert.Equal(prepared, codec.Prepare(ProjectStructureToolPolicy.ProjectStructureNodeProcessStart, wire.RootElement));
        Assert.Equal(input, codec.Read(prepared));
        Assert.Equal(AgentToolProposalRecovery.OwnerReceipt, prepared.Recovery);
        Assert.Equal(AgentToolProposalEffect.Mutation, prepared.Effect);
    }

    public enum ChangedInput { Project, Node, Definition, Execute, Readiness, Requester }

    [Theory]
    [InlineData(ChangedInput.Project)]
    [InlineData(ChangedInput.Node)]
    [InlineData(ChangedInput.Definition)]
    [InlineData(ChangedInput.Execute)]
    [InlineData(ChangedInput.Readiness)]
    [InlineData(ChangedInput.Requester)]
    public void A_changed_semantic_proposal_cannot_reuse_the_approved_digest(ChangedInput change) {
        var codec = new ProjectStructureProcessProposalCodec();
        var original = new ProjectStructureProcessStartProposal(ProjectId, "block:origin", new());
        var changed = change switch {
            ChangedInput.Project => original with { ProjectId = Guid.NewGuid() },
            ChangedInput.Node => original with { NodeId = "block:other" },
            ChangedInput.Definition => original with { Request = original.Request with { ProcessDefinitionId = Guid.NewGuid() } },
            ChangedInput.Execute => original with { Request = original.Request with { Execute = true } },
            ChangedInput.Readiness => original with { Request = original.Request with { RunHrMatch = false } },
            ChangedInput.Requester => original with { Request = original.Request with { RequestedBy = "changed display request" } },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        Assert.NotEqual(codec.Prepare(original).Digest, codec.Prepare(changed).Digest);
    }

    [Theory]
    [InlineData("\"authority\":{}")]
    [InlineData("\"callerIntentId\":\"27769e76-2064-4406-80bb-cc78fe629d80\"")]
    [InlineData("\"nodeId\":\"changed\"")]
    [InlineData("\"PROJECTID\":\"e06f343a-791d-4cc5-9145-81b02d68ce42\"")]
    public void Tool_arguments_cannot_supply_authority_intent_or_ambiguous_target_properties(string extra) {
        using var document = JsonDocument.Parse($$"""
            {"projectId":"{{ProjectId:D}}","nodeId":"block:origin","request":{},{{extra}}}
            """);
        Assert.Throws<JsonException>(() => new ProjectStructureProcessProposalCodec()
            .Prepare(ProjectStructureToolPolicy.ProjectStructureNodeProcessStart, document.RootElement));
    }

    [Fact]
    public void Runtime_invocation_authority_is_excluded_from_the_existing_agent_context_wire_contract() {
        var input = new ProjectStructureProcessStartProposal(ProjectId, "block:origin", new());
        var authority = new ProcessLaunchAuthority(new ProcessLaunchPrincipal.LocalOperator(ProcessLaunchOperatorSurface.UserInterface),
            Guid.NewGuid(), null, true, true, "source-policy");
        var context = new ProjectStructureAgentContext("display-agent", "Agent", "machine", "repo", "branch", "session") {
            ProcessLaunchInvocation = new(new(Guid.NewGuid()), new ProjectStructureProcessProposalCodec().Prepare(input).Digest.Value, authority, input)
        };
        var json = JsonSerializer.Serialize(context, ProjectStructureProcessProposalCodec.SerializerOptions);
        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("processLaunchInvocation", out _));
        Assert.Null(JsonSerializer.Deserialize<ProjectStructureAgentContext>(json, ProjectStructureProcessProposalCodec.SerializerOptions)!.ProcessLaunchInvocation);
        Assert.NotEqual(Guid.Empty, context.ProcessLaunchInvocation.IntentId.Value);
    }
}
