using System.Runtime.CompilerServices;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessRuntimeHostCodeFirstGuardTests
{
    private const string CodexSegment = "codex";
    private const string BundlesSegment = "bundles";
    private const int RequiredSourceTestDominanceMultiplier = 5;

    private static readonly string[] ConcreteBundlePathFragments =
    [
        $"{CodexSegment}/{BundlesSegment}",
        $@"{CodexSegment}\{BundlesSegment}"
    ];

    private static readonly string[] ConcretePathApiTokens =
    [
        "Path.Combine",
        "Path.Join",
        "Directory.EnumerateFiles",
        "Directory.EnumerateDirectories",
        "Directory.GetFiles",
        "Directory.GetDirectories",
        "Directory.Exists",
        "File.ReadAllText",
        "File.ReadAllLines",
        "File.Exists",
        "File.OpenRead"
    ];

    private static readonly LongRunningE2EProofContract[] LongRunningTemplateAutomationProofs =
    [
        new(
            "Blazor_app_delivery_template_SB03_INV_001_completes_through_automation_dispatch_finalizer_and_readback",
            "SB03_INV_001"),
        new(
            "Software_delivery_template_SB04_INV_001_completes_multi_team_governance_through_automation_dispatch",
            "SB04_INV_001")
    ];

    private static readonly string[] ProductionAutomationPathTokens =
    [
        "CreateLaunchPlanAsync",
        "SelectProcessMockLaunchCandidatesAsync",
        "SubmitLaunchPlanForApprovalAsync",
        "DecideLaunchPlanApprovalAsync",
        "ExecuteLaunchPlanAsync",
        "ProcessPendingAsync",
        "ListExecutionRunsAsync"
    ];

    [Fact]
    public void Process_runtime_host_codefirst_SB01_INV_001_source_inventory_is_not_bundle_path_coupled()
    {
        var root = FindRepositoryRoot();
        var sourceFiles = new[]
        {
            Path.Combine(root, "src", "CanDoItAll.Processes.Contracts", "Runtime", "ProcessRuntimeHostContractModels.cs"),
            Path.Combine(root, "src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessVerificationHostCapabilityCatalog.cs"),
            Path.Combine(root, "src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessVerificationRuntimeHost.cs"),
            Path.Combine(root, "src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessVerificationAuditStore.cs"),
            Path.Combine(root, "src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessManagerReadOnlyVerificationCommandService.cs"),
            Path.Combine(root, "src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessReadOnlyVerificationJobRunner.cs"),
            Path.Combine(root, "src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessDryRunExecutionHost.cs"),
            Path.Combine(root, "src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessDryRunExecutionPipeline.cs")
        };
        var missingFiles = sourceFiles
            .Where(path => !File.Exists(path))
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();
        var bundleCoupledFiles = sourceFiles
            .Where(path => File.Exists(path))
            .Where(path => ContainsConcreteBundlePath(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        Assert.Empty(missingFiles);
        Assert.Empty(bundleCoupledFiles);
    }

    [Fact]
    public void Process_runtime_host_codefirst_SB01_INV_002_long_lived_runtime_host_tests_do_not_read_concrete_bundle_files()
    {
        var root = FindRepositoryRoot();
        var coupledFiles = EnumerateRuntimeHostGuardedFiles(root)
            .Select(path => new
            {
                Path = path,
                Couplings = FindConcreteBundleFileReadCouplings(File.ReadAllText(path))
            })
            .Where(result => result.Couplings.Count > 0)
            .Select(result => $"{Path.GetRelativePath(root, result.Path)}: {string.Join("; ", result.Couplings)}")
            .ToArray();

        Assert.Empty(coupledFiles);
    }

    [Fact]
    public void Process_runtime_host_codefirst_SB01_INV_006_numstat_command_requires_explicit_current_bundle_start_sha()
    {
        var arguments = ProcessRuntimeHostCodeFirstDiffSummary.BuildNumstatArguments("0123456");

        Assert.Equal(["diff", "--numstat", "0123456...HEAD"], arguments);
        Assert.Throws<ArgumentException>(() => ProcessRuntimeHostCodeFirstDiffSummary.BuildNumstatArguments(""));
        Assert.Throws<ArgumentException>(() => ProcessRuntimeHostCodeFirstDiffSummary.BuildNumstatArguments("origin/main"));
    }

    [Fact]
    public void Process_runtime_host_codefirst_SB01_INV_009_ratio_report_rejects_conservative_head_fallback_unless_blocked()
    {
        var blockedFallback = ProcessRuntimeHostCodeFirstClosureReport.FromLines(
        [
            "ExplicitBaseline: HEAD 69e601b28a26",
            "CommandPolicy: conservative worktree fallback because no bundle-start SHA was recorded in the prepared bundle.",
            "Decision: SB08 final closure is blocked by the code-first ratio gate under the conservative HEAD baseline."
        ]);
        var falseClosureFallback = ProcessRuntimeHostCodeFirstClosureReport.FromLines(
        [
            "ExplicitBaseline: HEAD 69e601b28a26",
            "CommandPolicy: conservative worktree fallback because no bundle-start SHA was recorded in the prepared bundle.",
            "Decision: Merge-ready for process runtime stabilization."
        ]);
        var explicitBaselineClosure = ProcessRuntimeHostCodeFirstClosureReport.FromLines(
        [
            "ExplicitBaseline: 430496c5e7217a847e9172dcc0c2fba57f75f75c",
            "CommandPolicy: explicit bundle start SHA.",
            "Decision: Merge-ready for process runtime stabilization."
        ]);

        Assert.True(blockedFallback.IsPolicyConsistent);
        Assert.False(falseClosureFallback.IsPolicyConsistent);
        Assert.Contains(
            "Conservative HEAD fallback can only support a blocked release decision.",
            falseClosureFallback.BlockingReasons);
        Assert.True(explicitBaselineClosure.IsPolicyConsistent);
    }

    [Fact]
    public void Process_runtime_host_codefirst_SB01_INV_010_worktree_numstat_command_requires_explicit_start_sha()
    {
        var arguments = ProcessRuntimeHostCodeFirstDiffSummary.BuildWorktreeNumstatArguments("0123456");

        Assert.Equal(["diff", "--numstat", "0123456"], arguments);
        Assert.Throws<ArgumentException>(() => ProcessRuntimeHostCodeFirstDiffSummary.BuildWorktreeNumstatArguments(""));
        Assert.Throws<ArgumentException>(() => ProcessRuntimeHostCodeFirstDiffSummary.BuildWorktreeNumstatArguments("HEAD"));
        Assert.Throws<ArgumentException>(() => ProcessRuntimeHostCodeFirstDiffSummary.BuildWorktreeNumstatArguments("origin/main"));
    }

    [Fact]
    public void Process_runtime_host_codefirst_SB01_INV_007_long_running_template_e2e_proof_cites_production_dispatch_path()
    {
        var root = FindRepositoryRoot();
        var e2eSource = File.ReadAllText(Path.Combine(
            root,
            "tests",
            "CanDoItAll.Tests.Integration",
            "ProcessTemplateExecutionE2ETests.cs"));
        var supportSource = File.ReadAllText(Path.Combine(
            root,
            "tests",
            "CanDoItAll.Tests.Integration",
            "ProcessTemplateAutomationTestSupport.cs"));
        var missingProductionPathTokens = ProductionAutomationPathTokens
            .Where(token => !supportSource.Contains(token, StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(missingProductionPathTokens);

        foreach (var proof in LongRunningTemplateAutomationProofs)
        {
            var methodBody = ExtractMethodBody(e2eSource, proof.MethodName);

            Assert.Contains(proof.InvariantId, proof.MethodName, StringComparison.Ordinal);
            Assert.Contains("ExecuteTemplateWithProcessMockAgentsAsync", methodBody, StringComparison.Ordinal);
            Assert.Contains("AssertFinalizerSummaries", methodBody, StringComparison.Ordinal);
            Assert.Contains("AssertArtifact", methodBody, StringComparison.Ordinal);
            Assert.DoesNotContain("SuppressAutomationDispatch = true", methodBody, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Process_runtime_host_codefirst_SB01_INV_008_manual_contract_tests_are_not_counted_as_automation_proofs()
    {
        var root = FindRepositoryRoot();
        var e2eSource = File.ReadAllText(Path.Combine(
            root,
            "tests",
            "CanDoItAll.Tests.Integration",
            "ProcessTemplateExecutionE2ETests.cs"));
        var manualContractMethods = ExtractMethodNamesContaining(e2eSource, "SuppressAutomationDispatch = true");
        var proofMethodNames = LongRunningTemplateAutomationProofs
            .Select(proof => proof.MethodName)
            .ToHashSet(StringComparer.Ordinal);
        var manualMethodsCountedAsAutomationProofs = manualContractMethods
            .Where(proofMethodNames.Contains)
            .ToArray();

        Assert.Empty(manualMethodsCountedAsAutomationProofs);
        Assert.All(manualContractMethods, methodName =>
            Assert.Contains("manual_contract", methodName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Process_runtime_host_codefirst_SB03_INV_011_business_plan_postgres_automation_proof_is_not_manual_transition_contract()
    {
        var root = FindRepositoryRoot();
        var businessPlanSource = File.ReadAllText(Path.Combine(
            root,
            "tests",
            "CanDoItAll.Tests.Integration",
            "BusinessPlanProcessPostgresIntegrationTests.cs"));
        var automationMethodBody = ExtractMethodBody(
            businessPlanSource,
            "Business_plan_process_SB05_INV_001_completes_on_postgresql_through_automation_dispatch_finalizer_and_readback");

        Assert.Contains("ExecuteTemplateWithProcessMockAgentsAsync", automationMethodBody, StringComparison.Ordinal);
        Assert.Contains("AssertBusinessAutomationDispatchReadback", automationMethodBody, StringComparison.Ordinal);
        Assert.Contains("AssertPersistedBusinessAutomationReadbackAsync", automationMethodBody, StringComparison.Ordinal);
        Assert.Contains("AssertAutomationFinalizerSummaries", automationMethodBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SuppressAutomationDispatch = true", automationMethodBody, StringComparison.Ordinal);
        Assert.Contains(
            "Business_plan_process_manual_contract_runs_with_business_artifacts_evidence_and_statuses",
            businessPlanSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "Business_plan_process_manual_contract_projects_and_runs_on_postgresql",
            businessPlanSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "public async Task Business_plan_process_runs_with_business_artifacts_evidence_and_statuses",
            businessPlanSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "public async Task Business_plan_process_projects_and_runs_on_postgresql",
            businessPlanSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Process_runtime_host_codefirst_SB05_INV_004_scheduler_workflow_lifecycle_proof_uses_process_owned_readonly_paths() {
        var root = FindRepositoryRoot();
        var processServiceTestsSource = File.ReadAllText(Path.Combine(
            root,
            "tests",
            "CanDoItAll.Tests.Integration",
            "ProcessesServiceIntegrationTests.cs"));
        var readOnlyJobTestsSource = File.ReadAllText(Path.Combine(
            root,
            "tests",
            "CanDoItAll.Tests.Integration",
            "ProcessDomainEvidenceReadOnlyAdapterTests.cs"));
        var schedulerPlannerSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.SchedulerPlanner",
            "SchedulerPlannerService.cs"));
        var triggerMethodBody = ExtractMethodBody(
            processServiceTestsSource,
            "StartRunFromTriggerAsync_SB05_INV_001_starts_scheduler_and_workflow_origin_runs_through_process_owned_path_without_driver_hooks");
        var jobRunnerMethodBody = ExtractMethodBody(
            readOnlyJobTestsSource,
            "Process_readonly_verification_job_runner_SB05_INV_003_executes_scheduler_and_workflow_lifecycle_status_provenance_readback_without_mutation");
        var launchProcessStart = schedulerPlannerSource.IndexOf(
            "private async Task<SchedulerTargetLaunchResult> LaunchProcessAsync",
            StringComparison.Ordinal);
        Assert.True(launchProcessStart >= 0, "Scheduler process launcher method was not found.");
        var launchWorkflowStart = schedulerPlannerSource.IndexOf(
            "private async Task<SchedulerTargetLaunchResult> LaunchWorkflowAsync",
            launchProcessStart,
            StringComparison.Ordinal);
        Assert.True(launchWorkflowStart > launchProcessStart, "Scheduler workflow launcher method must follow the process launcher.");
        var launchProcessBody = schedulerPlannerSource[launchProcessStart..launchWorkflowStart];

        Assert.Contains("StartRunFromTriggerAsync", triggerMethodBody, StringComparison.Ordinal);
        Assert.Contains("ProcessRunTriggerSourceKind.SchedulerPlan", triggerMethodBody, StringComparison.Ordinal);
        Assert.Contains("ProcessRunTriggerSourceKind.WorkflowRun", triggerMethodBody, StringComparison.Ordinal);
        Assert.Contains("Assert.Empty(details.WorkflowRuns)", triggerMethodBody, StringComparison.Ordinal);
        Assert.Contains("Assert.Empty(details.ExecutionRuns)", triggerMethodBody, StringComparison.Ordinal);
        Assert.Contains("Assert.Empty(workflowLinks)", triggerMethodBody, StringComparison.Ordinal);
        Assert.Contains("ProcessOutboxRecordStatus.DeadLettered", triggerMethodBody, StringComparison.Ordinal);
        Assert.Contains("StartRunCommandKey", triggerMethodBody, StringComparison.Ordinal);
        Assert.Contains("AutomationDispatchCommandKey", triggerMethodBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SuppressAutomationDispatch = true", triggerMethodBody, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessExecutionCapable", triggerMethodBody, StringComparison.Ordinal);
        Assert.Contains("IProcessReadOnlyVerificationJobRunner", jobRunnerMethodBody, StringComparison.Ordinal);
        Assert.Contains("ProcessReadOnlyVerificationJobSourceKind.Scheduler", jobRunnerMethodBody, StringComparison.Ordinal);
        Assert.Contains("ProcessReadOnlyVerificationJobSourceKind.Workflow", jobRunnerMethodBody, StringComparison.Ordinal);
        Assert.Contains("ProcessRuntimeHostContractSurface.SchedulerWorkflowReadOnlyJob", jobRunnerMethodBody, StringComparison.Ordinal);
        Assert.Contains("ValidateReadOnlySafety", jobRunnerMethodBody, StringComparison.Ordinal);
        Assert.Contains("AuditRecordCount", jobRunnerMethodBody, StringComparison.Ordinal);
        Assert.Contains("NoMutationPerformed", jobRunnerMethodBody, StringComparison.Ordinal);
        Assert.DoesNotContain("AllowsProcessMutation, true", jobRunnerMethodBody, StringComparison.Ordinal);
        Assert.Contains("processesService.StartRunFromTriggerAsync", launchProcessBody, StringComparison.Ordinal);
        Assert.Contains("ProcessRunTriggerSourceKind.SchedulerPlan", launchProcessBody, StringComparison.Ordinal);
        Assert.Contains("SchedulerPlannerConstants.AutomationOwnerKey", launchProcessBody, StringComparison.Ordinal);
        Assert.DoesNotContain("workflowRuntimeManager", launchProcessBody, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessExecutionCapableDriver", launchProcessBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_runtime_host_codefirst_SB01_INV_003_numstat_summary_groups_source_tests_docs_and_bundle_lines()
    {
        var summary = ProcessRuntimeHostCodeFirstDiffSummary.FromNumstatLines(
        [
            "12\t3\tsrc/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDryRunExecutionHost.cs",
            "7\t1\ttests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs",
            "2\t2\tdocs/process-runtime-restoration-ledger.md",
            "1\t1\tcodex/bundles/process-driver-runtime-host-real-implementation-phase-v1/reviews/01-execution-report.md",
            "-\t-\toutput/binary-artifact.zip"
        ]);

        Assert.Equal(15, summary.SourceChangedLines);
        Assert.Equal(8, summary.TestChangedLines);
        Assert.Equal(4, summary.DocumentationChangedLines);
        Assert.Equal(2, summary.BundleChangedLines);
        Assert.Equal(23, summary.SourceAndTestChangedLines);
        Assert.Equal(0, summary.OtherChangedLines);
        Assert.True(summary.SatisfiesSourceTestDominance(RequiredSourceTestDominanceMultiplier));
    }

    [Fact]
    public void Process_runtime_host_codefirst_SB01_INV_004_numstat_summary_blocks_bundle_heavy_closure()
    {
        var summary = ProcessRuntimeHostCodeFirstDiffSummary.FromNumstatLines(
        [
            "3\t0\tsrc/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDryRunExecutionHost.cs",
            "1\t1\ttests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs",
            "5\t1\tcodex/bundles/process-driver-runtime-host-real-implementation-phase-v1/reviews/01-execution-report.md"
        ]);

        Assert.Equal(5, summary.SourceAndTestChangedLines);
        Assert.Equal(6, summary.BundleChangedLines);
        Assert.False(summary.SatisfiesSourceTestDominance(RequiredSourceTestDominanceMultiplier));
    }

    [Fact]
    public void Process_runtime_host_codefirst_SB01_INV_005_numstat_summary_accepts_exact_five_to_one_source_test_dominance()
    {
        var summary = ProcessRuntimeHostCodeFirstDiffSummary.FromNumstatLines(
        [
            "6\t4\tsrc/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDryRunExecutionHost.cs",
            "3\t2\ttests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs",
            "2\t1\tcodex/bundles/process-template-automation-e2e-multiteam-host-readiness-v1/reviews/01-execution-report.md"
        ]);

        Assert.Equal(15, summary.SourceAndTestChangedLines);
        Assert.Equal(3, summary.BundleChangedLines);
        Assert.True(summary.SatisfiesSourceTestDominance(RequiredSourceTestDominanceMultiplier));
    }

    private static bool ContainsConcreteBundlePath(string text)
    {
        return ConcreteBundlePathFragments.Any(fragment => text.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> FindConcreteBundleFileReadCouplings(string text)
    {
        return text
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .Where(IsConcreteBundleFileReadCoupling)
            .Select(line => line.Trim())
            .ToArray();
    }

    private static bool IsConcreteBundleFileReadCoupling(string line)
    {
        return line.Contains(CodexSegment, StringComparison.OrdinalIgnoreCase) &&
            line.Contains(BundlesSegment, StringComparison.OrdinalIgnoreCase) &&
            ConcretePathApiTokens.Any(token => line.Contains(token, StringComparison.Ordinal));
    }

    private static IEnumerable<string> EnumerateRuntimeHostGuardedFiles(string root)
    {
        var guardedDirectories = new[]
        {
            Path.Combine(root, "src", "CanDoItAll.Processes.Contracts", "Runtime"),
            Path.Combine(root, "src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch"),
            Path.Combine(root, "tests", "CanDoItAll.Tests.Integration")
        };

        return guardedDirectories
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.TopDirectoryOnly))
            .Where(path =>
                path.Contains($"{Path.DirectorySeparatorChar}src{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(path).StartsWith("Process", StringComparison.Ordinal));
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory(), Path.GetDirectoryName(sourceFilePath) ?? string.Empty })
        {
            if (string.IsNullOrWhiteSpace(startPath))
            {
                continue;
            }

            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private static string ExtractMethodBody(string source, string methodName)
    {
        var markerIndex = source.IndexOf(methodName, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            throw new InvalidOperationException($"Could not find method '{methodName}'.");
        }

        var bodyStart = source.IndexOf('{', markerIndex);
        if (bodyStart < 0)
        {
            throw new InvalidOperationException($"Could not find method body for '{methodName}'.");
        }

        var depth = 0;
        for (var index = bodyStart; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source[bodyStart..(index + 1)];
                }
            }
        }

        throw new InvalidOperationException($"Could not parse method body for '{methodName}'.");
    }

    private static IReadOnlyList<string> ExtractMethodNamesContaining(string source, string text)
    {
        var lines = source.Split(["\r\n", "\n"], StringSplitOptions.None);

        return lines
            .Select((line, index) => new { Line = line, Index = index })
            .Where(item => item.Line.Contains(text, StringComparison.Ordinal))
            .Select(item => FindNearestFactMethodName(lines, item.Index))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string FindNearestFactMethodName(IReadOnlyList<string> lines, int startIndex)
    {
        for (var index = startIndex; index >= 0; index--)
        {
            var line = lines[index].Trim();
            if (!line.StartsWith("public async Task ", StringComparison.Ordinal) &&
                !line.StartsWith("public void ", StringComparison.Ordinal))
            {
                continue;
            }

            var nameEnd = line.IndexOf('(');
            var nameStart = nameEnd > 0
                ? line.LastIndexOf(' ', nameEnd - 1) + 1
                : -1;
            if (nameStart > 0 && nameEnd > nameStart)
            {
                return line[nameStart..nameEnd];
            }
        }

        throw new InvalidOperationException($"Could not locate a test method before line {startIndex + 1}.");
    }

    private sealed record LongRunningE2EProofContract(string MethodName, string InvariantId);

    private sealed record ProcessRuntimeHostCodeFirstClosureReport(
        string ExplicitBaseline,
        string CommandPolicy,
        string Decision)
    {
        public IReadOnlyList<string> BlockingReasons => FindBlockingReasons();

        public bool IsPolicyConsistent => BlockingReasons.Count == 0;

        public static ProcessRuntimeHostCodeFirstClosureReport FromLines(IEnumerable<string> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);

            var values = lines
                .Select(line => line.Split(':', 2))
                .Where(fields => fields.Length == 2)
                .ToDictionary(
                    fields => fields[0].Trim(),
                    fields => fields[1].Trim(),
                    StringComparer.OrdinalIgnoreCase);

            return new ProcessRuntimeHostCodeFirstClosureReport(
                ReadRequired(values, "ExplicitBaseline"),
                ReadRequired(values, "CommandPolicy"),
                ReadRequired(values, "Decision"));
        }

        private IReadOnlyList<string> FindBlockingReasons()
        {
            List<string> reasons = [];
            var usesConservativeHeadFallback =
                ExplicitBaseline.StartsWith("HEAD ", StringComparison.OrdinalIgnoreCase) ||
                CommandPolicy.Contains("conservative worktree fallback", StringComparison.OrdinalIgnoreCase);
            var isBlockedDecision = Decision.Contains("blocked", StringComparison.OrdinalIgnoreCase);

            if (usesConservativeHeadFallback && !isBlockedDecision)
            {
                reasons.Add("Conservative HEAD fallback can only support a blocked release decision.");
            }

            return reasons;
        }

        private static string ReadRequired(IReadOnlyDictionary<string, string> values, string key)
        {
            return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new ArgumentException($"The code-first closure report is missing '{key}'.");
        }
    }

    private sealed record ProcessRuntimeHostCodeFirstDiffSummary(
        int SourceChangedLines,
        int TestChangedLines,
        int DocumentationChangedLines,
        int BundleChangedLines,
        int OtherChangedLines)
    {
        public int SourceAndTestChangedLines => SourceChangedLines + TestChangedLines;

        public bool SatisfiesSourceTestDominance(int requiredMultiplier)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requiredMultiplier);

            return BundleChangedLines == 0 ||
                SourceAndTestChangedLines >= requiredMultiplier * BundleChangedLines;
        }

        public static IReadOnlyList<string> BuildNumstatArguments(string startSha)
        {
            ValidateExplicitStartSha(startSha);

            return ["diff", "--numstat", $"{startSha}...HEAD"];
        }

        public static IReadOnlyList<string> BuildWorktreeNumstatArguments(string startSha)
        {
            ValidateExplicitStartSha(startSha);

            return ["diff", "--numstat", startSha];
        }

        public static ProcessRuntimeHostCodeFirstDiffSummary FromNumstatLines(IEnumerable<string> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);

            var sourceChangedLines = 0;
            var testChangedLines = 0;
            var documentationChangedLines = 0;
            var bundleChangedLines = 0;
            var otherChangedLines = 0;

            foreach (var line in lines)
            {
                if (!TryParseNumstatLine(line, out var changedLines, out var path))
                {
                    continue;
                }

                var normalizedPath = path.Replace('\\', '/');
                if (normalizedPath.StartsWith("src/", StringComparison.OrdinalIgnoreCase))
                {
                    sourceChangedLines += changedLines;
                }
                else if (normalizedPath.StartsWith("tests/", StringComparison.OrdinalIgnoreCase))
                {
                    testChangedLines += changedLines;
                }
                else if (normalizedPath.StartsWith("docs/", StringComparison.OrdinalIgnoreCase))
                {
                    documentationChangedLines += changedLines;
                }
                else if (normalizedPath.StartsWith($"{CodexSegment}/{BundlesSegment}/", StringComparison.OrdinalIgnoreCase))
                {
                    bundleChangedLines += changedLines;
                }
                else
                {
                    otherChangedLines += changedLines;
                }
            }

            return new ProcessRuntimeHostCodeFirstDiffSummary(
                sourceChangedLines,
                testChangedLines,
                documentationChangedLines,
                bundleChangedLines,
                otherChangedLines);
        }

        private static bool TryParseNumstatLine(string line, out int changedLines, out string path)
        {
            changedLines = 0;
            path = string.Empty;

            var fields = line.Split('\t', 3);
            if (fields.Length != 3 ||
                !int.TryParse(fields[0], out var addedLines) ||
                !int.TryParse(fields[1], out var deletedLines))
            {
                return false;
            }

            changedLines = addedLines + deletedLines;
            path = fields[2].Trim();
            return path.Length > 0;
        }

        private static void ValidateExplicitStartSha(string startSha)
        {
            if (string.IsNullOrWhiteSpace(startSha))
            {
                throw new ArgumentException("The bundle start SHA must be explicit.", nameof(startSha));
            }

            if (startSha.Length is < 7 or > 40 ||
                startSha.Any(character => !Uri.IsHexDigit(character)))
            {
                throw new ArgumentException("The bundle start SHA must be a 7-40 character hexadecimal Git object id.", nameof(startSha));
            }
        }
    }
}
