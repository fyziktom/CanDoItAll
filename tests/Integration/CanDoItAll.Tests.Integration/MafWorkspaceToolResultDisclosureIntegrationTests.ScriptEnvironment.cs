using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class MafWorkspaceToolResultDisclosureIntegrationTests {
    private const string SecretReadingScript = "Write-Output $env:OPENAI_API_KEY";

    [Fact]
    public async Task A_script_reading_a_secret_variable_is_refused_before_any_process_starts_without_the_agent_permission() {
        await using var fixture = await Fixture.CreateAsync(ToolContractCatalog.WorkspacePowerShellRunScript);
        await File.WriteAllTextAsync(Path.Combine(fixture.Journal.WorkspaceRoot, "script.ps1"), SecretReadingScript);
        var pending = await fixture.ExecuteAsync(new ToolClient(ToolContractCatalog.WorkspacePowerShellRunScript));
        await fixture.Journal.ApproveAsync(pending.PendingApprovals);

        var client = new ToolClient(ToolContractCatalog.WorkspacePowerShellRunScript);
        var completed = await fixture.ExecuteAsync(client);

        Assert.Contains("completed", completed.ResponseText, StringComparison.Ordinal);
        Assert.Equal(0, fixture.Operations.Commands - fixture.Operations.AliasCommands);
        var proposal = await fixture.ProposalAsync();
        Assert.Equal(AgentToolEffectState.NotCommitted, proposal.EffectState);
        var modelInput = Assert.Single(client.Inputs);
        Assert.Contains("reads OPENAI_API_KEY", modelInput, StringComparison.Ordinal);
        Assert.Contains("Scripts may read environment variables", modelInput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_same_script_runs_when_the_agent_may_read_the_environment() {
        await using var fixture = await Fixture.CreateAsync(ToolContractCatalog.WorkspacePowerShellRunScript);
        await fixture.SaveActorAndReloadAsync(scriptEnvironment: true);
        await File.WriteAllTextAsync(Path.Combine(fixture.Journal.WorkspaceRoot, "script.ps1"), SecretReadingScript);
        var pending = await fixture.ExecuteAsync(new ToolClient(ToolContractCatalog.WorkspacePowerShellRunScript));
        await fixture.Journal.ApproveAsync(pending.PendingApprovals);

        var completed = await fixture.ExecuteAsync(new ToolClient(ToolContractCatalog.WorkspacePowerShellRunScript));

        Assert.Contains("completed", completed.ResponseText, StringComparison.Ordinal);
        Assert.Equal(1, fixture.Operations.Commands - fixture.Operations.AliasCommands);
    }
}
