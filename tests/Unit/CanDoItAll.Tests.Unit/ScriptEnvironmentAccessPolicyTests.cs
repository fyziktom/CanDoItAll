using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ScriptEnvironmentAccessPolicyTests
{
    private const string SecretReadingScript = "Write-Output $env:OPENAI_API_KEY";

    [Fact]
    public async Task Without_the_gate_the_existing_decision_is_unchanged()
    {
        var context = CreateScriptContext(ToolContractCatalog.WorkspacePowerShellRunScript, SecretReadingScript);

        var decision = await new DefaultAgentToolInvocationPolicy().EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.RequireApproval, decision.Kind);
    }

    [Fact]
    public async Task A_script_reading_a_variable_outside_the_basic_set_is_denied_before_approval_with_the_setting_to_enable()
    {
        var context = CreateScriptContext(ToolContractCatalog.WorkspacePowerShellRunScript, SecretReadingScript) with
        {
            ScriptEnvironmentAccessAllowed = false
        };

        var decision = await new DefaultAgentToolInvocationPolicy().EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("reads OPENAI_API_KEY", decision.Reason, StringComparison.Ordinal);
        Assert.Contains("'Scripts may read environment variables'", decision.Reason, StringComparison.Ordinal);
        Assert.Contains("PATH, TEMP and HOME", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Basic_reads_still_go_through_the_normal_approval_path()
    {
        var context = CreateScriptContext(
            ToolContractCatalog.WorkspacePowerShellRunScript,
            "$report = Join-Path $env:TEMP 'report.txt'\n$env:APP_MODE = 'test'") with
        {
            ScriptEnvironmentAccessAllowed = false
        };

        var decision = await new DefaultAgentToolInvocationPolicy().EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.RequireApproval, decision.Kind);
    }

    [Fact]
    public async Task A_python_script_listing_the_environment_is_denied()
    {
        var context = CreateScriptContext(
            ToolContractCatalog.WorkspacePythonRunFile,
            "import os\nfor key in os.environ:\n    print(key)",
            path: "scripts/probe.py") with
        {
            ScriptEnvironmentAccessAllowed = false
        };

        var decision = await new DefaultAgentToolInvocationPolicy().EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("lists all environment variables", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_script_that_could_not_be_inspected_is_denied_with_the_reason()
    {
        var context = CreateScriptContext(
            ToolContractCatalog.WorkspacePowerShellRunScript,
            string.Empty,
            inspectionFailure: "script path 'scripts/probe.ps1' is larger than the 131072 byte policy inspection limit.") with
        {
            ScriptEnvironmentAccessAllowed = false
        };

        var decision = await new DefaultAgentToolInvocationPolicy().EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("could not be inspected for environment access (script path 'scripts/probe.ps1' is larger than the 131072 byte policy inspection limit)", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Other_tools_are_not_affected_by_the_gate()
    {
        var arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["path"] = "notes.md" };
        var context = CreateContext(ToolContractCatalog.WorkspaceReadFile, ToolInvocationClassification.Read, arguments, string.Empty, string.Empty) with
        {
            ScriptEnvironmentAccessAllowed = false
        };

        var decision = await new DefaultAgentToolInvocationPolicy().EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    private static ToolInvocationPolicyContext CreateScriptContext(
        string toolName,
        string content,
        string path = "scripts/probe.ps1",
        string inspectionFailure = "")
        => CreateContext(
            toolName,
            ToolInvocationClassification.Mutation,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["path"] = path },
            content,
            inspectionFailure);

    private static ToolInvocationPolicyContext CreateContext(
        string toolName,
        ToolInvocationClassification classification,
        IReadOnlyDictionary<string, string> arguments,
        string inspectedScriptContent,
        string scriptInspectionFailure)
        => new(
            AgentId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            AgentName: "Script Agent",
            ToolName: toolName,
            RedactedArguments: arguments,
            Classification: classification,
            IsKnownTool: true,
            AutoApprovalAllowed: false,
            ApprovalWrapperAvailable: true,
            ExecutionRunId: "run-script-environment",
            SourceKind: "agents",
            ProcessRunId: string.Empty,
            ProcessStepId: string.Empty,
            ApprovalWrapperEffectiveForProvider: true,
            InspectedScriptContent: inspectedScriptContent,
            ScriptInspectionFailure: scriptInspectionFailure)
        {
            PathArguments = ToolInvocationPathArgumentResolver.Resolve(
                toolName,
                arguments.Select(argument => new KeyValuePair<string, object?>(argument.Key, argument.Value)))
        };
}
