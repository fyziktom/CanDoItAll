using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using Microsoft.AspNetCore.DataProtection;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceProductFilesystemCompletionGateContributionTests
{
    [Fact]
    public void Validate_CompletedMutationWithEvidenceAndEmptyNativeRoot_ReturnsMissingOutput()
    {
        var productRoot = CreateTemporaryProductRoot();
        try
        {
            var (gate, context) = CreateBoundGateContext(
                productRoot,
                evidenceRefs: ["evidence/implementation.md"]);
            var issue = gate.Validate(context);

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
            var (gate, context) = CreateBoundGateContext(
                productRoot,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths] =
                        JsonSerializer.Serialize(new[] { "required-output.txt" })
                });

            var issue = gate.Validate(context);

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
            var (gate, context) = CreateBoundGateContext(
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

            var issue = gate.Validate(context);

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
            var (gate, context) = CreateBoundGateContext(
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

            var issue = gate.Validate(context);

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
            var (gate, context) = CreateBoundGateContext(
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

            var issue = gate.Validate(context);

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
            var (gate, context) = CreateBoundGateContext(
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
            var issue = gate.Validate(context);

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
    public void Validate_AliasOnlyProductRoot_ReconstructsPersistedLaunchAuthority()
    {
        var workspaceRoot = CreateTemporaryProductRoot();
        var productRoot = CreateTemporaryProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(productRoot, "product.txt"), "product");
            var dataProtectionProvider = new EphemeralDataProtectionProvider();
            var launchRegistry = new ExternalTargetPathRegistry(dataProtectionProvider);
            Assert.True(launchRegistry.TryCreateAlias(productRoot, out var alias));
            var context = CreateContext(
                productRoot: null,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductRootAlias] = alias,
                    [ProcessRuntimeLaunchVariables.ExternalTargetRootBindings] =
                        JsonSerializer.Serialize(launchRegistry.ExportBindings([alias]))
                });

            var issue = CreateGate(
                    workspaceRoot,
                    CreateWorkspaceFileInspectionScopeFactory(workspaceRoot, dataProtectionProvider))
                .Validate(context);

            Assert.Null(issue);
        }
        finally
        {
            DeleteDirectory(productRoot);
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void Validate_AliasWithUppercaseBindingIdentity_ReconstructsPersistedLaunchAuthority()
    {
        var workspaceRoot = CreateTemporaryProductRoot();
        var productRoot = CreateTemporaryProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(productRoot, "product.txt"), "product");
            var dataProtectionProvider = new EphemeralDataProtectionProvider();
            var launchRegistry = new ExternalTargetPathRegistry(dataProtectionProvider);
            Assert.True(launchRegistry.TryCreateAlias(productRoot, out var alias));
            var bindings = launchRegistry.ExportBindings([alias])
                .Select(binding => binding with
                {
                    RootId = binding.RootId.ToUpperInvariant()
                })
                .ToArray();
            var context = CreateContext(
                productRoot: null,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductRootAlias] = alias,
                    [ProcessRuntimeLaunchVariables.ExternalTargetRootBindings] =
                        JsonSerializer.Serialize(bindings)
                });

            var issue = CreateGate(
                    workspaceRoot,
                    CreateWorkspaceFileInspectionScopeFactory(workspaceRoot, dataProtectionProvider))
                .Validate(context);

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

            var issue = CreateGate(workspaceRoot).Validate(context);

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
    public void Validate_LegacyAliasProductRoot_ReturnsInspectionUnavailable()
    {
        var workspaceRoot = CreateTemporaryProductRoot();
        try
        {
            var context = CreateContext(
                productRoot: null,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductRootAlias] =
                        "external-target/C/workspace/product"
                });

            var issue = CreateGate(workspaceRoot).Validate(context);

            Assert.NotNull(issue);
            Assert.Equal("process.adapter.product_output_inspection_unavailable", issue.Code);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void Validate_AliasWithMismatchedPersistedBinding_ReturnsInspectionUnavailable()
    {
        var workspaceRoot = CreateTemporaryProductRoot();
        try
        {
            var context = CreateContext(
                productRoot: null,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductRootAlias] =
                        "external-target/v1/0123456789abcdef01234567/product",
                    [ProcessRuntimeLaunchVariables.ExternalTargetRootBindings] = JsonSerializer.Serialize(
                        new[]
                        {
                            new ExternalTargetRootBinding(
                                "fedcba9876543210fedcba98",
                                "test-host",
                                "test-protected-root-token")
                        })
                });

            var issue = CreateGate(workspaceRoot).Validate(context);

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
    public void Validate_AliasWithMalformedPersistedBinding_ReturnsInspectionUnavailable()
    {
        var workspaceRoot = CreateTemporaryProductRoot();
        try
        {
            var context = CreateContext(
                productRoot: null,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductRootAlias] =
                        "external-target/v1/0123456789abcdef01234567/product",
                    [ProcessRuntimeLaunchVariables.ExternalTargetRootBindings] =
                        """[{"RootId":null,"HostPlatform":"test-host","ProtectedRootToken":"token"}]"""
                });

            var issue = CreateGate(workspaceRoot).Validate(context);

            Assert.NotNull(issue);
            Assert.Equal("process.adapter.product_output_inspection_unavailable", issue.Code);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void Validate_AliasWithTamperedPersistedBinding_ReturnsInspectionUnavailable()
    {
        var workspaceRoot = CreateTemporaryProductRoot();
        var productRoot = CreateTemporaryProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(productRoot, "product.txt"), "product");
            var dataProtectionProvider = new EphemeralDataProtectionProvider();
            var launchRegistry = new ExternalTargetPathRegistry(dataProtectionProvider);
            Assert.True(launchRegistry.TryCreateAlias(productRoot, out var alias));
            var tamperedBindings = launchRegistry.ExportBindings([alias])
                .Select(binding => binding with
                {
                    ProtectedRootToken = $"tampered-{binding.ProtectedRootToken}"
                })
                .ToArray();
            var context = CreateContext(
                productRoot: null,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductRootAlias] = alias,
                    [ProcessRuntimeLaunchVariables.ExternalTargetRootBindings] =
                        JsonSerializer.Serialize(tamperedBindings)
                });

            var issue = CreateGate(
                    workspaceRoot,
                    CreateWorkspaceFileInspectionScopeFactory(workspaceRoot, dataProtectionProvider))
                .Validate(context);

            Assert.NotNull(issue);
            Assert.Equal("process.adapter.product_output_inspection_unavailable", issue.Code);
        }
        finally
        {
            DeleteDirectory(productRoot);
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void Validate_AliasWithConflictingDuplicateBinding_ReturnsInspectionUnavailable()
    {
        var workspaceRoot = CreateTemporaryProductRoot();
        var productRoot = CreateTemporaryProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(productRoot, "product.txt"), "product");
            var dataProtectionProvider = new EphemeralDataProtectionProvider();
            var launchRegistry = new ExternalTargetPathRegistry(dataProtectionProvider);
            Assert.True(launchRegistry.TryCreateAlias(productRoot, out var alias));
            var binding = Assert.Single(launchRegistry.ExportBindings([alias]));
            var context = CreateContext(
                productRoot: null,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductRootAlias] = alias,
                    [ProcessRuntimeLaunchVariables.ExternalTargetRootBindings] =
                        JsonSerializer.Serialize(new[]
                        {
                            binding,
                            binding with
                            {
                                ProtectedRootToken = $"tampered-{binding.ProtectedRootToken}"
                            }
                        })
                });

            var issue = CreateGate(
                    workspaceRoot,
                    CreateWorkspaceFileInspectionScopeFactory(workspaceRoot, dataProtectionProvider))
                .Validate(context);

            Assert.NotNull(issue);
            Assert.Equal("process.adapter.product_output_inspection_unavailable", issue.Code);
        }
        finally
        {
            DeleteDirectory(productRoot);
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void Validate_PersistedAuthorityDoesNotLeakToNextAssignment()
    {
        var workspaceRoot = CreateTemporaryProductRoot();
        var productRoot = CreateTemporaryProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(productRoot, "product.txt"), "product");
            var dataProtectionProvider = new EphemeralDataProtectionProvider();
            var launchRegistry = new ExternalTargetPathRegistry(dataProtectionProvider);
            Assert.True(launchRegistry.TryCreateAlias(productRoot, out var alias));
            var authorizedContext = CreateContext(
                productRoot: null,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductRootAlias] = alias,
                    [ProcessRuntimeLaunchVariables.ExternalTargetRootBindings] =
                        JsonSerializer.Serialize(launchRegistry.ExportBindings([alias]))
                });
            var unauthorizedContext = CreateContext(
                productRoot: null,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductRootAlias] = alias
                });
            var gate = CreateGate(
                workspaceRoot,
                CreateWorkspaceFileInspectionScopeFactory(workspaceRoot, dataProtectionProvider));

            Assert.Null(gate.Validate(authorizedContext));
            var issue = gate.Validate(unauthorizedContext);

            Assert.NotNull(issue);
            Assert.Equal("process.adapter.product_output_inspection_unavailable", issue.Code);
        }
        finally
        {
            DeleteDirectory(productRoot);
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void Validate_NativeProductRootWithPersistedAliasAuthority_UsesAliasAuthority()
    {
        var workspaceRoot = CreateTemporaryProductRoot();
        var productRoot = CreateTemporaryProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(productRoot, "product.txt"), "product");
            var dataProtectionProvider = new EphemeralDataProtectionProvider();
            var launchRegistry = new ExternalTargetPathRegistry(dataProtectionProvider);
            Assert.True(launchRegistry.TryCreateAlias(productRoot, out var alias));
            var context = CreateContext(
                productRoot,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductRootAlias] = alias,
                    [ProcessRuntimeLaunchVariables.ExternalTargetRootBindings] =
                        JsonSerializer.Serialize(launchRegistry.ExportBindings([alias]))
                });

            var issue = CreateGate(
                    workspaceRoot,
                    CreateWorkspaceFileInspectionScopeFactory(workspaceRoot, dataProtectionProvider))
                .Validate(context);

            Assert.Null(issue);
        }
        finally
        {
            DeleteDirectory(productRoot);
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void Validate_NativeProductRootWithUnboundAlias_ReturnsInspectionUnavailable()
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

            var issue = CreateGate(workspaceRoot).Validate(context);

            Assert.NotNull(issue);
            Assert.Equal("process.adapter.product_output_inspection_unavailable", issue.Code);
        }
        finally
        {
            DeleteDirectory(productRoot);
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void Validate_NativeProductRootWithEmptyAlias_ReturnsInspectionUnavailable()
    {
        var productRoot = CreateTemporaryProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(productRoot, "product.txt"), "product");
            var context = CreateContext(
                productRoot,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductRootAlias] = string.Empty
                });

            var issue = CreateGate(productRoot).Validate(context);

            Assert.NotNull(issue);
            Assert.Equal("process.adapter.product_output_inspection_unavailable", issue.Code);
        }
        finally
        {
            DeleteDirectory(productRoot);
        }
    }

    [Fact]
    public void Validate_NativeProductRootWithNullAlias_ReturnsInspectionUnavailable()
    {
        var productRoot = CreateTemporaryProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(productRoot, "product.txt"), "product");
            var context = CreateContext(
                productRoot,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductRootAlias] = null!
                });

            var issue = CreateGate(productRoot).Validate(context);

            Assert.NotNull(issue);
            Assert.Equal("process.adapter.product_output_inspection_unavailable", issue.Code);
        }
        finally
        {
            DeleteDirectory(productRoot);
        }
    }

    [Fact]
    public void Validate_NativeProductRootWithBindingsButNoAlias_ReturnsInspectionUnavailable()
    {
        var productRoot = CreateTemporaryProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(productRoot, "product.txt"), "product");
            var context = CreateContext(
                productRoot,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ExternalTargetRootBindings] = "[]"
                });

            var issue = CreateGate(productRoot).Validate(context);

            Assert.NotNull(issue);
            Assert.Equal("process.adapter.product_output_inspection_unavailable", issue.Code);
        }
        finally
        {
            DeleteDirectory(productRoot);
        }
    }

    [Fact]
    public void Validate_NativeProductRootWithoutAlias_ReturnsInspectionUnavailable()
    {
        var workspaceRoot = CreateTemporaryProductRoot();
        var productRoot = CreateTemporaryProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(productRoot, "product.txt"), "product");

            var issue = CreateGate(workspaceRoot)
                .Validate(CreateContext(productRoot));

            Assert.NotNull(issue);
            Assert.Equal("process.adapter.product_output_inspection_unavailable", issue.Code);
        }
        finally
        {
            DeleteDirectory(productRoot);
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void Validate_NativeProductRootWithAliasRequiredPath_RejectsCrossAuthorityPath()
    {
        var productRoot = CreateTemporaryProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(productRoot, "product.txt"), "product");
            var (gate, context) = CreateBoundGateContext(
                productRoot,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths] =
                        JsonSerializer.Serialize(new[]
                        {
                            "external-target/v1/0123456789abcdef01234567/escaped.txt"
                        })
                });

            var issue = gate.Validate(context);

            Assert.NotNull(issue);
            Assert.Equal("process.adapter.product_required_output_path_invalid", issue.Code);
        }
        finally
        {
            DeleteDirectory(productRoot);
        }
    }

    [Fact]
    public void Validate_NativeRequiredPathAndContentUnderAlias_ReconstructPersistedLaunchAuthority()
    {
        var workspaceRoot = CreateTemporaryProductRoot();
        var productRoot = CreateTemporaryProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(productRoot, "required-output.txt"), "expected product state");
            var dataProtectionProvider = new EphemeralDataProtectionProvider();
            var launchRegistry = new ExternalTargetPathRegistry(dataProtectionProvider);
            Assert.True(launchRegistry.TryCreateAlias(productRoot, out var alias));
            var context = CreateContext(
                productRoot: null,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductRootAlias] = alias,
                    [ProcessRuntimeLaunchVariables.ExternalTargetRootBindings] =
                        JsonSerializer.Serialize(launchRegistry.ExportBindings([alias])),
                    [ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths] =
                        JsonSerializer.Serialize(new[]
                        {
                            Path.Combine(productRoot, "required-output.txt")
                        }),
                    [ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks] =
                        JsonSerializer.Serialize(new object[]
                        {
                            new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                ["pathCandidates"] = new[]
                                {
                                    Path.Combine(productRoot, "required-output.txt")
                                },
                                ["requiredTextAnyGroups"] = new[] { new[] { "expected product state" } }
                            }
                        })
                });

            var issue = CreateGate(
                    workspaceRoot,
                    CreateWorkspaceFileInspectionScopeFactory(workspaceRoot, dataProtectionProvider))
                .Validate(context);

            Assert.Null(issue);
        }
        finally
        {
            DeleteDirectory(productRoot);
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void Validate_NativeRequiredPathInsideWorkspaceButOutsideAliasAuthority_ReturnsUnavailable()
    {
        var workspaceRoot = CreateTemporaryProductRoot();
        var productRoot = CreateTemporaryProductRoot();
        try
        {
            var workspaceOnlyPath = Path.Combine(workspaceRoot, "workspace-only.txt");
            File.WriteAllText(workspaceOnlyPath, "not product output");
            File.WriteAllText(Path.Combine(productRoot, "product.txt"), "product");
            var dataProtectionProvider = new EphemeralDataProtectionProvider();
            var launchRegistry = new ExternalTargetPathRegistry(dataProtectionProvider);
            Assert.True(launchRegistry.TryCreateAlias(productRoot, out var alias));
            var context = CreateContext(
                productRoot: null,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductRootAlias] = alias,
                    [ProcessRuntimeLaunchVariables.ExternalTargetRootBindings] =
                        JsonSerializer.Serialize(launchRegistry.ExportBindings([alias])),
                    [ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths] =
                        JsonSerializer.Serialize(new[] { workspaceOnlyPath })
                });

            var issue = CreateGate(
                    workspaceRoot,
                    CreateWorkspaceFileInspectionScopeFactory(workspaceRoot, dataProtectionProvider))
                .Validate(context);

            Assert.NotNull(issue);
            Assert.Equal("process.adapter.product_required_output_unavailable", issue.Code);
        }
        finally
        {
            DeleteDirectory(productRoot);
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void Validate_NativeContentCandidateOutsideAliasAuthority_ReturnsUnavailable()
    {
        var workspaceRoot = CreateTemporaryProductRoot();
        var productRoot = CreateTemporaryProductRoot();
        var externalSiblingRoot = CreateTemporaryProductRoot();
        try
        {
            var externalSiblingPath = Path.Combine(externalSiblingRoot, "sibling.txt");
            File.WriteAllText(externalSiblingPath, "expected product state");
            File.WriteAllText(Path.Combine(productRoot, "product.txt"), "product");
            var dataProtectionProvider = new EphemeralDataProtectionProvider();
            var launchRegistry = new ExternalTargetPathRegistry(dataProtectionProvider);
            Assert.True(launchRegistry.TryCreateAlias(productRoot, out var alias));
            var context = CreateContext(
                productRoot: null,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductRootAlias] = alias,
                    [ProcessRuntimeLaunchVariables.ExternalTargetRootBindings] =
                        JsonSerializer.Serialize(launchRegistry.ExportBindings([alias])),
                    [ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks] =
                        JsonSerializer.Serialize(new object[]
                        {
                            new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                ["pathCandidates"] = new[] { externalSiblingPath },
                                ["requiredTextAnyGroups"] = new[] { new[] { "expected product state" } }
                            }
                        })
                });

            var issue = CreateGate(
                    workspaceRoot,
                    CreateWorkspaceFileInspectionScopeFactory(workspaceRoot, dataProtectionProvider))
                .Validate(context);

            Assert.NotNull(issue);
            Assert.Equal("process.adapter.product_required_file_content_check_unavailable", issue.Code);
        }
        finally
        {
            DeleteDirectory(externalSiblingRoot);
            DeleteDirectory(productRoot);
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void Validate_UnixBackslashFilenameUnderAliasAuthority_Succeeds()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var workspaceRoot = CreateTemporaryProductRoot();
        var productRoot = CreateTemporaryProductRoot();
        try
        {
            const string fileName = @"file\name.txt";
            File.WriteAllText(Path.Combine(productRoot, fileName), "product");
            var dataProtectionProvider = new EphemeralDataProtectionProvider();
            var launchRegistry = new ExternalTargetPathRegistry(dataProtectionProvider);
            Assert.True(launchRegistry.TryCreateAlias(productRoot, out var alias));
            var context = CreateContext(
                productRoot: null,
                extraLaunchVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductRootAlias] = alias,
                    [ProcessRuntimeLaunchVariables.ExternalTargetRootBindings] =
                        JsonSerializer.Serialize(launchRegistry.ExportBindings([alias])),
                    [ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths] =
                        JsonSerializer.Serialize(new[] { fileName })
                });

            var issue = CreateGate(
                    workspaceRoot,
                    CreateWorkspaceFileInspectionScopeFactory(workspaceRoot, dataProtectionProvider))
                .Validate(context);

            Assert.Null(issue);
        }
        finally
        {
            DeleteDirectory(productRoot);
            DeleteDirectory(workspaceRoot);
        }
    }

    private static (
        WorkspaceProductFilesystemCompletionGateContribution Gate,
        ProcessCompletionGateContext Context) CreateBoundGateContext(
        string productRoot,
        bool mutatesProduct = true,
        IReadOnlyDictionary<string, string>? extraLaunchVariables = null,
        IReadOnlyList<string>? evidenceRefs = null)
    {
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var launchRegistry = new ExternalTargetPathRegistry(dataProtectionProvider);
        Assert.True(launchRegistry.TryCreateAlias(productRoot, out var alias));
        var launchVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (extraLaunchVariables is not null)
        {
            foreach (var (key, value) in extraLaunchVariables)
            {
                launchVariables[key] = value;
            }
        }

        launchVariables[ProcessRuntimeLaunchVariables.ProductRootAlias] = alias;
        launchVariables[ProcessRuntimeLaunchVariables.ExternalTargetRootBindings] =
            JsonSerializer.Serialize(launchRegistry.ExportBindings([alias]));
        return (
            CreateGate(
                productRoot,
                CreateWorkspaceFileInspectionScopeFactory(productRoot, dataProtectionProvider)),
            CreateContext(
                productRoot,
                mutatesProduct,
                launchVariables,
                evidenceRefs));
    }

    private static WorkspaceProductFilesystemCompletionGateContribution CreateGate(
        string workspaceRoot,
        WorkspaceFileInspectionScopeFactory? workspaceFileInspectionScopeFactory = null)
    {
        return new WorkspaceProductFilesystemCompletionGateContribution(
            new ProcessProductCompletionPathGate(
                new ProcessProductFilesystemInspector(
                    workspaceFileInspectionScopeFactory ??
                    CreateWorkspaceFileInspectionScopeFactory(
                        workspaceRoot,
                        new EphemeralDataProtectionProvider()))));
    }

    private static WorkspaceFileInspectionScopeFactory CreateWorkspaceFileInspectionScopeFactory(
        string workspaceRoot,
        IDataProtectionProvider dataProtectionProvider)
        => new(
            workspaceRoot,
            WorkspaceScopeDescriptor.Sandbox,
            TestWorkspaceServices.PhysicalPathPolicyFactory,
            new ExternalTargetPathRegistryFactory(dataProtectionProvider));

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
            var (gate, context) = CreateBoundGateContext(productRoot, evidenceRefs: []);

            Assert.Null(gate.Validate(context));
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
