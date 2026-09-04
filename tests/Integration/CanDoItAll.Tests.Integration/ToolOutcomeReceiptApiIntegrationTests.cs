using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Web.Api;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class ToolOutcomeReceiptApiIntegrationTests
{
    [Fact]
    public void Failed_outcome_round_trips_through_persistence_and_public_projection()
    {
        var source = CreateReceipt() with
        {
            InvocationOutcome = AgentToolInvocationOutcome.Failed,
            EffectState = AgentToolEffectState.NotCommitted,
            FailureCode = "InvalidToolArguments",
            FailureMessage = "Argument at '$.request.parentNodeKey' is required and is missing.",
            CanRetryWithCorrectedInput = true,
            EffectSourceKind = "project-structure",
            EffectSourceId = "project-42"
        };

        var persisted = RoundTrip(source);
        var projected = Assert.Single(AgentApiResponseMapper.ToToolReceipts([persisted]));

        Assert.Equal(AgentToolInvocationOutcome.Failed, projected.InvocationOutcome);
        Assert.Equal(AgentToolEffectState.NotCommitted, projected.EffectState);
        Assert.Equal("InvalidToolArguments", projected.FailureCode);
        Assert.True(projected.CanRetryWithCorrectedInput);
        Assert.Equal("project-structure", projected.EffectSourceKind);
        Assert.Equal("project-42", projected.EffectSourceId);
        Assert.DoesNotContain(
            "RequestSummary",
            JsonSerializer.Serialize(projected),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Committed_effect_is_preserved_when_later_processing_marks_the_invocation_failed()
    {
        var projected = Assert.Single(AgentApiResponseMapper.ToToolReceipts(
        [
            CreateReceipt() with
            {
                InvocationOutcome = AgentToolInvocationOutcome.Failed,
                EffectState = AgentToolEffectState.Committed,
                FailureCode = "PostCommitObservationFailed",
                FailureMessage = "The project structure changed, but a later observation failed."
            }
        ]));

        Assert.Equal(AgentToolInvocationOutcome.Failed, projected.InvocationOutcome);
        Assert.Equal(AgentToolEffectState.Committed, projected.EffectState);
        Assert.Equal("PostCommitObservationFailed", projected.FailureCode);
    }

    [Fact]
    public void Persistence_normalization_redacts_and_bounds_failure_fields()
    {
        const string secret = "receipt-secret-value";
        var source = CreateReceipt() with
        {
            InvocationOutcome = AgentToolInvocationOutcome.Failed,
            EffectState = AgentToolEffectState.Unknown,
            FailureMessage = $"Provider failed with api_key={secret}.",
            EffectSourceId = $"password={secret}"
        };
        var method = typeof(FileSandboxWorkspaceExecutionSliceStore).GetMethod(
            "NormalizeToolReceipt",
            BindingFlags.NonPublic | BindingFlags.Static);
        var normalized = Assert.IsType<ToolExecutionReceiptRecord>(
            method!.Invoke(null, [source]));

        Assert.DoesNotContain(secret, normalized.FailureMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, normalized.EffectSourceId, StringComparison.Ordinal);
    }

    [Fact]
    public void Legacy_receipt_without_typed_fields_deserializes_as_unknown()
    {
        var json = JsonNode.Parse(JsonSerializer.Serialize(CreateReceipt()))!.AsObject();
        foreach (var propertyName in new[]
                 {
                     "InvocationOutcome",
                     "EffectState",
                     "FailureCode",
                     "FailureMessage",
                     "CanRetryWithCorrectedInput",
                     "EffectSourceKind",
                     "EffectSourceId"
                 })
        {
            json.Remove(propertyName);
        }

        var legacy = JsonSerializer.Deserialize<ToolExecutionReceiptRecord>(json.ToJsonString());

        Assert.NotNull(legacy);
        Assert.Equal(AgentToolInvocationOutcome.Unknown, legacy.InvocationOutcome);
        Assert.Equal(AgentToolEffectState.Unknown, legacy.EffectState);
        Assert.Empty(legacy.FailureCode);
        Assert.Empty(legacy.FailureMessage);
        Assert.False(legacy.CanRetryWithCorrectedInput);
    }

    private static ToolExecutionReceiptRecord RoundTrip(ToolExecutionReceiptRecord source)
    {
        return JsonSerializer.Deserialize<ToolExecutionReceiptRecord>(
            JsonSerializer.Serialize(source))!;
    }

    private static ToolExecutionReceiptRecord CreateReceipt()
    {
        var now = DateTimeOffset.UtcNow;
        return new ToolExecutionReceiptRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "runtime-provider",
            "project_structure_asset_create",
            "RuntimeProvider:Mutation",
            "PolicyEnforced",
            "RuntimeProviderPolicy",
            "project_structure_asset_create|projectId=<redacted>,request={...}",
            string.Empty,
            "Failed",
            now,
            now.AddMilliseconds(10))
        {
            RuntimeToolProviderKey = "project-structure.runtime-tools",
            RuntimeToolProviderName = "Project structure runtime tools",
            DeclaredSideEffectMode = ToolExecutionSideEffectMode.ProductMutation
        };
    }
}