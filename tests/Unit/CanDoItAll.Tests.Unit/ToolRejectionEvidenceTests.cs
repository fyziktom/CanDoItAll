using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ToolRejectionEvidenceTests
{
    [Fact]
    public void Owner_rejection_before_any_effect_makes_a_returned_mutation_failure_not_committed()
    {
        var failed = JsonSerializer.SerializeToElement(new { succeeded = false, message = "The target already exists." });
        var uncertain = MafRuntimeToolInvocationResultClassifier.Assess(ToolContractCatalog.WorkspaceWriteFile,
            ToolInvocationClassification.Mutation, failed);
        Assert.Equal(AgentToolEffectState.Unknown, uncertain.EffectState);

        using var capture = AgentToolInvocationEffectScope.Begin();
        AgentToolInvocationEffectScope.RecordRejectedBeforeEffect();
        Assert.True(capture.RejectedBeforeEffect);
        Assert.True(AgentToolInvocationEffectScope.IsCurrentRejectedBeforeEffect);
        var rejected = MafRuntimeToolInvocationResultClassifier.Assess(ToolContractCatalog.WorkspaceWriteFile,
            ToolInvocationClassification.Mutation, failed, rejectedBeforeEffect: capture.RejectedBeforeEffect);

        Assert.Equal(AgentToolInvocationOutcome.Failed, rejected.Outcome);
        Assert.Equal(AgentToolEffectState.NotCommitted, rejected.EffectState);
    }

    [Fact]
    public void A_committed_effect_outranks_an_earlier_rejection_and_no_scope_records_nothing()
    {
        AgentToolInvocationEffectScope.RecordRejectedBeforeEffect();
        Assert.False(AgentToolInvocationEffectScope.IsCurrentRejectedBeforeEffect);

        using var capture = AgentToolInvocationEffectScope.Begin();
        AgentToolInvocationEffectScope.RecordRejectedBeforeEffect();
        AgentToolInvocationEffectScope.RecordCommitted("owner", "saved-id");

        Assert.False(capture.RejectedBeforeEffect);
        Assert.False(AgentToolInvocationEffectScope.IsCurrentRejectedBeforeEffect);
        Assert.Equal(new AgentToolCommittedEffect("owner", "saved-id"), capture.CommittedEffect);
    }

    [Fact]
    public void Argument_rejected_by_its_input_constructor_is_a_correctable_failure_before_invocation()
    {
        var invocations = 0;
        var function = AIFunctionFactory.Create((BoundedToolInput request) => ++invocations, "bounded_tool");
        using var document = JsonDocument.Parse("""{ "request": { "take": 11 } }""");
        var arguments = new AIFunctionArguments(document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => (object?)property.Value.Clone()));

        var mapped = MafToolArgumentBindingFailureMapper.TryCreatePreInvocationFailure(function, arguments, out var failure);

        Assert.True(mapped);
        Assert.Equal(0, invocations);
        Assert.Equal("InvalidToolArguments", failure.ErrorCode);
        Assert.Contains("'$.request' was rejected: Take cannot exceed 10", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("(Parameter", failure.Message, StringComparison.Ordinal);
        Assert.Equal(AgentToolEffectState.NotCommitted, failure.EffectState);
        Assert.True(failure.CanRetryWithCorrectedInput);

        using var valid = JsonDocument.Parse("""{ "request": { "take": 10 } }""");
        Assert.False(MafToolArgumentBindingFailureMapper.TryCreatePreInvocationFailure(function,
            new AIFunctionArguments(valid.RootElement.EnumerateObject()
                .ToDictionary(property => property.Name, property => (object?)property.Value.Clone())), out _));
    }

    [Fact]
    public void An_argument_type_the_serializer_cannot_construct_is_rejected_before_invocation_instead_of_escaping()
    {
        var invocations = 0;
        var function = AIFunctionFactory.Create((UnbindableToolInput request) => ++invocations, "unbindable_tool");
        using var document = JsonDocument.Parse("""{ "request": { "items": ["one"] } }""");
        var arguments = new AIFunctionArguments(document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => (object?)property.Value.Clone()));

        var mapped = MafToolArgumentBindingFailureMapper.TryCreatePreInvocationFailure(function, arguments, out var failure);

        Assert.True(mapped);
        Assert.Equal(0, invocations);
        Assert.Contains("'$.request' could not be read as the tool's argument type", failure.Message, StringComparison.Ordinal);
        Assert.Equal(AgentToolEffectState.NotCommitted, failure.EffectState);
    }

    // The constructor parameter type differs from its property type, which the serializer cannot bind.
    public sealed record UnbindableToolInput
    {
        public UnbindableToolInput(IEnumerable<string> items) => Items = items.ToArray();

        public IReadOnlyList<string> Items { get; }
    }

    [Theory]
    [InlineData(AgentToolEffectState.None, true)]
    [InlineData(AgentToolEffectState.NotCommitted, true)]
    [InlineData(AgentToolEffectState.Unknown, false)]
    [InlineData(AgentToolEffectState.Committed, false)]
    public void Only_a_typed_rejection_with_proven_no_effect_is_disclosed_without_its_success_shape(
        AgentToolEffectState effect,
        bool expected)
    {
        var payload = new AgentToolPreparedPayload("fixture_tool", 1, AgentToolProtocolEnvelope.ComputeDigest("{}"), "{}",
            AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.ReconcileBeforeRetry);
        var disclosure = new AgentToolResultDisclosure(new AgentToolBusinessIntentId(Guid.NewGuid()), payload, effect,
            JsonSerializer.SerializeToElement(new { succeeded = false }));

        Assert.False(disclosure.IsNoEffectTypedFailure);
        Assert.Equal(expected, (disclosure with { IsTypedFailure = true }).IsNoEffectTypedFailure);
    }

    public sealed record BoundedToolInput
    {
        [JsonConstructor]
        public BoundedToolInput(int take)
        {
            if (take > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(take), "Take cannot exceed 10.");
            }

            Take = take;
        }

        public int Take { get; }
    }
}
