using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceProductFilesystemCompletionGateContributionTests
{
    [Fact]
    public void Validate_CompletedMutationWithEvidenceAndEmptyNativeRoot_ReturnsMissingOutput()
    {
        var productRoot = CreateTemporaryProductRoot();
        try
        {
            var issue = CreateGate(productRoot).Validate(
                CreateContext(productRoot, evidenceRefs: ["evidence/implementation.md"]));

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

            var issue = CreateGate(productRoot).Validate(context);

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

            var issue = CreateGate(productRoot).Validate(context);

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

            var issue = CreateGate(productRoot).Validate(context);

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

            var issue = CreateGate(productRoot).Validate(context);

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
            var issue = CreateGate(productRoot).Validate(context);

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
    public void Validate_AliasOnlyProductRoot_UsesBoundWorkspaceAuthority()
    {
        var workspaceRoot = CreateTemporaryProductRoot();
        var productRoot = CreateTemporaryProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(productRoot, "product.txt"), "product");
            var registry = new ExternalTargetPathRegistry();
            Assert.True(registry.TryCreateAlias(productRoot, out var alias));
            var context = CreateContext(
                productRoot: null,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductRootAlias] = alias
                });

            var issue = CreateGate(workspaceRoot, registry).Validate(context);

            Assert.Null(issue);
        }
        finally
        {
            DeleteDirectory(productRoot);
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void Validate_UnboundAliasProductRoot_ReturnsInspectionUnavailable()
    {
        var workspaceRoot = CreateTemporaryProductRoot();
        try
        {
            var context = CreateContext(
                productRoot: null,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductRootAlias] =
                        "external-target/v1/0123456789abcdef01234567/product"
                });

            var issue = CreateGate(workspaceRoot, new ExternalTargetPathRegistry()).Validate(context);

            Assert.NotNull(issue);
            Assert.Equal("process.adapter.product_output_inspection_unavailable", issue.Code);
            Assert.DoesNotContain("0123456789abcdef01234567", issue.Summary, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void Validate_NativeProductRootWithUnboundAlias_UsesValidatedNativeAuthority()
    {
        var workspaceRoot = CreateTemporaryProductRoot();
        var productRoot = CreateTemporaryProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(productRoot, "product.txt"), "product");
            var context = CreateContext(
                productRoot,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductRootAlias] =
                        "external-target/v1/0123456789abcdef01234567/product"
                });

            var issue = CreateGate(workspaceRoot, new ExternalTargetPathRegistry()).Validate(context);

            Assert.Null(issue);
        }
        finally
        {
            DeleteDirectory(productRoot);
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void Validate_RequiredPathUnderBoundAlias_UsesOwnerResolution()
    {
        var workspaceRoot = CreateTemporaryProductRoot();
        var productRoot = CreateTemporaryProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(productRoot, "required-output.txt"), "product");
            var registry = new ExternalTargetPathRegistry();
            Assert.True(registry.TryCreateAlias(productRoot, out var alias));
            var context = CreateContext(
                productRoot: null,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductRootAlias] = alias,
                    [ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths] =
                        JsonSerializer.Serialize(new[] { "required-output.txt" })
                });

            var issue = CreateGate(workspaceRoot, registry).Validate(context);

            Assert.Null(issue);
        }
        finally
        {
            DeleteDirectory(productRoot);
            DeleteDirectory(workspaceRoot);
        }
    }

    private static WorkspaceProductFilesystemCompletionGateContribution CreateGate(
        string workspaceRoot,
        IExternalTargetPathRegistry? externalTargetRegistry = null)
    {
        var workspaceFiles = TestWorkspaceServices.CreateFileService(
            workspaceRoot,
            externalTargetRegistry: externalTargetRegistry);
        return new WorkspaceProductFilesystemCompletionGateContribution(
            new ProcessProductCompletionPathGate(
                new ProcessProductFilesystemInspector(workspaceFiles)));
    }

    private static void AssertGenericEvidenceGateOwnsMissingEvidence(ProcessCompletionGateContext context)
    {
        var issue = ProcessProductMutationEvidenceGate.Validate(context.Assignment, context.Output);
        Assert.NotNull(issue);
        Assert.Equal("process.adapter.product_output_evidence_missing", issue.Code);
    }

    [Fact]
    public void Validate_MissingMutationEvidenceIsOwnedByGenericEvidenceGate()
    {
        var productRoot = CreateTemporaryProductRoot();
        try
        {
            var context = CreateContext(productRoot, evidenceRefs: []);

            Assert.Null(CreateGate(productRoot).Validate(context));
            AssertGenericEvidenceGateOwnsMissingEvidence(context);
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
