namespace CanDoItAll.Tests.Unit;

public sealed class CrossPlatformCiWorkflowTests
{
    private const string RepositoryRootEnvironmentVariable = "CANDOITALL_TEST_REPOSITORY_ROOT";

    [Fact]
    [Trait("Category", "UnixPortabilityCore")]
    public void Active_workflow_defines_three_actual_host_gates_and_package_fallback()
    {
        string repositoryRoot = FindRepositoryRoot();
        string workflowPath = Path.Combine(repositoryRoot, ".github", "workflows", "ci.yml");
        string disabledWorkflowPath = Path.Combine(repositoryRoot, ".github", "workflows-disabled", "ci.yml");

        Assert.True(File.Exists(workflowPath), $"Active CI workflow is missing: {workflowPath}");
        Assert.False(File.Exists(disabledWorkflowPath), $"Disabled CI workflow still exists: {disabledWorkflowPath}");

        string workflow = File.ReadAllText(workflowPath);
        Assert.Contains("windows-latest", workflow, StringComparison.Ordinal);
        Assert.Contains("ubuntu-24.04", workflow, StringComparison.Ordinal);
        Assert.Contains("macos-15", workflow, StringComparison.Ordinal);
        Assert.Contains("fail-fast: false", workflow, StringComparison.Ordinal);
        Assert.Contains("UseLocalCanDoItAllLibraries=false", workflow, StringComparison.Ordinal);
        Assert.Contains("ikalnytskyi/action-setup-postgres@v8", workflow, StringComparison.Ordinal);
        Assert.Contains("CANDOITALL_TESTS_POSTGRES_CONNECTION", workflow, StringComparison.Ordinal);
        Assert.Contains("postgres-version: \"16\"", workflow, StringComparison.Ordinal);
        Assert.Contains("Category=UnixPortabilityCore", workflow, StringComparison.Ordinal);
        Assert.Contains("Category!=UnixRuntimePortability", workflow, StringComparison.Ordinal);
        Assert.Contains("RequiresHostDocker!=true", workflow, StringComparison.Ordinal);
        Assert.Contains("Run PostgreSQL-backed core migration and restart gate", workflow, StringComparison.Ordinal);
        Assert.Contains(
            "(Category=UnixPortabilityCore)&(RequiresHostDocker=true)",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains("test_enforce_portability_baseline.py", workflow, StringComparison.Ordinal);
        Assert.Contains(
            "python ./codex/bundles/Unix-portability/scripts/enforce_portability_baseline.py",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "--baseline ./codex/bundles/Unix-portability/shared/portability-risk-baseline.json",
            workflow,
            StringComparison.Ordinal);
        Assert.DoesNotContain("--write-baseline", workflow, StringComparison.Ordinal);
        Assert.Contains("Test-CorePortabilityHeadless.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("UnixPortabilityBrowserSmoke", File.ReadAllText(Path.Combine(
            repositoryRoot,
            "tests",
            "Playwright",
            "CanDoItAll.Tests.Playwright",
            "CorePortabilityBrowserSmokeTests.cs")), StringComparison.Ordinal);
        Assert.Contains("playwright.ps1 install chromium", workflow, StringComparison.Ordinal);
        Assert.Contains("Test-RuntimePortability.ps1", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "if: runner.os == 'Windows'\n        shell: pwsh\n        run: ./tests/Playwright/CanDoItAll.Tests.Playwright/bin/Release/net10.0/playwright.ps1 install chromium",
            workflow.Replace("\r\n", "\n", StringComparison.Ordinal),
            StringComparison.Ordinal);
        Assert.Contains("actions/upload-artifact@v4", workflow, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "UnixPortabilityCore")]
    public void Runtime_portability_runner_enforces_exact_cross_project_selection()
    {
        string repositoryRoot = FindRepositoryRoot();
        string script = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "tools",
            "Validation",
            "Test-RuntimePortability.ps1"));

        Assert.Contains("Category=UnixRuntimePortability", script, StringComparison.Ordinal);
        Assert.Contains("ExpectedCaseCount 422", script, StringComparison.Ordinal);
        Assert.Contains("ExpectedCaseCount 33", script, StringComparison.Ordinal);
        Assert.Contains("ExpectedCaseCount 1", script, StringComparison.Ordinal);
        Assert.Contains("class selection drifted", script, StringComparison.Ordinal);
        Assert.Contains("method selection drifted", script, StringComparison.Ordinal);
        Assert.Contains("ValidateSet('All', 'Unit', 'Integration', 'Browser')", script, StringComparison.Ordinal);
        Assert.Contains("--no-build", script, StringComparison.Ordinal);
        Assert.Contains("--no-restore", script, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "UnixPortabilityCore")]
    public void Headless_validator_uses_a_short_redacted_windows_work_root()
    {
        string repositoryRoot = FindRepositoryRoot();
        string script = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "tools",
            "Validation",
            "Test-CorePortabilityHeadless.ps1"));

        Assert.Contains("if ($IsWindows)", script, StringComparison.Ordinal);
        Assert.Contains("\"cda07-$([Guid]::NewGuid()", script, StringComparison.Ordinal);
        Assert.Contains("$Value.Replace($workRootPath, '<runtime-root>'", script, StringComparison.Ordinal);
        Assert.Contains("@($workRootPath)", script, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "$buildArtifactsRoot = Join-Path $outputRootPath 'build-artifacts'",
            script,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        string? configuredRoot = Environment.GetEnvironmentVariable(RepositoryRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            string resolvedRoot = Path.GetFullPath(configuredRoot);
            if (File.Exists(Path.Combine(resolvedRoot, "CanDoItAll.slnx")))
            {
                return resolvedRoot;
            }
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the CanDoItAll repository root.");
    }
}
