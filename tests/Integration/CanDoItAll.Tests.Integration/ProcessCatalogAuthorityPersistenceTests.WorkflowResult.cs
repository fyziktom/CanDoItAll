using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Workflow_tool_result_disclosure_accepts_numeric_and_SDK_string_enums_but_requires_exact_nonempty_owner_IDs(bool stringEnums) {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var test = await WorkflowCase.CreateAsync(scope.ServiceProvider, clock);
        await using var lease = await test.Journal.AcquireRunAsync(test.Context.AdmittedToolSession!, default);
        using var bound = lease.Bind();
        var batch = await test.AdmitAsync(lease, ["saved-result"]);
        var claim = await test.Journal.ClaimInvocationAsync(lease, batch.Id, "saved-result", test.Payload, default);
        using var invocation = claim.Bind();
        var run = await test.RunAsync();
        await test.Owner().CreateRunWithStartedEventAsync(run, WorkflowStarted(run));
        var result = new WorkflowAgentStartResult(new(run.RunId.Value, run.WorkflowId.Value, run.VersionId.Value, run.State,
            run.Backend, run.Summary, run.CreatedAtUtc, run.UpdatedAtUtc, run.TerminalAtUtc),
            WorkflowAgentDefinitionSelectionMode.ExactSavedVersion, run.Backend, WorkflowLaunchIdempotencyDisposition.EnforcedNewRun, "Accepted") {
            Observation = WorkflowLaunchObservation.AdmissionReceiptPending
        };
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        if (stringEnums) {
            options.Converters.Add(new JsonStringEnumConverter());
        }
        var savedResult = JsonSerializer.SerializeToElement(result, options);
        Assert.Equal(stringEnums ? JsonValueKind.String : JsonValueKind.Number, savedResult.GetProperty("run").GetProperty("state").ValueKind);
        var decoded = savedResult.Deserialize<WorkflowAgentStartResult>(WorkflowProcessToolProposalCodec.SerializerOptions)!;
        Assert.Equal(result, decoded);
        Assert.NotEqual(Guid.Empty, decoded.Run.RunId);
        Assert.NotEqual(Guid.Empty, decoded.Run.WorkflowId);
        Assert.NotEqual(Guid.Empty, decoded.Run.VersionId);
        Assert.True(decoded.RequiresOwnerReconciliation);
        await test.Journal.CompleteInvocationAsync(claim, WorkflowEnvelope(savedResult.GetRawText()), AgentToolEffectState.Committed, default, requiresOwnerReconciliation: result.RequiresOwnerReconciliation);
        var metadata = Assert.Single(test.Provider.GetToolMetadata(test.Context), item => item.ToolName == WorkflowToolPolicy.WorkflowsRunStart);
        await using var permitted = await metadata.AuthorizeResultDisclosureAsync!(new(claim.Proposal.IntentId, test.Payload,
            AgentToolEffectState.Committed, savedResult), default);
        Assert.NotNull(permitted);
        await permitted.DisposeAsync();
        var forged = JsonSerializer.SerializeToElement(result with { Run = result.Run with { RunId = Guid.NewGuid() } }, options);
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => metadata.AuthorizeResultDisclosureAsync!(
            new(claim.Proposal.IntentId, test.Payload, AgentToolEffectState.Committed, forged), default).AsTask());
        Assert.Equal(test.Payload, new WorkflowProcessToolProposalCodec().Prepare(test.Input));
    }
}
