using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.Infrastructure;

public sealed class CrossPlatformCiWorkflowTests
{
    private const string SourceAssetStepName = "Verify committed BaseLib source assets";

    [Fact]
    [Trait("Category", "UnixPortabilityCore")]
    public void Active_workflow_satisfies_the_sibling_source_platform_and_gate_policy()
    {
        string workflow = ReadActiveWorkflow();

        IReadOnlyList<string> violations = CiWorkflowPolicy.Validate(workflow);

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    [Trait("Category", "UnixPortabilityCore")]
    public void Moving_a_pin_to_another_immutable_commit_needs_no_second_edit()
    {
        string workflow = ReadActiveWorkflow();
        string moved = ReplacePin(workflow, "CANDOITALL_FILETOOLS_COMMIT", "0123456789abcdef0123456789abcdef01234567");

        Assert.NotEqual(workflow, moved);
        Assert.Empty(CiWorkflowPolicy.Validate(moved));
    }

    [Theory]
    [Trait("Category", "UnixPortabilityCore")]
    [InlineData("branch-pin")]
    [InlineData("literal-checkout-ref")]
    [InlineData("pr-source-branch")]
    [InlineData("missing-resolver-dependency")]
    [InlineData("broad-file-copy")]
    [InlineData("missing-source-asset-check")]
    [InlineData("baseline-auto-acceptance")]
    [InlineData("package-substitution")]
    [InlineData("quarantine-in-stable-gate")]
    [InlineData("quarantine-in-host-lane")]
    [InlineData("dropped-linux-shard")]
    [InlineData("dropped-host-shard")]
    [InlineData("linux-host-platform-only")]
    [InlineData("host-lane-without-windows")]
    [InlineData("full-scope-never-runs")]
    [InlineData("main-runs-split-scope")]
    public void Weakened_workflow_is_rejected(string weakening)
    {
        string workflow = ReadActiveWorkflow();
        string weakened = weakening switch
        {
            "branch-pin" => ReplacePin(workflow, "CANDOITALL_FILETOOLS_COMMIT", "development"),
            "literal-checkout-ref" => ReplaceFirst(
                workflow,
                "ref: ${{ needs.dependencies.outputs.components-commit }}",
                "ref: development"),
            "pr-source-branch" => ReplaceFirst(workflow, "github.base_ref || github.ref_name", "github.head_ref || github.ref_name"),
            "missing-resolver-dependency" => ReplaceFirst(workflow, "    needs: dependencies\n", string.Empty),
            "broad-file-copy" => ReplaceFirst(workflow, "CANDOITALL_TESTS_POSTGRES_CREATE_STRATEGY: WAL_LOG", "CANDOITALL_TESTS_POSTGRES_CREATE_STRATEGY: FILE_COPY"),
            "missing-source-asset-check" => RemoveFirstStep(workflow, SourceAssetStepName),
            "baseline-auto-acceptance" => ReplaceFirst(
                workflow,
                "--baseline ./tools/Validation/Portability/portability-risk-baseline.json",
                "--baseline ./tools/Validation/Portability/portability-risk-baseline.json --write-baseline"),
            "package-substitution" => ReplaceFirst(
                workflow,
                "dotnet restore ./CanDoItAll.slnx -p:UseLocalCanDoItAllLibraries=true",
                "dotnet restore ./CanDoItAll.slnx -p:UseLocalCanDoItAllLibraries=false"),
            "quarantine-in-stable-gate" => ReplaceFirst(workflow, "&Category!=Quarantined", string.Empty),
            "quarantine-in-host-lane" => ReplaceFirst(
                workflow,
                "Category!=Quarantined&Category!=UnixRuntimePortability&RequiresHostDocker!=true&Category=HostPlatform",
                "Category!=UnixRuntimePortability&RequiresHostDocker!=true&Category=HostPlatform"),
            "dropped-linux-shard" => ReplaceFirst(workflow, "shard: [1, 2, 3, 4, 5]", "shard: [1, 2, 3, 4]"),
            "dropped-host-shard" => ReplaceFirst(workflow, "shard: [1, 2, 3]\n", "shard: [1, 2]\n"),
            "linux-host-platform-only" => ReplaceFirst(
                workflow,
                "RequiresHostDocker!=true\"\n          -ShardIndex ${{ matrix.shard }} -ShardCount 5",
                "RequiresHostDocker!=true&Category=HostPlatform\"\n          -ShardIndex ${{ matrix.shard }} -ShardCount 5"),
            "host-lane-without-windows" => ReplaceFirst(workflow, "            os: windows-latest\n          - name: macos-arm64\n            os: macos-15\n    runs-on", "            os: macos-15\n          - name: macos-arm64\n            os: macos-15\n    runs-on"),
            "full-scope-never-runs" => ReplaceFirst(workflow, "if: needs.dependencies.outputs.platform-scope == 'full'", "if: false"),
            "main-runs-split-scope" => ReplaceFirst(workflow, "$env:GITHUB_REF -eq 'refs/heads/main'", "$env:GITHUB_REF -eq 'refs/heads/release'"),
            _ => throw new ArgumentOutOfRangeException(nameof(weakening))
        };

        Assert.NotEqual(workflow, weakened);
        Assert.NotEmpty(CiWorkflowPolicy.Validate(weakened));
    }

    [Fact]
    [Trait("Category", "UnixPortabilityCore")]
    public void Build_graph_consumes_sibling_sources_instead_of_packages()
    {
        string repositoryRoot = TestRepositoryRoot.Find();

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

        string dockerfile = File.ReadAllText(Path.Combine(repositoryRoot, "src", "App", "CanDoItAll.Web", "Dockerfile"));
        Assert.Contains(
            "COPY --from=components --exclude=**/[Bb]in --exclude=**/[Oo]bj . /CanDoItAll.Components",
            dockerfile,
            StringComparison.Ordinal);
        Assert.Contains(
            "COPY --from=filetools --exclude=**/[Bb]in --exclude=**/[Oo]bj . /CanDoItAll.FileTools",
            dockerfile,
            StringComparison.Ordinal);
        Assert.DoesNotContain("UseLocalCanDoItAllLibraries=false", dockerfile, StringComparison.Ordinal);

        string portabilityToolsPath = Path.Combine(repositoryRoot, "tools", "Validation", "Portability");
        Assert.True(File.Exists(Path.Combine(portabilityToolsPath, "scan_portability.py")));
        Assert.True(File.Exists(Path.Combine(portabilityToolsPath, "platform-sensitive-patterns.txt")));
        Assert.True(File.Exists(Path.Combine(portabilityToolsPath, "portability-risk-baseline.json")));
        Assert.Contains("UnixPortabilityBrowserSmoke", File.ReadAllText(Path.Combine(
            repositoryRoot, "tests", "Playwright", "CanDoItAll.Tests.Playwright", "CorePortabilityBrowserSmokeTests.cs")),
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "UnixPortabilityCore")]
    public void Runtime_portability_catalog_is_well_formed_and_its_runner_enforces_discovery()
    {
        // The exact case counts belong to the catalog and are compared with actual discovery by the runner itself
        // (its drift checks and -SelfTest); this test guards the catalog's shape instead of copying its numbers.
        string repositoryRoot = TestRepositoryRoot.Find();
        string script = File.ReadAllText(Path.Combine(repositoryRoot, "tools", "Validation", "Test-RuntimePortability.ps1"));
        foreach (string requirement in new[]
                 {
                     "sourceFingerprint", "dependencyMode", "assemblies", "[switch]$SkipBuild", "[switch]$BuildOnly",
                     "[switch]$SelfTest", "class selection drifted", "fully qualified test selection drifted",
                     "ValidateSet('All', 'Unit', 'Integration', 'Browser')", "--no-build", "--no-restore"
                 })
        {
            Assert.Contains(requirement, script, StringComparison.Ordinal);
        }

        using JsonDocument catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repositoryRoot, "tools", "Validation", "RuntimePortabilityCatalog.json")));
        JsonElement root = catalog.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("catalogVersion").GetString()));
        Assert.Equal("Category=UnixRuntimePortability", root.GetProperty("traitFilter").GetString());
        JsonElement[] scopes = root.GetProperty("scopes").EnumerateArray().ToArray();
        Assert.Equal(
            new[] { "Browser", "Integration", "Unit" },
            scopes.Select(scope => scope.GetProperty("name").GetString()).Order(StringComparer.Ordinal));

        foreach (JsonElement scope in scopes)
        {
            string name = scope.GetProperty("name").GetString()!;
            string projectPath = scope.GetProperty("projectPath").GetString()!;
            Assert.True(File.Exists(TestRepositoryPath.Resolve(repositoryRoot, projectPath)), $"{name} project is missing: {projectPath}");
            string assemblyName = Path.GetFileNameWithoutExtension(projectPath);
            int expectedCases = scope.GetProperty("expectedCaseCount").GetInt32();
            string[] classes = scope.GetProperty("expectedClasses").EnumerateArray().Select(value => value.GetString()!).ToArray();
            string[] names = scope.GetProperty("expectedFullyQualifiedNames").EnumerateArray().Select(value => value.GetString()!).ToArray();

            Assert.True(expectedCases > 0, $"{name} must select at least one case.");
            Assert.NotEmpty(classes);
            Assert.Equal(classes.Length, classes.Distinct(StringComparer.Ordinal).Count());
            Assert.All(classes, value => Assert.StartsWith(assemblyName + ".", value, StringComparison.Ordinal));
            Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
            Assert.True(names.Length <= expectedCases, $"{name} lists more fully qualified cases than it expects.");
            Assert.All(names, value => Assert.Contains(value[..value.LastIndexOf('.')], classes));
        }
    }

    [Fact]
    [Trait("Category", "UnixPortabilityCore")]
    public void Headless_validator_uses_a_short_redacted_windows_work_root()
    {
        string repositoryRoot = TestRepositoryRoot.Find();
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

    private static string ReadActiveWorkflow()
    {
        string repositoryRoot = TestRepositoryRoot.Find();
        string workflowPath = Path.Combine(repositoryRoot, ".github", "workflows", "ci.yml");
        Assert.True(File.Exists(workflowPath), $"Active CI workflow is missing: {workflowPath}");
        Assert.False(
            File.Exists(Path.Combine(repositoryRoot, ".github", "workflows-disabled", "ci.yml")),
            "A disabled copy of the CI workflow still exists.");
        return File.ReadAllText(workflowPath).Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static string ReplacePin(string workflow, string key, string value)
        => Regex.Replace(workflow, $"(?m)^(  {Regex.Escape(key)}:)[ \\t]*\\S+[ \\t]*$", $"$1 {value}");

    private static string ReplaceFirst(string text, string value, string replacement)
    {
        int index = text.IndexOf(value, StringComparison.Ordinal);
        return index < 0 ? text : text[..index] + replacement + text[(index + value.Length)..];
    }

    private static string RemoveFirstStep(string workflow, string stepName)
    {
        string header = $"      - name: {stepName}\n";
        int start = workflow.IndexOf(header, StringComparison.Ordinal);
        if (start < 0)
        {
            return workflow;
        }

        int next = workflow.IndexOf("\n      - name: ", start + header.Length, StringComparison.Ordinal);
        return next < 0 ? workflow[..start] : workflow[..start] + workflow[(next + 1)..];
    }

    private static class CiWorkflowPolicy
    {
        private const string ComponentsRepository = "fyziktom/CanDoItAll.Components";
        private const string FileToolsPinKey = "CANDOITALL_FILETOOLS_COMMIT";
        private const string ComponentsBranchRef = "${{ github.base_ref || github.ref_name }}";
        private const string ComponentsCommitRef = "${{ needs.dependencies.outputs.components-commit }}";
        private static readonly string[] Siblings = [ComponentsRepository, "fyziktom/CanDoItAll.FileTools"];

        private static readonly string[] StableGateExclusions =
        [
            "Category!=Playwright", "Category!=LiveProcess", "Category!=LongRunning", "Category!=Quarantined",
            "Category!=UnixRuntimePortability", "RequiresHostDocker!=true"
        ];

        private static readonly string[] RequiredCommands =
        [
            "  stable:\n    needs: dependencies",
            "  containers:\n    needs: dependencies",
            "components-commit: ${{ steps.components.outputs.commit }}",
            "git -C CanDoItAll.Components rev-parse --verify HEAD",
            "Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value \"commit=$commit\"",
            "  tests-linux:\n    needs: dependencies",
            "  tests-host:\n    needs: dependencies",
            "platform-scope: ${{ steps.scope.outputs.scope }}",
            "$env:GITHUB_EVENT_NAME -eq 'schedule'",
            "$env:GITHUB_REF -eq 'refs/heads/main'",
            "dotnet test ./tests/Solutions/CanDoItAll.Tests.Unit.slnx",
            "dotnet test ./tests/Solutions/CanDoItAll.Tests.Memory.slnx",
            "./tools/Validation/Invoke-TestShard.ps1",
            "-WeightsPath ./tools/Validation/TestShardWeights.json",
            "(Category=UnixPortabilityCore)&(Category!=UnixRuntimePortability)&(RequiresHostDocker!=true)",
            "(Category=UnixPortabilityCore)&(RequiresHostDocker=true)",
            "ikalnytskyi/action-setup-postgres@",
            "postgres-version: \"18\"",
            "Verify PostgreSQL 18 server provenance",
            "show server_version_num",
            "CANDOITALL_TESTS_POSTGRES_CONNECTION",
            "playwright.ps1 install chromium",
            "Test-RuntimePortability.ps1",
            "Test-CorePortabilityHeadless.ps1",
            "python ./tools/Validation/Portability/test_enforce_portability_baseline.py",
            "python ./tools/Validation/Portability/test_scan_artifacts_for_secrets.py",
            "python ./tools/Validation/Portability/scan_portability.py",
            "python ./tools/Validation/Portability/enforce_portability_baseline.py",
            "--baseline ./tools/Validation/Portability/portability-risk-baseline.json",
            "Test-Documentation.ps1",
            "Test-Docker.ps1 -RunNegativeFixtures",
            "actions/upload-artifact@",
            "Remove-Item -LiteralPath .secrets/db-password"
        ];

        private static readonly string[] ForbiddenFragments =
        [
            "UseLocalCanDoItAllLibraries=false",
            "-UseLocalCanDoItAllLibraries $false",
            "CANDOITALL_TESTS_POSTGRES_CREATE_STRATEGY: FILE_COPY",
            "--write-baseline",
            "codex/bundles",
            "continue-on-error: true"
        ];

        public static IReadOnlyList<string> Validate(string workflowText)
        {
            string workflow = workflowText.Replace("\r\n", "\n", StringComparison.Ordinal);
            var violations = new List<string>();

            Match pin = Regex.Match(workflow, $"(?m)^  {FileToolsPinKey}:[ \\t]*(\\S+)[ \\t]*$");
            if (!pin.Success || !Regex.IsMatch(pin.Groups[1].Value, "^[0-9a-f]{40}$")) {
                violations.Add($"{FileToolsPinKey} must declare an immutable 40-character commit.");
            }

            foreach (var (jobName, steps) in Jobs(workflow))
            {
                ValidateJob(jobName, steps, violations);
            }

            ValidatePlatformMatrix(workflow, violations);
            ValidateTestLanes(workflow, violations);
            string[] stableFilters = Regex.Matches(workflow, "(?:--filter|-Filter) \"([^\"]*Category!=Playwright[^\"]*)\"")
                .Select(match => match.Groups[1].Value)
                .ToArray();
            if (stableFilters.Length == 0)
            {
                violations.Add("No stable test lane filter is wired.");
            }

            foreach (string exclusion in StableGateExclusions)
            {
                if (stableFilters.Any(filter => !filter.Split('&').Contains(exclusion, StringComparer.Ordinal)))
                {
                    violations.Add($"A stable test lane filter no longer excludes {exclusion}.");
                }
            }

            violations.AddRange(RequiredCommands
                .Where(command => !workflow.Contains(command, StringComparison.Ordinal))
                .Select(command => $"Required gate command is not wired: {command}"));
            violations.AddRange(ForbiddenFragments
                .Where(fragment => workflow.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                .Select(fragment => $"Forbidden workflow fragment is present: {fragment}"));
            return violations;
        }

        private static void ValidateJob(string jobName, IReadOnlyList<string> steps, List<string> violations)
        {
            int componentsCheckout = -1;
            int firstBuild = -1;
            int assetCheck = -1;
            for (int index = 0; index < steps.Count; index++)
            {
                string step = steps[index];
                foreach (var repository in Siblings)
                {
                    if (!step.Contains($"repository: {repository}", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string checkoutPath = repository[(repository.IndexOf('/') + 1)..];
                    string expectedRef = repository == ComponentsRepository
                        ? jobName == "dependencies" ? ComponentsBranchRef : ComponentsCommitRef
                        : $"${{{{ env.{FileToolsPinKey} }}}}";
                    if (!step.Contains($"ref: {expectedRef}", StringComparison.Ordinal)) {
                        violations.Add($"Job {jobName} checks out {repository} without consuming {expectedRef}.");
                    }

                    if (!step.Contains($"path: {checkoutPath}", StringComparison.Ordinal))
                    {
                        violations.Add($"Job {jobName} checks out {repository} outside the sibling path {checkoutPath}.");
                    }

                    if (repository == ComponentsRepository && componentsCheckout < 0)
                    {
                        componentsCheckout = index;
                    }
                }

                if (step.StartsWith($"name: {SourceAssetStepName}", StringComparison.Ordinal) &&
                    step.Contains("ls-files --error-unmatch", StringComparison.Ordinal) &&
                    step.Contains("Length -eq 0", StringComparison.Ordinal))
                {
                    assetCheck = assetCheck < 0 ? index : assetCheck;
                }

                bool builds = Regex.IsMatch(step, @"dotnet (restore|build|test)\b|docker compose");
                if (builds && firstBuild < 0)
                {
                    firstBuild = index;
                }

                if (Regex.IsMatch(step, @"dotnet (restore|build|test)\b") &&
                    !step.Contains("UseLocalCanDoItAllLibraries=true", StringComparison.Ordinal))
                {
                    violations.Add($"Job {jobName} step '{StepName(step)}' builds without the pinned sibling sources.");
                }
            }

            if (componentsCheckout >= 0 && firstBuild >= 0 &&
                (assetCheck < 0 || assetCheck < componentsCheckout || assetCheck > firstBuild))
            {
                violations.Add($"Job {jobName} does not verify committed Components source assets before building.");
            }
        }

        private static void ValidatePlatformMatrix(string workflow, List<string> violations)
        {
            if (!workflow.Contains("fail-fast: false", StringComparison.Ordinal))
            {
                violations.Add("The platform matrix must not cancel the other platforms after one failure.");
            }

            string stableJob = JobText(workflow, "stable");
            string[] systems = Regex.Matches(stableJob, @"(?m)^\s+os: (\S+)\s*$").Select(match => match.Groups[1].Value).ToArray();
            foreach (string family in new[] { "windows-", "ubuntu-", "macos-" })
            {
                if (!systems.Any(system => system.StartsWith(family, StringComparison.Ordinal)))
                {
                    violations.Add($"The platform matrix no longer covers {family.TrimEnd('-')}.");
                }
            }

            string[] strategies = Regex.Matches(stableJob, @"(?m)^\s+postgres-create-strategy: (\S+)\s*$")
                .Select(match => match.Groups[1].Value)
                .ToArray();
            if (strategies.Length != systems.Length ||
                strategies.Any(strategy => strategy is not ("WAL_LOG" or "FILE_COPY")) ||
                !strategies.Contains("WAL_LOG") ||
                !strategies.Contains("FILE_COPY"))
            {
                violations.Add("Every matrix platform must choose a supported PostgreSQL create strategy, and both strategies must run.");
            }

            if (!workflow.Contains("      CANDOITALL_TESTS_POSTGRES_CREATE_STRATEGY: WAL_LOG", StringComparison.Ordinal)) {
                violations.Add("The broad stable gate must use WAL_LOG rather than forcing checkpoints for every database.");
            }
            var postgresGate = Jobs(workflow).Single(job => job.Name == "stable").Steps
                .Single(step => step.StartsWith("name: Run PostgreSQL-backed core migration and restart gate", StringComparison.Ordinal));
            if (!postgresGate.Contains(
                    "CANDOITALL_TESTS_POSTGRES_CREATE_STRATEGY: ${{ matrix.postgres-create-strategy }}",
                    StringComparison.Ordinal))
            {
                violations.Add("The matrix PostgreSQL create strategy is not passed to the tests.");
            }
        }

        // Linux shards run every stable Components and Integration test; Windows and macOS shards run the host-platform
        // classes, or everything in the full scope. A shard count that differs from its matrix would skip classes.
        private static void ValidateTestLanes(string workflow, List<string> violations)
        {
            foreach (string jobName in new[] { "tests-linux", "tests-host" })
            {
                string job = JobText(workflow, jobName);
                Match shards = Regex.Match(job, @"(?m)^\s+shard: \[([0-9, ]+)\]\s*$");
                int[] shardValues = shards.Success
                    ? shards.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray()
                    : [];
                string[] invocations = Regex.Split(job, @"(?m)^      - ")
                    .Where(step => step.Contains("./tools/Validation/Invoke-TestShard.ps1", StringComparison.Ordinal))
                    .ToArray();
                if (invocations.Length == 0 || shardValues.Length == 0 ||
                    !shardValues.SequenceEqual(Enumerable.Range(1, shardValues.Length)))
                {
                    violations.Add($"Job {jobName} must run Invoke-TestShard.ps1 over a contiguous shard matrix starting at 1.");
                    continue;
                }

                foreach (string invocation in invocations)
                {
                    Match count = Regex.Match(invocation, @"-ShardIndex \$\{\{ matrix\.shard \}\} -ShardCount (\d+)");
                    if (!count.Success || int.Parse(count.Groups[1].Value) != shardValues.Length)
                    {
                        violations.Add($"Job {jobName} step '{StepName(invocation)}' does not partition over its {shardValues.Length} matrix shards.");
                    }

                    foreach (string project in new[] { "CanDoItAll.Tests.Components.csproj", "CanDoItAll.Tests.Integration.csproj" })
                    {
                        if (!invocation.Contains(project, StringComparison.Ordinal))
                        {
                            violations.Add($"Job {jobName} step '{StepName(invocation)}' does not shard {project}.");
                        }
                    }
                }

                bool[] hostOnly = invocations.Select(step => step.Contains("&Category=HostPlatform\"", StringComparison.Ordinal)).ToArray();
                if (jobName == "tests-linux" && hostOnly.Any(value => value))
                {
                    violations.Add("Linux shards must run every stable Components and Integration test, not only host-platform classes.");
                }

                if (jobName == "tests-host")
                {
                    foreach (string family in new[] { "os: windows-", "os: macos-" })
                    {
                        if (!job.Contains(family, StringComparison.Ordinal))
                        {
                            violations.Add($"The host-platform lane no longer covers {family[4..].TrimEnd('-')}.");
                        }
                    }

                    bool split = invocations.Any(step => step.Contains("&Category=HostPlatform\"", StringComparison.Ordinal) &&
                        step.Contains("if: needs.dependencies.outputs.platform-scope != 'full'", StringComparison.Ordinal));
                    bool full = invocations.Any(step => !step.Contains("Category=HostPlatform", StringComparison.Ordinal) &&
                        step.Contains("if: needs.dependencies.outputs.platform-scope == 'full'", StringComparison.Ordinal));
                    if (!split || !full)
                    {
                        violations.Add("The host-platform lane must run host-platform classes in the split scope and every stable test in the full scope.");
                    }
                }
            }
        }

        private static string JobText(string workflow, string jobName)
        {
            int jobsStart = workflow.IndexOf("\njobs:\n", StringComparison.Ordinal);
            Match header = Regex.Match(workflow[Math.Max(jobsStart, 0)..], $@"(?m)^  {Regex.Escape(jobName)}:\s*$");
            if (jobsStart < 0 || !header.Success)
            {
                return string.Empty;
            }

            int start = jobsStart + header.Index;
            Match next = Regex.Match(workflow[(start + header.Length)..], @"(?m)^  [A-Za-z0-9_-]+:\s*$");
            return next.Success ? workflow[start..(start + header.Length + next.Index)] : workflow[start..];
        }

        private static IEnumerable<(string Name, IReadOnlyList<string> Steps)> Jobs(string workflow)
        {
            int jobsStart = workflow.IndexOf("\njobs:\n", StringComparison.Ordinal);
            if (jobsStart < 0)
            {
                yield break;
            }

            MatchCollection headers = Regex.Matches(workflow[jobsStart..], @"(?m)^  ([A-Za-z0-9_-]+):\s*$");
            for (int index = 0; index < headers.Count; index++)
            {
                int start = jobsStart + headers[index].Index;
                int end = index + 1 < headers.Count ? jobsStart + headers[index + 1].Index : workflow.Length;
                string[] steps = Regex.Split(workflow[start..end], @"(?m)^      - ")
                    .Skip(1)
                    .ToArray();
                yield return (headers[index].Groups[1].Value, steps);
            }
        }

        private static string StepName(string step)
        {
            Match name = Regex.Match(step, @"^name: (.+)$", RegexOptions.Multiline);
            return name.Success ? name.Groups[1].Value.Trim() : "(unnamed)";
        }
    }
}
