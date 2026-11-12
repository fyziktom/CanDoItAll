using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafOwnerSourceRestorationTests {
    private const string Tool = "fixture_owner_mutation";

    [Fact]
    public void Restore_uses_the_exact_saved_source_instead_of_the_current_provider_source() {
        var original = Source("original");
        var current = Source("current");
        var metadata = Metadata(current);
        using var arguments = JsonDocument.Parse("{}");

        Assert.Equal(current, MafToolRunContext.PrepareMetadata(metadata, arguments.RootElement, false, original).SourcePreparation);
        Assert.Equal(original, MafToolRunContext.PrepareMetadata(metadata, arguments.RootElement, true, original).SourcePreparation);
        Assert.Throws<AgentToolAdmissionException>(() => MafToolRunContext.PrepareMetadata(metadata, arguments.RootElement, true, null));
        Assert.Throws<InvalidDataException>(() => MafToolRunContext.PrepareMetadata(metadata, arguments.RootElement, true,
            AgentToolProtocolEnvelope.Create("unsupported", 1, "{}")));
    }

    [Fact]
    public void Providers_without_owner_restoration_keep_their_existing_preparation_behavior() {
        var current = Source("current");
        var metadata = Metadata(current) with { RestoreAdmission = null };
        using var arguments = JsonDocument.Parse("{}");

        Assert.Equal(current, MafToolRunContext.PrepareMetadata(metadata, arguments.RootElement, true, Source("original")).SourcePreparation);
    }

    [Theory]
    [InlineData(AgentToolProposalState.Prepared)]
    [InlineData(AgentToolProposalState.Executing)]
    [InlineData(AgentToolProposalState.ReconciliationRequired)]
    [InlineData(AgentToolProposalState.Completed)]
    public void Owner_restore_selects_exact_batch_without_inheriting_identical_calls_from_another_batch(AgentToolProposalState state) {
        var first = Proposal(state);
        var next = first with { IntentId = new(Guid.NewGuid()) };
        var batch = new AgentToolBatchRecord(new(Guid.NewGuid()), 0, Digest("request"), Source("response"), [first]);
        var other = new AgentToolBatchRecord(new(Guid.NewGuid()), 1, Digest("request"), Source("response"), [next]);
        var segment = new AgentToolInvocationSegment(Guid.NewGuid(), 0, 0, Source("input"), PendingApprovals: []);
        var journal = Journal([segment], [batch, other]);
        var call = new FunctionCallContent(first.CallId, Tool, new Dictionary<string, object?>());

        Assert.Same(first, MafToolRunContext.FindOriginalProposal(journal, segment, batch.Id, call));
        Assert.Same(next, MafToolRunContext.FindOriginalProposal(journal, segment, other.Id, call));
        Assert.Null(MafToolRunContext.FindOriginalProposal(journal, segment, new(Guid.NewGuid()), call));
        Assert.Equal(state == AgentToolProposalState.Completed,
            MafToolRunContext.FindCompletedContextProposal(journal, segment, batch.Id, call) is not null);
    }

    [Fact]
    public void Prepared_approval_continuation_uses_its_original_intent_binding() {
        var proposal = Proposal(AgentToolProposalState.Prepared);
        var batch = new AgentToolBatchRecord(new(Guid.NewGuid()), 0, Digest("request"), Source("response"), [proposal]);
        var approval = new PendingToolApprovalRecord("approval", proposal.CallId, Tool, "function", "", "{}") {
            ToolAdmission = new(batch.Id, proposal.IntentId, proposal.Payload.SemanticVersion, proposal.Payload.Digest)
        };
        var previous = new AgentToolInvocationSegment(Guid.NewGuid(), 0, 0, Source("input"), PendingApprovals: [approval]);
        var current = new AgentToolInvocationSegment(Guid.NewGuid(), 1, 1, Source("input"), previous.Id, PendingApprovals: []);
        var journal = Journal([previous, current], [batch]);

        Assert.Same(proposal, MafToolRunContext.FindOriginalProposal(journal, current, null, new(proposal.CallId, Tool)));
        Assert.Null(MafToolRunContext.FindOriginalProposal(journal, current, null, new("another-call", Tool)));
    }

    private static AgentRuntimeToolMetadata Metadata(AgentToolProtocolEnvelope current) => new("fixture", Tool,
        AgentRuntimeToolOperationKind.Mutation, true) {
        PrepareAdmission = arguments => Payload(arguments.GetRawText(), current),
        RestoreAdmission = (arguments, original) => original.Format == "fixture-source" && original.Version == 1
            ? Payload(arguments.GetRawText(), original) : throw new InvalidDataException("Unsupported original source.")
    };

    private static AgentToolPreparedPayload Payload(string arguments, AgentToolProtocolEnvelope source)
        => new(Tool, 2, Digest(arguments + source.Digest.Value), arguments, AgentToolProposalEffect.Mutation,
            AgentToolProposalRecovery.OwnerReceipt, source);
    private static AgentToolProtocolEnvelope Source(string value)
        => AgentToolProtocolEnvelope.Create("fixture-source", 1, JsonSerializer.Serialize(new { value }));
    private static AgentToolSemanticDigest Digest(string value) => AgentToolProtocolEnvelope.ComputeDigest(value);
    private static AgentToolProposalRecord Proposal(AgentToolProposalState state)
        => new(new(Guid.NewGuid()), 0, "call", Payload("{}", Source("original")), false, state,
            ExecutionApprovalStatus.Approved, Payload("{}", Source("original")).Digest);
    private static AgentToolJournalRecord Journal(System.Collections.Immutable.ImmutableArray<AgentToolInvocationSegment> segments,
        System.Collections.Immutable.ImmutableArray<AgentToolBatchRecord> batches)
        => new(1, 1, new(new(Guid.NewGuid(), Guid.NewGuid(), new(Guid.NewGuid())), Guid.NewGuid(),
            AgentRuntimeContextPurpose.InteractiveChat, new(Guid.NewGuid(), "fixture", new(1))), segments, batches);
}
