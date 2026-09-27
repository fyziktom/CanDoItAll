using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class MafWorkspaceToolResultDisclosureIntegrationTests {
    [Fact]
    public async Task A_script_that_could_not_start_is_a_durable_not_committed_result_with_its_launch_code() {
        await using var fixture = await Fixture.CreateAsync(ToolContractCatalog.WorkspacePowerShellRunScript);
        fixture.Operations.FailStartWith = WorkspaceProcessStartFailureKind.ExecutableNotFound;
        var pending = await fixture.ExecuteAsync(new ToolClient(ToolContractCatalog.WorkspacePowerShellRunScript));
        await fixture.Journal.ApproveAsync(pending.PendingApprovals);

        var client = new ToolClient(ToolContractCatalog.WorkspacePowerShellRunScript);
        var completed = await fixture.ExecuteAsync(client);

        Assert.Contains("completed", completed.ResponseText, StringComparison.Ordinal);
        Assert.Equal(1, fixture.Operations.Commands - fixture.Operations.AliasCommands);
        var proposal = await fixture.ProposalAsync();
        Assert.Equal(AgentToolProposalState.Completed, proposal.State);
        Assert.Equal(AgentToolEffectState.NotCommitted, proposal.EffectState);
        var modelInput = Assert.Single(client.Inputs);
        Assert.Contains("ProcessStartFailed.ExecutableNotFound", modelInput, StringComparison.Ordinal);
        Assert.Contains("(pwsh or powershell)", modelInput, StringComparison.Ordinal);
        Assert.DoesNotContain("operator-only start detail", modelInput, StringComparison.Ordinal);
    }
}
