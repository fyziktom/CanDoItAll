using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceProductFilesystemCompletionGateContributionTests
{
    private static readonly WorkspaceProductFilesystemCompletionGateContribution Gate = new();

    [Fact]
    public void Validate_CompletedMutationWithEvidenceAndEmptyNativeRoot_ReturnsMissingOutput()
    {
        var productRoot = CreateTemporaryProductRoot();
        try
        {
            var issue = Gate.Validate(CreateContext(productRoot, evidenceRefs: ["evidence/implementation.md"]));

            Assert.NotNull(issue);
            Assert.Equal("process.adapter.product_output_missing", issue.Code);
        }
        finally
        {
            DeleteDirectory(productRoot);
        }
    }

    [Fact]
    public void Validate_CompletedStepWithMissingRequiredPath_ReturnsRequiredPathIssue()
    {
        var productRoot = CreateTemporaryProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(productRoot, "existing.txt"), "product");
            var context = CreateContext(
                productRoot,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths] =
                        JsonSerializer.Serialize(new[] { "required-output.txt" })
                });

            var issue = Gate.Validate(context);

            Assert.NotNull(issue);
            Assert.Equal("process.adapter.product_required_output_missing", issue.Code);
        }
        finally
        {
            DeleteDirectory(productRoot);
        }
    }

    [Fact]
    public void Validate_NonMutatingStepWithFailedRequiredContentCheck_ReturnsContentIssue()
    {
        var productRoot = CreateTemporaryProductRoot();
        try
        {
            var readbackFile = Path.Combine(productRoot, "readback.txt");
            File.WriteAllText(readbackFile, "actual product state");
            var context = CreateContext(
                productRoot,
                mutatesProduct: false,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks] =
                        JsonSerializer.Serialize(new object[]
                        {
                            new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                ["pathCandidates"] = new[] { "readback.txt" },
                                ["requiredTextAnyGroups"] = new[] { new[] { "expected product state" } }
                            }
                        })
                });

            var issue = Gate.Validate(context);

            Assert.NotNull(issue);
            Assert.Equal("process.adapter.product_required_file_content_missing", issue.Code);
        }
        finally
        {
            DeleteDirectory(productRoot);
        }
    }

    [Fact]
    public void Validate_RequiredContentCheckAcceptsLaterCandidateWhenPreferredFileIsStale()
    {
        var productRoot = CreateTemporaryProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(productRoot, "preferred.sln"), "stale membership");
            File.WriteAllText(
                Path.Combine(productRoot, "alternative.slnx"),
                "src/Calculator/Calculator.csproj");
            var context = CreateContext(
                productRoot,
                mutatesProduct: false,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks] =
                        JsonSerializer.Serialize(new object[]
                        {
                            new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                ["pathCandidates"] = new[] { "preferred.sln", "alternative.slnx" },
                                ["requiredTextAnyGroups"] =
                                    new[] { new[] { "src/Calculator/Calculator.csproj" } }
                            }
                        })
                });

            var issue = Gate.Validate(context);

            Assert.Null(issue);
        }
        finally
        {
            DeleteDirectory(productRoot);
        }
    }

    [Fact]
    public void Validate_RequiredContentCheckRejectsInvalidCandidateEvenWhenAlternativeIsValid()
    {
        var productRoot = CreateTemporaryProductRoot();
        var outsideRoot = CreateTemporaryProductRoot();
        try
        {
            File.WriteAllText(
                Path.Combine(productRoot, "alternative.slnx"),
                "src/Calculator/Calculator.csproj");
            var context = CreateContext(
                productRoot,
                mutatesProduct: false,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks] =
                        JsonSerializer.Serialize(new object[]
                        {
                            new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                ["pathCandidates"] = new[]
                                {
                                    Path.Combine(outsideRoot, "escaped.sln"),
                                    "alternative.slnx"
                                },
                                ["requiredTextAnyGroups"] =
                                    new[] { new[] { "src/Calculator/Calculator.csproj" } }
                            }
                        })
                });

            var issue = Gate.Validate(context);

            Assert.NotNull(issue);
            Assert.Equal(
                "process.adapter.product_required_file_content_check_unavailable",
                issue.Code);
            Assert.Equal(ProcessDiagnosticRetrySafety.UnsafeToRetry, issue.RetrySafety);
        }
        finally
        {
            DeleteDirectory(productRoot);
            DeleteDirectory(outsideRoot);
        }
    }

    [Fact]
    public void Validate_RequiredContentCheckWithUnavailableReadback_ReturnsEvidenceAccessIssue()
    {
        var productRoot = CreateTemporaryProductRoot();
        try
        {
            var readbackFile = Path.Combine(productRoot, "readback.txt");
            File.WriteAllText(readbackFile, "actual product state");
            var context = CreateContext(
                productRoot,
                mutatesProduct: false,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks] =
                        JsonSerializer.Serialize(new object[]
                        {
                            new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                ["pathCandidates"] = new[] { "readback.txt" },
                                ["requiredTextAnyGroups"] = new[] { new[] { "expected product state" } }
                            }
                        })
                });

            using var exclusiveHandle = new FileStream(
                readbackFile,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);
            var issue = Gate.Validate(context);

            Assert.NotNull(issue);
            Assert.Equal("process.adapter.product_required_file_content_check_unavailable", issue.Code);
            Assert.Contains("not verified product defect evidence", issue.Summary, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(productRoot);
        }
    }

    [Fact]
    public void Validate_AliasOnlyProductRoot_DoesNotPerformNativeFilesystemInspection()
    {
        var context = CreateContext(
            productRoot: null,
            extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ProcessRuntimeLaunchVariables.ProductRootAlias] = "external-target/C/work/product"
            });

        var issue = Gate.Validate(context);

        Assert.Null(issue);
    }

    [Fact]
    public void Validate_MissingMutationEvidenceIsOwnedByGenericEvidenceGate()
    {
        var productRoot = CreateTemporaryProductRoot();
        try
        {
            var context = CreateContext(productRoot, evidenceRefs: []);

            Assert.Null(Gate.Validate(context));
            var issue = ProcessProductMutationEvidenceGate.Validate(context.Assignment, context.Output);
            Assert.NotNull(issue);
            Assert.Equal("process.adapter.product_output_evidence_missing", issue.Code);
        }
        finally
        {
            DeleteDirectory(productRoot);
        }
    }

    private static ProcessCompletionGateContext CreateContext(
        string? productRoot,
        bool mutatesProduct = true,
        IReadOnlyDictionary<string, string>? extraLaunchVariables = null,
        IReadOnlyList<string>? evidenceRefs = null)
    {
        var launchVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(productRoot))
        {
            launchVariables["OutputRoot"] = productRoot;
            launchVariables["ProductRoot"] = productRoot;
            launchVariables["ExternalTargetRoot"] = productRoot;
        }

        if (extraLaunchVariables is not null)
        {
            foreach (var (key, value) in extraLaunchVariables)
            {
                launchVariables[key] = value;
            }
        }

        var assignment = new ProcessRuntimeStepAssignment(
            ProcessRunId.New(),
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            "workspace-completion",
            "test-role",
            "test-role",
            "Test role",
            ProcessLaunchExecutorKinds.Agent,
            Guid.NewGuid().ToString("D"),
            "Test agent",
            "Validate the configured product target.",
            "sha256:test",
            "Test assignment.",
            [ArtifactSlotId.New()],
            [],
            mutatesProduct ? [ProcessOperationContractNames.MutateProductTarget] : [],
            mutatesProduct
                ? ProcessOperationContractNames.ExternalProductTargetMutable
                : ProcessOperationContractNames.ExternalProductTargetReadOnly,
            launchVariables,
            BranchGate: null,
            DateTimeOffset.UtcNow);
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "Validation completed.",
            EvidenceRefs = evidenceRefs ?? ["evidence/completion.md"],
            NextActions = []
        };

        return new ProcessCompletionGateContext(assignment, output, [], CurrentExecutionRunId: null);
    }

    private static string CreateTemporaryProductRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCompletionGate.{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
