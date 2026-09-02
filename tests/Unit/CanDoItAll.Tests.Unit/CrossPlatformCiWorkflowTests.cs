namespace CanDoItAll.Tests.Unit.Infrastructure;

public sealed class CrossPlatformCiWorkflowTests
{
    private const string RepositoryRootEnvironmentVariable = "CANDOITALL_TEST_REPOSITORY_ROOT";

    [Fact]
    [Trait("Category", "UnixPortabilityCore")]
    public void Active_workflow_defines_three_actual_host_gates_with_pinned_sibling_sources()
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
        Assert.Contains("UseLocalCanDoItAllLibraries=true", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("UseLocalCanDoItAllLibraries=false", workflow, StringComparison.Ordinal);
        Assert.Contains("repository: fyziktom/CanDoItAll.Components", workflow, StringComparison.Ordinal);
        Assert.Contains("repository: fyziktom/CanDoItAll.FileTools", workflow, StringComparison.Ordinal);
        Assert.Contains("path: CanDoItAll.Components", workflow, StringComparison.Ordinal);
        Assert.Contains("path: CanDoItAll.FileTools", workflow, StringComparison.Ordinal);
        Assert.Contains("CANDOITALL_COMPONENTS_COMMIT", workflow, StringComparison.Ordinal);
        Assert.Contains("CANDOITALL_FILETOOLS_COMMIT", workflow, StringComparison.Ordinal);
        Assert.Contains("CANDOITALL_COMPONENTS_COMMIT: c3e6aa03a878994c0ba8aed6af017d0be75f3796", workflow, StringComparison.Ordinal);
        Assert.Contains("CANDOITALL_FILETOOLS_COMMIT: 498b36825bd5a5222429972af120b04becf4b3f6", workflow, StringComparison.Ordinal);
        Assert.Equal(2, workflow.Split("- name: Verify committed BaseLib source assets", StringSplitOptions.None).Length - 1);
        Assert.Contains("src/CanDoItAll.Components.BaseLib/wwwroot/css/material-symbols.css", workflow, StringComparison.Ordinal);
        Assert.Contains("src/CanDoItAll.Components.BaseLib/wwwroot/css/output.css", workflow, StringComparison.Ordinal);
        Assert.Contains("git -C ../CanDoItAll.Components ls-files --error-unmatch -- $asset", workflow, StringComparison.Ordinal);
        Assert.Contains("(Get-Item -LiteralPath $assetPath).Length -eq 0", workflow, StringComparison.Ordinal);
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
        string portabilityToolsPath = Path.Combine(repositoryRoot, "tools", "Validation", "Portability");
        Assert.True(File.Exists(Path.Combine(portabilityToolsPath, "scan_portability.py")));
        Assert.True(File.Exists(Path.Combine(portabilityToolsPath, "platform-sensitive-patterns.txt")));
        Assert.True(File.Exists(Path.Combine(portabilityToolsPath, "portability-risk-baseline.json")));
        Assert.Contains(
            "python ./tools/Validation/Portability/test_enforce_portability_baseline.py",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "python ./tools/Validation/Portability/test_scan_artifacts_for_secrets.py",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "python ./tools/Validation/Portability/scan_portability.py",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "python ./tools/Validation/Portability/enforce_portability_baseline.py",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "--baseline ./tools/Validation/Portability/portability-risk-baseline.json",
            workflow,
            StringComparison.Ordinal);
        Assert.DoesNotContain("codex/bundles", workflow, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("Create disposable Compose database secret", workflow, StringComparison.Ordinal);
        Assert.Contains(".secrets/db-password", workflow, StringComparison.Ordinal);
        Assert.Contains("Test-Docker.ps1 -RunNegativeFixtures", workflow, StringComparison.Ordinal);
        Assert.Contains("up -d --build --wait --wait-timeout 360", workflow, StringComparison.Ordinal);
        Assert.Contains("Remove-Item -LiteralPath .secrets/db-password", workflow, StringComparison.Ordinal);

        string buildTargets = File.ReadAllText(Path.Combine(repositoryRoot, "Directory.Build.targets"));
        Assert.Contains("Exclude=\"CanDoItAll.Components.*\"", buildTargets, StringComparison.Ordinal);
        Assert.Contains("Exclude=\"CanDoItAll.FileTools.*\"", buildTargets, StringComparison.Ordinal);
        Assert.Contains("CanDoItAllLocalLibrary", buildTargets, StringComparison.Ordinal);
        Assert.Contains("ValidateResolvedLocalCanDoItAllLibraryReferences", buildTargets, StringComparison.Ordinal);
        Assert.Contains("_UnconvertedCanDoItAllLibraryPackage", buildTargets, StringComparison.Ordinal);
        Assert.Contains("_MissingCanDoItAllLocalProject", buildTargets, StringComparison.Ordinal);
        Assert.DoesNotContain("WithMetadataValue('Identity'", buildTargets, StringComparison.Ordinal);

        string compose = File.ReadAllText(Path.Combine(repositoryRoot, "compose.yaml"));
        Assert.Contains("components: ../CanDoItAll.Components", compose, StringComparison.Ordinal);
        Assert.Contains("filetools: ../CanDoItAll.FileTools", compose, StringComparison.Ordinal);

        string dockerfile = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "App",
            "CanDoItAll.Web",
            "Dockerfile"));
        Assert.Contains(
            "COPY --from=components --exclude=**/[Bb]in --exclude=**/[Oo]bj . /CanDoItAll.Components",
            dockerfile,
            StringComparison.Ordinal);
        Assert.Contains(
            "COPY --from=filetools --exclude=**/[Bb]in --exclude=**/[Oo]bj . /CanDoItAll.FileTools",
            dockerfile,
            StringComparison.Ordinal);
        Assert.DoesNotContain("UseLocalCanDoItAllLibraries=false", dockerfile, StringComparison.Ordinal);
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
        using System.Text.Json.JsonDocument catalog = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repositoryRoot,
            "tools",
            "Validation",
            "RuntimePortabilityCatalog.json")));

        Assert.Contains("sourceFingerprint", script, StringComparison.Ordinal);
        Assert.Contains("dependencyMode", script, StringComparison.Ordinal);
        Assert.Contains("assemblies", script, StringComparison.Ordinal);
        Assert.Contains("[switch]$SkipBuild", script, StringComparison.Ordinal);
        Assert.Contains("[switch]$BuildOnly", script, StringComparison.Ordinal);
        Assert.Contains("[switch]$SelfTest", script, StringComparison.Ordinal);
        Assert.Contains("class selection drifted", script, StringComparison.Ordinal);
        Assert.Contains("fully qualified test selection drifted", script, StringComparison.Ordinal);
        Assert.Contains("ValidateSet('All', 'Unit', 'Integration', 'Browser')", script, StringComparison.Ordinal);
        Assert.Contains("--no-build", script, StringComparison.Ordinal);
        Assert.Contains("--no-restore", script, StringComparison.Ordinal);

        System.Text.Json.JsonElement catalogRoot = catalog.RootElement;
        Assert.Equal(1, catalogRoot.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("2026-08-19.1", catalogRoot.GetProperty("catalogVersion").GetString());
        Assert.Equal("Category=UnixRuntimePortability", catalogRoot.GetProperty("traitFilter").GetString());
        System.Text.Json.JsonElement[] scopes = catalogRoot.GetProperty("scopes").EnumerateArray().ToArray();
        Assert.Equal(3, scopes.Length);
        Assert.Equal(429, FindScope(scopes, "Unit").GetProperty("expectedCaseCount").GetInt32());
        Assert.Equal(45, FindScope(scopes, "Integration").GetProperty("expectedCaseCount").GetInt32());
        System.Text.Json.JsonElement browserScope = FindScope(scopes, "Browser");
        Assert.Equal(1, browserScope.GetProperty("expectedCaseCount").GetInt32());
        Assert.Equal(
            "CanDoItAll.Tests.Playwright.Smoke.AppSmokeTests.Runtime_node_actions_show_direct_optional_and_dependency_missing_states",
            Assert.Single(browserScope.GetProperty("expectedFullyQualifiedNames").EnumerateArray()).GetString());
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

    private static System.Text.Json.JsonElement FindScope(
        IEnumerable<System.Text.Json.JsonElement> scopes,
        string name)
    {
        return Assert.Single(scopes, scope => string.Equals(
            scope.GetProperty("name").GetString(),
            name,
            StringComparison.Ordinal));
    }
}
