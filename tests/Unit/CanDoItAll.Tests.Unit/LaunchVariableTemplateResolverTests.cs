using CanDoItAll.Processes.Application;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class LaunchVariableTemplateResolverTests
{
    [Fact]
    public void Resolve_replaces_supported_placeholder_forms_recursively()
    {
        var resolver = new LaunchVariableTemplateResolver();
        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CurrentProcessRunId"] = "run-001",
            ["ScriptName"] = "create-dotnet-project.wire-solution.ps1",
            ["ManagedArtifactRoot"] = "artifacts/process-runs/{CurrentProcessRunId}",
            ["DotNetCreateProjectScriptRef"] = "{ManagedArtifactRoot}/scripts/${ScriptName}",
            ["DotNetAddTestProjectScriptRef"] = "artifacts/process-runs/{{CurrentProcessRunId}}/scripts/add-test-project.wire-solution.ps1"
        };

        var result = resolver.Resolve(variables);

        Assert.False(result.HasBlockingDiagnostics);
        Assert.Equal(
            "artifacts/process-runs/run-001/scripts/create-dotnet-project.wire-solution.ps1",
            result.Variables["DotNetCreateProjectScriptRef"]);
        Assert.Equal(
            "artifacts/process-runs/run-001/scripts/add-test-project.wire-solution.ps1",
            result.Variables["DotNetAddTestProjectScriptRef"]);
    }

    [Fact]
    public void Resolve_reports_unresolved_tool_critical_placeholder_as_blocking()
    {
        var resolver = new LaunchVariableTemplateResolver();
        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DotNetCreateProjectScriptRef"] = "artifacts/process-runs/{CurrentProcessRunId}/scripts/create-dotnet-project.wire-solution.ps1"
        };

        var result = resolver.Resolve(variables);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.True(result.HasBlockingDiagnostics);
        Assert.Equal(LaunchVariableTemplateDiagnosticKind.UnresolvedPlaceholder, diagnostic.Kind);
        Assert.Equal("DotNetCreateProjectScriptRef", diagnostic.VariableKey);
        Assert.Equal("CurrentProcessRunId", diagnostic.PlaceholderKey);
        Assert.True(diagnostic.IsToolCritical);
    }

    [Fact]
    public void Resolve_reports_cycles_as_blocking()
    {
        var resolver = new LaunchVariableTemplateResolver();
        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DotNetCreateProjectScriptRef"] = "{ScriptPath}",
            ["ScriptPath"] = "{ManagedArtifactRoot}/scripts/create.ps1",
            ["ManagedArtifactRoot"] = "artifacts/process-runs/{ScriptPath}"
        };

        var result = resolver.Resolve(variables);

        Assert.True(result.HasBlockingDiagnostics);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Kind == LaunchVariableTemplateDiagnosticKind.Cycle &&
            diagnostic.IsToolCritical);
    }

    [Fact]
    public void Resolve_preserves_non_tool_critical_unresolved_placeholder_without_blocking()
    {
        var resolver = new LaunchVariableTemplateResolver();
        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DisplayText"] = "Optional future value: {Later}"
        };

        var result = resolver.Resolve(variables);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.False(result.HasBlockingDiagnostics);
        Assert.Equal(LaunchVariableTemplateDiagnosticKind.UnresolvedPlaceholder, diagnostic.Kind);
        Assert.Equal("DisplayText", diagnostic.VariableKey);
        Assert.Equal("Later", diagnostic.PlaceholderKey);
        Assert.False(diagnostic.IsToolCritical);
        Assert.Equal("Optional future value: {Later}", result.Variables["DisplayText"]);
    }
}
