using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkspaceScriptEnvironmentAccessAnalyzerTests
{
    private static readonly HashSet<string> BaseNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "PATH", "TEMP", "TMP", "TMPDIR", "HOME", "USERPROFILE", "PSModulePath", "PYTHONUTF8"
    };

    private const string PowerShell = ToolContractCatalog.WorkspacePowerShellRunScript;
    private const string Python = ToolContractCatalog.WorkspacePythonRunFile;

    [Theory]
    [InlineData("Write-Output $env:TEMP")]
    [InlineData("$path = Join-Path $env:USERPROFILE 'x'")]
    [InlineData("$env:PATH += ';C:\\tools'")]
    [InlineData("$env:APP_MODE = 'test'")]
    [InlineData("${env:TEMP}")]
    [InlineData("[Environment]::GetEnvironmentVariable('PATH')")]
    [InlineData("Get-Item env:TEMP")]
    [InlineData("# Get-ChildItem Env:\nWrite-Output 'ok'")]
    [InlineData("<# $env:OPENAI_API_KEY #>\nWrite-Output 'ok'")]
    [InlineData("Write-Output 'environment check skipped'")]
    public void PowerShell_reads_of_basic_names_writes_and_comments_are_allowed(string script)
    {
        Assert.Empty(Analyze(PowerShell, script));
    }

    [Theory]
    [InlineData("Write-Output $env:OPENAI_API_KEY", "OPENAI_API_KEY")]
    [InlineData("${env:MY_SECRET}", "MY_SECRET")]
    [InlineData("[Environment]::GetEnvironmentVariable('CORP_TOKEN')", "CORP_TOKEN")]
    [InlineData("[System.Environment]::GetEnvironmentVariable(\"HTTPS_PROXY\", 'Process')", "HTTPS_PROXY")]
    [InlineData("(Get-Item -Path env:DATABASE_URL).Value", "DATABASE_URL")]
    public void PowerShell_named_reads_outside_the_basic_set_are_reported(string script, string expectedName)
    {
        var finding = Assert.Single(Analyze(PowerShell, script));

        Assert.Equal(WorkspaceScriptEnvironmentAccessKind.NamedRead, finding.Kind);
        Assert.Equal(expectedName, finding.Name);
    }

    [Theory]
    [InlineData("Get-ChildItem Env:")]
    [InlineData("gci env: | Sort-Object Name")]
    [InlineData("dir env:")]
    [InlineData("ls env:\\")]
    [InlineData("Get-ChildItem -Path Env:*")]
    [InlineData("[Environment]::GetEnvironmentVariables()")]
    public void PowerShell_listing_all_variables_is_reported_as_enumeration(string script)
    {
        Assert.Contains(Analyze(PowerShell, script), finding => finding.Kind == WorkspaceScriptEnvironmentAccessKind.Enumeration);
    }

    [Theory]
    [InlineData("Get-Item \"env:$name\"")]
    [InlineData("[Environment]::GetEnvironmentVariable($name)")]
    public void PowerShell_names_computed_at_run_time_are_reported_as_dynamic(string script)
    {
        Assert.Contains(Analyze(PowerShell, script), finding => finding.Kind == WorkspaceScriptEnvironmentAccessKind.DynamicName);
    }

    [Theory]
    [InlineData("import os\nprint(os.environ['HOME'])")]
    [InlineData("import os\nos.getenv('TEMP')")]
    [InlineData("import os\nos.environ.get(\"PATH\", \"\")")]
    [InlineData("import os\nos.environ['APP_MODE'] = 'test'")]
    [InlineData("import os, subprocess\nsubprocess.run(['x'], env=os.environ)")]
    [InlineData("import os, subprocess\nchild = os.environ.copy()\nchild['A'] = '1'")]
    [InlineData("import os, subprocess\nsubprocess.run(['x'], env=dict(os.environ))")]
    [InlineData("# print(os.environ)\nprint('ok')")]
    public void Python_basic_reads_writes_and_passing_the_environment_on_are_allowed(string script)
    {
        Assert.Empty(Analyze(Python, script));
    }

    [Theory]
    [InlineData("import os\nos.environ.get('OPENAI_API_KEY')", "OPENAI_API_KEY")]
    [InlineData("import os\nkey = os.environ[\"GITHUB_TOKEN\"]", "GITHUB_TOKEN")]
    [InlineData("import os\nos.getenv('HTTPS_PROXY')", "HTTPS_PROXY")]
    [InlineData("from os import environ\nenviron['DB_PASSWORD']", "DB_PASSWORD")]
    public void Python_named_reads_outside_the_basic_set_are_reported(string script, string expectedName)
    {
        var finding = Assert.Single(Analyze(Python, script));

        Assert.Equal(WorkspaceScriptEnvironmentAccessKind.NamedRead, finding.Kind);
        Assert.Equal(expectedName, finding.Name);
    }

    [Theory]
    [InlineData("import os\nprint(os.environ)")]
    [InlineData("import os\nfor key, value in os.environ.items():\n    print(key)")]
    [InlineData("import os\nfor key in os.environ:\n    print(key)")]
    [InlineData("import os, json\njson.dumps(dict(os.environ))")]
    [InlineData("import os\nsnapshot = dict(os.environ)")]
    [InlineData("import os\nsorted(os.environ)")]
    public void Python_dumping_or_iterating_the_environment_is_reported_as_enumeration(string script)
    {
        Assert.Contains(Analyze(Python, script), finding => finding.Kind == WorkspaceScriptEnvironmentAccessKind.Enumeration);
    }

    [Theory]
    [InlineData("import os\nos.environ[name]")]
    [InlineData("import os\nos.getenv(variable)")]
    [InlineData("import os\nos.environ.get(key, '')")]
    public void Python_names_computed_at_run_time_are_reported_as_dynamic(string script)
    {
        Assert.Contains(Analyze(Python, script), finding => finding.Kind == WorkspaceScriptEnvironmentAccessKind.DynamicName);
    }

    [Fact]
    public void Other_tools_and_empty_content_are_not_inspected()
    {
        Assert.Empty(Analyze(ToolContractCatalog.WorkspaceReadFile, "Write-Output $env:OPENAI_API_KEY"));
        Assert.Empty(Analyze(PowerShell, "   "));
    }

    [Fact]
    public void The_description_names_variables_and_kinds_without_values()
    {
        var findings = Analyze(
            PowerShell,
            "$a = $env:OPENAI_API_KEY\n$b = $env:CORP_TOKEN\nGet-ChildItem Env:\n[Environment]::GetEnvironmentVariable($n)");

        Assert.Equal(
            "reads OPENAI_API_KEY and CORP_TOKEN, lists all environment variables and reads a variable whose name is computed at run time",
            WorkspaceScriptEnvironmentAccessAnalyzer.Describe(findings));
    }

    private static IReadOnlyList<WorkspaceScriptEnvironmentAccessFinding> Analyze(string toolName, string script)
        => WorkspaceScriptEnvironmentAccessAnalyzer.Analyze(toolName, script, BaseNames.Contains);
}
