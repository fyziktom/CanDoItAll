using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class DotNetSolutionSetupRuntimeExecutorTests
{
    [Fact]
    public void ExecutorKey_is_the_stable_dotnet_solution_setup_driver_key()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var executor = CreateExecutor(workspace, new FakeWorkspaceProcessHost());

        Assert.Equal("dotnet.solution-setup", executor.ExecutorKey);
    }

    [Fact]
    public async Task TryExecuteAsync_scaffolds_missing_solution_and_project_from_descriptor_when_step_key_is_template_specific()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var processHost = new FakeWorkspaceProcessHost(request =>
        {
            if (request.ToolName == "workspace_dotnet_new")
            {
                CreateDotNetNewOutput(request);
            }
            else if (request.ToolName == "workspace_pwsh_run_script")
            {
                WriteSolutionMembership(productRoot);
            }
        });
        var executor = CreateExecutor(workspace, processHost);

        var result = await executor.TryExecuteAsync(CreateAssignment(productRoot, "template-owned-solution-setup"));

        Assert.NotNull(result);
        Assert.True(result!.Succeeded, result.Summary);
        Assert.Equal(ProcessStepOutcomeStatus.Completed, result.Output!.Status);
        Assert.Null(result.EffectiveCompletionScope);
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_dotnet_new" && receipt.RequestSummary.StartsWith("new sln ", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_dotnet_new" && receipt.RequestSummary.StartsWith("new blazorwasm ", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.ToolReceipts, receipt =>
            receipt.ToolName == "workspace_pwsh_run_script" &&
            receipt.ExitSummary.Contains("Succeeded", StringComparison.OrdinalIgnoreCase) &&
            receipt.DeclaredSideEffectMode == ToolExecutionSideEffectMode.ProductMutation);
        Assert.All(
            result.ToolReceipts.Where(receipt => !string.IsNullOrWhiteSpace(receipt.WorkingDirectory)),
            receipt => Assert.DoesNotContain(productRoot, receipt.WorkingDirectory, StringComparison.Ordinal));
        Assert.Contains(
            result.ToolReceipts,
            receipt => receipt.WorkingDirectory.StartsWith("external-target/v1/", StringComparison.Ordinal));
        Assert.Equal(3, processHost.Requests.Count);
        Assert.Contains("src/Calculator/Calculator.csproj", await File.ReadAllTextAsync(Path.Combine(productRoot, "Calculator.slnx")), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(".slnx")]
    [InlineData(".sln")]
    public async Task TryExecuteAsync_accepts_a_solution_candidate_created_by_the_sdk(
        string generatedSolutionExtension)
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var primarySolutionFile = Path.Combine(productRoot, "Calculator.sln");
        var alternativeSolutionFile = Path.Combine(productRoot, "Calculator.slnx");
        var generatedSolutionFile = Path.Combine(
            productRoot,
            $"Calculator{generatedSolutionExtension}");
        var processHost = new FakeWorkspaceProcessHost(request =>
        {
            if (request.ToolName == "workspace_dotnet_new")
            {
                CreateDotNetNewOutput(request, generatedSolutionExtension);
            }
            else if (request.ToolName == "workspace_pwsh_run_script")
            {
                WriteSolutionMembership(productRoot, generatedSolutionExtension);
            }
        });
        var executor = CreateExecutor(workspace, processHost);
        var launchVariables = new Dictionary<string, string>(CreateLaunchVariables(productRoot), StringComparer.OrdinalIgnoreCase)
        {
            ["DotNetSolutionFile"] = primarySolutionFile,
            ["DotNetSolutionFileCandidates"] = $"{primarySolutionFile}; {alternativeSolutionFile}",
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths] = JsonSerializer.Serialize(
            new[]
            {
                Path.Combine(productRoot, "src", "Calculator", "Calculator.csproj")
            }),
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks] = JsonSerializer.Serialize(
            new object[]
            {
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["pathCandidates"] = new[] { primarySolutionFile, alternativeSolutionFile },
                    ["requiredTextAnyGroups"] = new[] { new[] { "src/Calculator/Calculator.csproj" } }
                },
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["pathCandidates"] = new[] { Path.Combine(productRoot, "src", "Calculator", "Calculator.csproj") },
                    ["requiredTextAnyGroups"] = new[] { new[] { "<TargetFramework>net8.0</TargetFramework>" } }
                }
            })
        };
        var assignment = CreateAssignment(productRoot, launchVariables: launchVariables);

        var result = await executor.TryExecuteAsync(assignment);

        Assert.NotNull(result);
        Assert.True(result!.Succeeded, result.Summary);
        Assert.True(File.Exists(generatedSolutionFile));
        Assert.Null(ProcessProductCompletionPathGate.ValidateRequiredProductFilesystemState(
            assignment,
            result.Output!));
        Assert.Contains(
            result.ToolReceipts,
            receipt => receipt.ToolName == "workspace_dotnet_new" &&
                       receipt.RequestSummary.StartsWith("new sln ", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryExecuteAsync_skips_existing_derived_solution_format_alternative()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var preferredSolutionFile = Path.Combine(productRoot, "Calculator.sln");
        var existingSolutionFile = Path.Combine(productRoot, "Calculator.slnx");
        var appProjectFile = Path.Combine(productRoot, "src", "Calculator", "Calculator.csproj");
        WriteExistingAppProject(productRoot);
        await File.WriteAllTextAsync(existingSolutionFile, string.Empty);
        var processHost = new FakeWorkspaceProcessHost(request =>
        {
            if (request.ToolName == "workspace_pwsh_run_script")
            {
                WriteSolutionMembership(productRoot);
            }
        });
        var executor = CreateExecutor(workspace, processHost);
        var launchVariables = new Dictionary<string, string>(
            CreateLaunchVariables(productRoot),
            StringComparer.OrdinalIgnoreCase)
        {
            ["DotNetSolutionFile"] = preferredSolutionFile,
            ["DotNetSolutionFileCandidates"] = preferredSolutionFile,
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks] =
                JsonSerializer.Serialize(
                    new object[]
                    {
                        new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            ["pathCandidates"] = new[] { preferredSolutionFile, existingSolutionFile },
                            ["requiredTextAnyGroups"] = new[] { new[] { "src/Calculator/Calculator.csproj" } }
                        },
                        new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            ["pathCandidates"] = new[] { appProjectFile },
                            ["requiredTextAnyGroups"] = new[] { new[] { "<Project Sdk=" } }
                        }
                    })
        };

        var result = await executor.TryExecuteAsync(
            CreateAssignment(productRoot, launchVariables: launchVariables));

        Assert.NotNull(result);
        Assert.True(result!.Succeeded, result.Summary);
        Assert.Single(processHost.Requests);
        Assert.Equal("workspace_pwsh_run_script", processHost.Requests[0].ToolName);
        Assert.Contains(
            result.ToolReceipts,
            receipt =>
                receipt.ToolName == "workspace_dotnet_new" &&
                receipt.RiskClass == "RuntimeOwned:IdempotentSkip" &&
                receipt.ExitSummary.Contains("Calculator.slnx", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryExecuteAsync_derives_solution_format_alternative_when_candidate_variable_is_absent()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var preferredSolutionFile = Path.Combine(productRoot, "Calculator.sln");
        var existingSolutionFile = Path.Combine(productRoot, "Calculator.slnx");
        var appProjectFile = Path.Combine(productRoot, "src", "Calculator", "Calculator.csproj");
        WriteExistingAppProject(productRoot);
        await File.WriteAllTextAsync(existingSolutionFile, string.Empty);
        var processHost = new FakeWorkspaceProcessHost(request =>
        {
            if (request.ToolName == "workspace_pwsh_run_script")
            {
                WriteSolutionMembership(productRoot);
            }
        });
        var executor = CreateExecutor(workspace, processHost);
        var launchVariables = new Dictionary<string, string>(
            CreateLaunchVariables(productRoot),
            StringComparer.OrdinalIgnoreCase)
        {
            ["DotNetSolutionFile"] = preferredSolutionFile,
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths] =
                JsonSerializer.Serialize(new[] { appProjectFile }),
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks] =
                JsonSerializer.Serialize(
                    new object[]
                    {
                        new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            ["pathCandidates"] = new[] { preferredSolutionFile, existingSolutionFile },
                            ["requiredTextAnyGroups"] = new[] { new[] { "src/Calculator/Calculator.csproj" } }
                        },
                        new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            ["pathCandidates"] = new[] { appProjectFile },
                            ["requiredTextAnyGroups"] = new[] { new[] { "<Project Sdk=" } }
                        }
                    })
        };
        launchVariables.Remove("DotNetSolutionFileCandidates");

        var result = await executor.TryExecuteAsync(
            CreateAssignment(productRoot, launchVariables: launchVariables));

        Assert.NotNull(result);
        Assert.True(result!.Succeeded, result.Summary);
        Assert.Single(processHost.Requests);
        Assert.Equal("workspace_pwsh_run_script", processHost.Requests[0].ToolName);
        Assert.Contains(
            result.ToolReceipts,
            receipt =>
                receipt.ToolName == "workspace_dotnet_new" &&
                receipt.RiskClass == "RuntimeOwned:IdempotentSkip" &&
                receipt.ExitSummary.Contains("Calculator.slnx", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryExecuteAsync_does_not_treat_a_directory_as_an_existing_solution_file()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var solutionFile = Path.Combine(productRoot, "Calculator.slnx");
        Directory.CreateDirectory(solutionFile);
        WriteExistingAppProject(productRoot);
        var processHost = new FakeWorkspaceProcessHost();
        var executor = CreateExecutor(workspace, processHost);

        var result = await executor.TryExecuteAsync(CreateAssignment(productRoot));

        Assert.NotNull(result);
        Assert.False(result!.Succeeded);
        var request = Assert.Single(processHost.Requests);
        Assert.Equal("workspace_dotnet_new", request.ToolName);
        Assert.Equal("sln", request.Arguments[1]);
        Assert.DoesNotContain(
            result.ToolReceipts,
            receipt =>
                receipt.ToolName == "workspace_dotnet_new" &&
                receipt.RiskClass == "RuntimeOwned:IdempotentSkip");
    }

    [Fact]
    public async Task TryExecuteAsync_reconciles_failed_create_when_contracted_postcondition_exists()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var processHost = new FakeWorkspaceProcessHost(request =>
        {
            if (request.ToolName == "workspace_dotnet_new")
            {
                CreateDotNetNewOutput(request);
            }
            else if (request.ToolName == "workspace_pwsh_run_script")
            {
                WriteSolutionMembership(productRoot);
            }
        })
        {
            ExitCodeResolver = request => request.ToolName == "workspace_dotnet_new" ? 73 : 0,
            Stderr = "The target was created concurrently."
        };
        var executor = CreateExecutor(workspace, processHost);

        var result = await executor.TryExecuteAsync(CreateAssignment(productRoot));

        Assert.NotNull(result);
        Assert.True(result!.Succeeded, result.Summary);
        Assert.Equal(
            2,
            result.ToolReceipts.Count(
                receipt =>
                    receipt.ToolName == "workspace_dotnet_new" &&
                    receipt.ExitSummary.StartsWith("Failed", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(
            2,
            result.ToolReceipts.Count(
                receipt =>
                    receipt.ToolName == "workspace_dotnet_new" &&
                    receipt.RequestSummary.Contains("postcondition-reconciled", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task TryExecuteAsync_passes_explicit_template_options_to_dotnet_new()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var processHost = new FakeWorkspaceProcessHost(request =>
        {
            if (request.ToolName == "workspace_dotnet_new")
            {
                CreateDotNetNewOutput(request);
            }
            else if (request.ToolName == "workspace_pwsh_run_script")
            {
                WriteSolutionMembership(productRoot);
            }
        });
        var executor = CreateExecutor(workspace, processHost);
        var launchVariables = new Dictionary<string, string>(CreateLaunchVariables(productRoot), StringComparer.OrdinalIgnoreCase)
        {
            ["DotNetAppTemplateOptions"] = "--pwa"
        };

        var result = await executor.TryExecuteAsync(CreateAssignment(productRoot, launchVariables: launchVariables));

        Assert.NotNull(result);
        Assert.True(result!.Succeeded, result.Summary);
        var appRequest = Assert.Single(
            processHost.Requests,
            request => request.ToolName == "workspace_dotnet_new" &&
                       request.Arguments.Contains("blazorwasm", StringComparer.OrdinalIgnoreCase));
        Assert.Contains(appRequest.Arguments, argument => string.Equals(argument, "--pwa", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(
            ["new", "blazorwasm", "--pwa", "--framework", "net8.0", "-n", "Calculator"],
            appRequest.Arguments);
    }

    [Fact]
    public async Task TryExecuteAsync_rejects_create_plan_without_a_target_framework()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var processHost = new FakeWorkspaceProcessHost();
        var executor = CreateExecutor(workspace, processHost);
        var launchVariables = new Dictionary<string, string>(CreateLaunchVariables(productRoot), StringComparer.OrdinalIgnoreCase);
        launchVariables.Remove("DotNetTargetFramework");

        var result = await executor.TryExecuteAsync(CreateAssignment(productRoot, launchVariables: launchVariables));

        Assert.NotNull(result);
        Assert.False(result!.Succeeded);
        Assert.Contains("target_framework_missing", result.Summary, StringComparison.Ordinal);
        Assert.Empty(processHost.Requests);
    }

    [Fact]
    public async Task TryExecuteAsync_verifies_explicit_existing_context_without_mutation_tools()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var portalProject = Path.Combine(productRoot, "modules", "Portal", "Portal.csproj");
        var contractsProject = Path.Combine(productRoot, "shared", "Contracts", "Contracts.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(portalProject)!);
        Directory.CreateDirectory(Path.GetDirectoryName(contractsProject)!);
        await File.WriteAllTextAsync(portalProject, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        await File.WriteAllTextAsync(contractsProject, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        Directory.CreateDirectory(Path.Combine(productRoot, "build"));
        await File.WriteAllTextAsync(
            Path.Combine(productRoot, "build", "EnterpriseSuite.sln"),
            "modules/Portal/Portal.csproj\nshared/Contracts/Contracts.csproj");
        var processHost = new FakeWorkspaceProcessHost();
        var executor = CreateExecutor(workspace, processHost);

        var result = await executor.TryExecuteAsync(CreateAssignment(
            productRoot,
            "template-owned-existing-solution-verification",
            CreateVerifyExistingLaunchVariables(productRoot, [portalProject, contractsProject])));

        Assert.NotNull(result);
        Assert.True(result!.Succeeded, result.Summary);
        Assert.Equal(ProcessStepOutcomeStatus.Completed, result.Output!.Status);
        Assert.Equal(
            ProcessRuntimeOwnedCompletionScope.ReadOnlyProductVerification,
            result.EffectiveCompletionScope);
        Assert.Empty(processHost.Requests);
        Assert.DoesNotContain(result.ToolReceipts, receipt => receipt.ToolName is "workspace_dotnet_new" or "workspace_write_file" or "workspace_pwsh_run_script");
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_stat_path");
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_read_file");
    }

    [Fact]
    public async Task Linux_verify_existing_requires_case_exact_solution_membership()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var workspace = new RuntimeExecutorWorkspace();
        string productRoot = workspace.CreateProductRoot();
        string projectFile = Path.Combine(productRoot, "modules", "Portal", "Portal.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);
        await File.WriteAllTextAsync(projectFile, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        Directory.CreateDirectory(Path.Combine(productRoot, "build"));
        await File.WriteAllTextAsync(
            Path.Combine(productRoot, "build", "EnterpriseSuite.sln"),
            "modules/portal/Portal.csproj");
        var processHost = new FakeWorkspaceProcessHost();
        var executor = CreateExecutor(workspace, processHost);

        ProcessRuntimeOwnedStepExecutionResult? result = await executor.TryExecuteAsync(CreateAssignment(
            productRoot,
            "template-owned-existing-solution-verification",
            CreateVerifyExistingLaunchVariables(productRoot, [projectFile])));

        Assert.NotNull(result);
        Assert.False(result!.Succeeded);
        Assert.Contains("does not include required project", result.Summary, StringComparison.Ordinal);
        Assert.Empty(processHost.Requests);
    }

    [Fact]
    public async Task TryExecuteAsync_rejects_missing_existing_context_file_without_mutation_tools()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        Directory.CreateDirectory(Path.Combine(productRoot, "build"));
        await File.WriteAllTextAsync(Path.Combine(productRoot, "build", "EnterpriseSuite.sln"), string.Empty);
        var processHost = new FakeWorkspaceProcessHost();
        var executor = CreateExecutor(workspace, processHost);

        var result = await executor.TryExecuteAsync(CreateAssignment(
            productRoot,
            "template-owned-existing-solution-verification",
            CreateVerifyExistingLaunchVariables(productRoot, [Path.Combine(productRoot, "modules", "Portal", "Portal.csproj")] )));

        Assert.NotNull(result);
        Assert.False(result!.Succeeded);
        Assert.Contains("missing required file", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(processHost.Requests);
        Assert.DoesNotContain(result.ToolReceipts, receipt => receipt.ToolName is "workspace_dotnet_new" or "workspace_write_file" or "workspace_pwsh_run_script");
    }

    [Fact]
    public async Task TryExecuteAsync_reports_all_missing_existing_solution_candidates_as_missing()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var projectFile = Path.Combine(productRoot, "modules", "Portal", "Portal.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);
        await File.WriteAllTextAsync(projectFile, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var processHost = new FakeWorkspaceProcessHost();
        var executor = CreateExecutor(workspace, processHost);

        var result = await executor.TryExecuteAsync(CreateAssignment(
            productRoot,
            "template-owned-existing-solution-verification",
            CreateVerifyExistingLaunchVariables(productRoot, [projectFile])));

        Assert.NotNull(result);
        Assert.False(result!.Succeeded);
        Assert.Contains("missing every solution candidate", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EnterpriseSuite.sln", result.Summary, StringComparison.Ordinal);
        Assert.Contains("EnterpriseSuite.slnx", result.Summary, StringComparison.Ordinal);
        Assert.Empty(processHost.Requests);
    }

    [Fact]
    public void Tool_plan_guard_accepts_declared_verify_existing_context_without_an_initialization_script()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var assignment = CreateAssignment(
            productRoot,
            "template-owned-existing-solution-verification",
            CreateVerifyExistingLaunchVariables(productRoot, [Path.Combine(productRoot, "modules", "Portal", "Portal.csproj")]));

        var result = DotNetSolutionSetupToolPlanGuard.Evaluate(
            assignment,
            TestWorkspaceServices.PhysicalPathPolicyFactory);

        Assert.True(result.IsSatisfied);
        Assert.Null(result.Plan);
    }

    [Fact]
    public async Task TryExecuteAsync_repairs_existing_project_empty_solution_without_regenerating_project()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        Directory.CreateDirectory(Path.Combine(productRoot, "src", "Calculator"));
        await File.WriteAllTextAsync(Path.Combine(productRoot, "Calculator.slnx"), string.Empty);
        await File.WriteAllTextAsync(
            Path.Combine(productRoot, "src", "Calculator", "Calculator.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.BlazorWebAssembly\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");
        var processHost = new FakeWorkspaceProcessHost(request =>
        {
            if (request.ToolName == "workspace_pwsh_run_script")
            {
                WriteSolutionMembership(productRoot);
            }
        });
        var executor = CreateExecutor(workspace, processHost);

        var result = await executor.TryExecuteAsync(CreateAssignment(productRoot));

        Assert.NotNull(result);
        Assert.True(result!.Succeeded, result.Summary);
        Assert.Single(processHost.Requests);
        Assert.Equal("workspace_pwsh_run_script", processHost.Requests[0].ToolName);
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_dotnet_new" && receipt.RiskClass == "RuntimeOwned:IdempotentSkip");
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_write_file" && receipt.RequestSummary.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_stat_path" && receipt.ExitSummary.Contains("Succeeded", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("src/Calculator/Calculator.csproj", await File.ReadAllTextAsync(Path.Combine(productRoot, "Calculator.slnx")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryExecuteAsync_returns_failure_when_helper_does_not_satisfy_readback()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        Directory.CreateDirectory(Path.Combine(productRoot, "src", "Calculator"));
        await File.WriteAllTextAsync(Path.Combine(productRoot, "Calculator.slnx"), string.Empty);
        await File.WriteAllTextAsync(
            Path.Combine(productRoot, "src", "Calculator", "Calculator.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.BlazorWebAssembly\" />");
        var executor = CreateExecutor(workspace, new FakeWorkspaceProcessHost());

        var result = await executor.TryExecuteAsync(CreateAssignment(productRoot));

        Assert.NotNull(result);
        Assert.False(result!.Succeeded);
        Assert.Contains("readback", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            ProcessRuntimeOwnedStepFailures.ApplyDeclaredIdempotency(
                ProcessRuntimeOwnedStepFailures.ReadbackContentMissing,
                ProcessToolOperationIdempotencyPolicy.CurrentRunRepeatable),
            result.Failure);
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_pwsh_run_script" && receipt.ExitSummary.Contains("Succeeded", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryExecuteAsync_reports_existing_unreadable_readback_as_unavailable()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        WriteExistingAppProject(productRoot);
        await File.WriteAllBytesAsync(
            Path.Combine(productRoot, "Calculator.slnx"),
            [0x00, 0x01, 0x02, 0x03]);
        var executor = CreateExecutor(workspace, new FakeWorkspaceProcessHost());

        var result = await executor.TryExecuteAsync(CreateAssignment(productRoot));

        Assert.NotNull(result);
        Assert.False(result!.Succeeded);
        Assert.Equal(ProcessRuntimeOwnedStepFailures.ReadbackUnavailable, result.Failure);
        Assert.Contains("could not be read", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            result.ToolReceipts,
            receipt =>
                receipt.ToolName == "workspace_read_file" &&
                receipt.ExitSummary.Contains("Denied", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryExecuteAsync_reports_every_missing_readback_candidate()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        WriteExistingAppProject(productRoot);
        WriteSolutionMembership(productRoot);
        var missingSln = Path.Combine(productRoot, "MissingCalculator.sln");
        var missingSlnx = Path.Combine(productRoot, "MissingCalculator.slnx");
        var solutionFile = Path.Combine(productRoot, "Calculator.slnx");
        var launchVariables = new Dictionary<string, string>(
            CreateLaunchVariables(productRoot),
            StringComparer.OrdinalIgnoreCase)
        {
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks] =
                JsonSerializer.Serialize(
                new object[]
                {
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["pathCandidates"] = new[] { solutionFile },
                        ["requiredTextAnyGroups"] = new[] { new[] { "src/Calculator/Calculator.csproj" } }
                    },
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["pathCandidates"] = new[] { missingSln, missingSlnx },
                        ["requiredTextAnyGroups"] = new[] { new[] { "src/Calculator/Calculator.csproj" } }
                    }
                })
        };
        var executor = CreateExecutor(workspace, new FakeWorkspaceProcessHost());

        var result = await executor.TryExecuteAsync(
            CreateAssignment(productRoot, launchVariables: launchVariables));

        Assert.NotNull(result);
        Assert.False(result!.Succeeded);
        Assert.Equal(
            ProcessRuntimeOwnedStepFailures.ApplyDeclaredIdempotency(
                ProcessRuntimeOwnedStepFailures.ReadbackPathMissing,
                ProcessToolOperationIdempotencyPolicy.CurrentRunRepeatable),
            result.Failure);
        Assert.Contains("MissingCalculator.sln", result.Summary, StringComparison.Ordinal);
        Assert.Contains("MissingCalculator.slnx", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryExecuteAsync_runs_add_test_project_helper_and_verifies_solution_and_project_reference()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        WriteExistingAppProject(productRoot);
        await File.WriteAllTextAsync(
            Path.Combine(productRoot, "Calculator.slnx"),
            "src/Calculator/Calculator.csproj");
        var processHost = new FakeWorkspaceProcessHost(request =>
        {
            if (request.ToolName == "workspace_pwsh_run_script")
            {
                WriteTestProjectMembership(productRoot);
            }
        });
        var executor = CreateExecutor(workspace, processHost);

        var result = await executor.TryExecuteAsync(CreateAssignment(
            productRoot,
            "add-test-project",
            CreateAddTestProjectLaunchVariables(productRoot)));

        Assert.NotNull(result);
        Assert.True(result!.Succeeded, result.Summary);
        Assert.Equal(ProcessStepOutcomeStatus.Completed, result.Output!.Status);
        Assert.Single(processHost.Requests);
        Assert.Equal("workspace_pwsh_run_script", processHost.Requests[0].ToolName);
        Assert.DoesNotContain(result.ToolReceipts, receipt => receipt.ToolName == "workspace_dotnet_new");
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_pwsh_run_script" && receipt.ExitSummary.Contains("Succeeded", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("tests/Calculator.Tests/Calculator.Tests.csproj", await File.ReadAllTextAsync(Path.Combine(productRoot, "Calculator.slnx")), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("../../src/Calculator/Calculator.csproj", await File.ReadAllTextAsync(Path.Combine(productRoot, "tests", "Calculator.Tests", "Calculator.Tests.csproj")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryExecuteAsync_returns_failure_when_helper_script_fails()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        WriteExistingAppProject(productRoot);
        await File.WriteAllTextAsync(
            Path.Combine(productRoot, "Calculator.slnx"),
            "src/Calculator/Calculator.csproj");
        var processHost = new FakeWorkspaceProcessHost
        {
            ExitCode = 1,
            FailureMessage = "script failed",
            Stderr = "membership command failed"
        };
        var executor = CreateExecutor(workspace, processHost);

        var result = await executor.TryExecuteAsync(CreateAssignment(
            productRoot,
            "add-test-project",
            CreateAddTestProjectLaunchVariables(productRoot)));

        Assert.NotNull(result);
        Assert.False(result!.Succeeded);
        Assert.Contains("helper failed", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_pwsh_run_script" && receipt.ExitSummary.Contains("Failed (exit 1)", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryExecuteAsync_declines_legacy_dotnet_variables_when_no_descriptor_is_declared()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var launchVariables = new Dictionary<string, string>(CreateLaunchVariables(productRoot), StringComparer.OrdinalIgnoreCase);
        launchVariables.Remove(ProcessRuntimeLaunchVariables.ProcessStepScriptHelperDescriptorJson);
        launchVariables.Remove(ProcessRuntimeLaunchVariables.ProcessStepRuntimeOwnedExecutorKey);
        var executor = CreateExecutor(workspace, new FakeWorkspaceProcessHost());

        var result = await executor.TryExecuteAsync(CreateAssignment(productRoot, launchVariables: launchVariables));

        Assert.Null(result);
    }

    [Fact]
    public async Task TryExecuteAsync_rejects_selected_driver_without_a_descriptor()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var launchVariables = new Dictionary<string, string>(CreateLaunchVariables(productRoot), StringComparer.OrdinalIgnoreCase);
        launchVariables.Remove(ProcessRuntimeLaunchVariables.ProcessStepScriptHelperDescriptorJson);
        var executor = CreateExecutor(workspace, new FakeWorkspaceProcessHost());

        var result = await executor.TryExecuteAsync(CreateAssignment(productRoot, launchVariables: launchVariables));

        Assert.NotNull(result);
        Assert.False(result!.Succeeded);
        Assert.Contains("dotnet.setup.plan.descriptor_missing", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryExecuteAsync_rejects_selected_driver_with_malformed_typed_descriptor_before_tool_invocation()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var launchVariables = new Dictionary<string, string>(CreateLaunchVariables(productRoot), StringComparer.OrdinalIgnoreCase)
        {
            [ProcessRuntimeLaunchVariables.ProcessStepScriptHelperDescriptorJson] = "{ malformed"
        };
        var processHost = new FakeWorkspaceProcessHost();
        var executor = CreateExecutor(workspace, processHost);

        var result = await executor.TryExecuteAsync(CreateAssignment(productRoot, launchVariables: launchVariables));

        Assert.NotNull(result);
        Assert.False(result!.Succeeded);
        Assert.Contains("dotnet.setup.plan.descriptor_missing", result.Summary, StringComparison.Ordinal);
        Assert.Empty(processHost.Requests);
    }

    [Fact]
    public async Task TryExecuteAsync_rejects_create_plan_without_a_selected_app_template()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var launchVariables = new Dictionary<string, string>(CreateLaunchVariables(productRoot), StringComparer.OrdinalIgnoreCase);
        launchVariables.Remove("DotNetAppTemplate");
        var processHost = new FakeWorkspaceProcessHost();
        var executor = CreateExecutor(workspace, processHost);

        var result = await executor.TryExecuteAsync(CreateAssignment(productRoot, launchVariables: launchVariables));

        Assert.NotNull(result);
        Assert.False(result!.Succeeded);
        Assert.Contains("dotnet.setup.plan.app_template_missing", result.Summary, StringComparison.Ordinal);
        Assert.Empty(processHost.Requests);
    }

    [Fact]
    public async Task TryExecuteAsync_rejects_missing_contracted_app_project_without_scanning_required_paths()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var launchVariables = new Dictionary<string, string>(CreateLaunchVariables(productRoot), StringComparer.OrdinalIgnoreCase);
        launchVariables.Remove("DotNetAppProjectFile");
        var processHost = new FakeWorkspaceProcessHost();
        var executor = CreateExecutor(workspace, processHost);

        var result = await executor.TryExecuteAsync(CreateAssignment(productRoot, launchVariables: launchVariables));

        Assert.NotNull(result);
        Assert.False(result!.Succeeded);
        Assert.Contains("DotNetAppProjectFile", result.Summary, StringComparison.Ordinal);
        Assert.Empty(processHost.Requests);
    }

    [Fact]
    public async Task TryExecuteAsync_rejects_selected_driver_with_invalid_typed_readback_contract_before_tool_invocation()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var launchVariables = new Dictionary<string, string>(CreateLaunchVariables(productRoot), StringComparer.OrdinalIgnoreCase)
        {
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks] =
                """[{"pathCandidates":[42],"requiredTextAnyGroups":[["src/Calculator/Calculator.csproj"]]}]"""
        };
        var processHost = new FakeWorkspaceProcessHost();
        var executor = CreateExecutor(workspace, processHost);

        var result = await executor.TryExecuteAsync(CreateAssignment(productRoot, launchVariables: launchVariables));

        Assert.NotNull(result);
        Assert.False(result!.Succeeded);
        Assert.Contains("dotnet.setup.plan.readback_check_invalid", result.Summary, StringComparison.Ordinal);
        Assert.Empty(processHost.Requests);
    }

    [Fact]
    public async Task TryExecuteAsync_rejects_readback_contract_without_solution_candidates_before_tool_invocation()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var appProjectFile = Path.Combine(productRoot, "src", "Calculator", "Calculator.csproj");
        var launchVariables = new Dictionary<string, string>(
            CreateLaunchVariables(productRoot),
            StringComparer.OrdinalIgnoreCase)
        {
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks] =
                JsonSerializer.Serialize(
                new object[]
                {
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["pathCandidates"] = new[] { appProjectFile },
                        ["requiredTextAnyGroups"] = new[] { new[] { "<TargetFramework>net8.0</TargetFramework>" } }
                    }
                })
        };
        var processHost = new FakeWorkspaceProcessHost();
        var executor = CreateExecutor(workspace, processHost);

        var result = await executor.TryExecuteAsync(
            CreateAssignment(productRoot, launchVariables: launchVariables));

        Assert.NotNull(result);
        Assert.False(result!.Succeeded);
        Assert.Contains(
            "dotnet.setup.plan.solution_readback_candidates_missing",
            result.Summary,
            StringComparison.Ordinal);
        Assert.Empty(processHost.Requests);
    }

    [Fact]
    public async Task TryExecuteAsync_rejects_readback_contract_that_omits_an_authoritative_solution_candidate()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var preferredSolutionFile = Path.Combine(productRoot, "Calculator.sln");
        var generatedSolutionFile = Path.Combine(productRoot, "Calculator.slnx");
        var appProjectFile = Path.Combine(productRoot, "src", "Calculator", "Calculator.csproj");
        var launchVariables = new Dictionary<string, string>(
            CreateLaunchVariables(productRoot),
            StringComparer.OrdinalIgnoreCase)
        {
            ["DotNetSolutionFile"] = preferredSolutionFile,
            ["DotNetSolutionFileCandidates"] = $"{preferredSolutionFile}; {generatedSolutionFile}",
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks] =
                JsonSerializer.Serialize(
                new object[]
                {
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["pathCandidates"] = new[] { preferredSolutionFile },
                        ["requiredTextAnyGroups"] = new[] { new[] { "src/Calculator/Calculator.csproj" } }
                    },
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["pathCandidates"] = new[] { appProjectFile },
                        ["requiredTextAnyGroups"] = new[] { new[] { "<TargetFramework>net8.0</TargetFramework>" } }
                    }
                })
        };
        var processHost = new FakeWorkspaceProcessHost();
        var executor = CreateExecutor(workspace, processHost);

        var result = await executor.TryExecuteAsync(
            CreateAssignment(productRoot, launchVariables: launchVariables));

        Assert.NotNull(result);
        Assert.False(result!.Succeeded);
        Assert.Contains(
            "dotnet.setup.plan.solution_readback_candidates_missing",
            result.Summary,
            StringComparison.Ordinal);
        Assert.Empty(processHost.Requests);
    }

    [Fact]
    public async Task TryExecuteAsync_rejects_readback_contract_that_omits_the_singular_solution_candidate()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var singularSolutionFile = Path.Combine(productRoot, "Calculator.sln");
        var generatedSolutionFile = Path.Combine(productRoot, "Calculator.slnx");
        var appProjectFile = Path.Combine(productRoot, "src", "Calculator", "Calculator.csproj");
        var launchVariables = new Dictionary<string, string>(
            CreateLaunchVariables(productRoot),
            StringComparer.OrdinalIgnoreCase)
        {
            ["DotNetSolutionFile"] = singularSolutionFile,
            ["DotNetSolutionFileCandidates"] = generatedSolutionFile,
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks] =
                JsonSerializer.Serialize(
                new object[]
                {
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["pathCandidates"] = new[] { generatedSolutionFile },
                        ["requiredTextAnyGroups"] = new[] { new[] { "src/Calculator/Calculator.csproj" } }
                    },
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["pathCandidates"] = new[] { appProjectFile },
                        ["requiredTextAnyGroups"] = new[] { new[] { "<TargetFramework>net8.0</TargetFramework>" } }
                    }
                })
        };
        var processHost = new FakeWorkspaceProcessHost();
        var executor = CreateExecutor(workspace, processHost);

        var result = await executor.TryExecuteAsync(
            CreateAssignment(productRoot, launchVariables: launchVariables));

        Assert.NotNull(result);
        Assert.False(result!.Succeeded);
        Assert.Contains(
            "dotnet.setup.plan.solution_readback_candidates_missing",
            result.Summary,
            StringComparison.Ordinal);
        Assert.Empty(processHost.Requests);
    }

    [Fact]
    public async Task TryExecuteAsync_rejects_readback_candidate_outside_product_root()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        WriteExistingAppProject(productRoot);
        WriteSolutionMembership(productRoot);
        var solutionFile = Path.Combine(productRoot, "Calculator.slnx");
        var outsideReadbackFile = Path.Combine(workspace.WorkspaceRoot, "outside.slnx");
        await File.WriteAllTextAsync(outsideReadbackFile, "src/Calculator/Calculator.csproj");
        var launchVariables = new Dictionary<string, string>(CreateLaunchVariables(productRoot), StringComparer.OrdinalIgnoreCase)
        {
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks] = JsonSerializer.Serialize(
            new object[]
            {
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["pathCandidates"] = new[] { solutionFile },
                    ["requiredTextAnyGroups"] = new[] { new[] { "src/Calculator/Calculator.csproj" } }
                },
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["pathCandidates"] = new[] { outsideReadbackFile },
                    ["requiredTextAnyGroups"] = new[] { new[] { "src/Calculator/Calculator.csproj" } }
                }
            })
        };
        var processHost = new FakeWorkspaceProcessHost(request =>
        {
            if (request.ToolName == "workspace_pwsh_run_script")
            {
                WriteSolutionMembership(productRoot);
            }
        });
        var executor = CreateExecutor(workspace, processHost);

        var result = await executor.TryExecuteAsync(CreateAssignment(productRoot, launchVariables: launchVariables));

        Assert.NotNull(result);
        Assert.False(result!.Succeeded);
        Assert.Contains("escapes ProductRoot", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryExecuteAsync_verify_existing_uses_existing_alternative_solution_candidate()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var projectFile = Path.Combine(productRoot, "src", "EnterpriseSuite", "EnterpriseSuite.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);
        await File.WriteAllTextAsync(projectFile, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var preferredSolution = Path.Combine(productRoot, "build", "EnterpriseSuite.sln");
        var existingSolution = Path.Combine(productRoot, "build", "EnterpriseSuite.slnx");
        Directory.CreateDirectory(Path.GetDirectoryName(existingSolution)!);
        await File.WriteAllTextAsync(preferredSolution, "stale solution membership");
        await File.WriteAllTextAsync(existingSolution, "src/EnterpriseSuite/EnterpriseSuite.csproj");
        var launchVariables = new Dictionary<string, string>(
            CreateVerifyExistingLaunchVariables(productRoot, [projectFile]),
            StringComparer.OrdinalIgnoreCase)
        {
            ["DotNetSolutionFile"] = preferredSolution,
            ["DotNetSolutionFileCandidates"] = $"{preferredSolution}; {existingSolution}"
        };
        var executor = CreateExecutor(workspace, new FakeWorkspaceProcessHost());

        var result = await executor.TryExecuteAsync(
            CreateAssignment(productRoot, launchVariables: launchVariables));

        Assert.NotNull(result);
        Assert.True(result!.Succeeded, result.Summary);
        Assert.Contains(
            result.ToolReceipts,
            receipt =>
                receipt.ToolName == "workspace_read_file" &&
                receipt.RequestSummary.EndsWith("EnterpriseSuite.slnx", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(
            2,
            result.ToolReceipts.Count(receipt => receipt.ToolName == "workspace_read_file"));
    }

    [Fact]
    public async Task TryExecuteAsync_verify_existing_requires_declared_test_project_membership()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var implementationProject = Path.Combine(
            productRoot,
            "src",
            "EnterpriseSuite",
            "EnterpriseSuite.csproj");
        var testProject = Path.Combine(
            productRoot,
            "tests",
            "EnterpriseSuite.Tests",
            "EnterpriseSuite.Tests.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(implementationProject)!);
        Directory.CreateDirectory(Path.GetDirectoryName(testProject)!);
        await File.WriteAllTextAsync(implementationProject, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        await File.WriteAllTextAsync(testProject, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var solutionFile = Path.Combine(productRoot, "build", "EnterpriseSuite.sln");
        Directory.CreateDirectory(Path.GetDirectoryName(solutionFile)!);
        await File.WriteAllTextAsync(solutionFile, "src/EnterpriseSuite/EnterpriseSuite.csproj");
        var launchVariables = new Dictionary<string, string>(
            CreateVerifyExistingLaunchVariables(productRoot, [implementationProject]),
            StringComparer.OrdinalIgnoreCase)
        {
            ["DotNetTestProjectFiles"] = JsonSerializer.Serialize(new[] { testProject })
        };
        var executor = CreateExecutor(workspace, new FakeWorkspaceProcessHost());

        var result = await executor.TryExecuteAsync(
            CreateAssignment(productRoot, launchVariables: launchVariables));

        Assert.NotNull(result);
        Assert.False(result!.Succeeded);
        Assert.Equal(ProcessRuntimeOwnedStepFailures.VerificationFailed, result.Failure);
        Assert.Contains("EnterpriseSuite.Tests.csproj", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryExecuteAsync_verify_existing_types_invalid_candidate_contract_as_contract_failure()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var projectFile = Path.Combine(productRoot, "src", "EnterpriseSuite", "EnterpriseSuite.csproj");
        var launchVariables = new Dictionary<string, string>(
            CreateVerifyExistingLaunchVariables(productRoot, [projectFile]),
            StringComparer.OrdinalIgnoreCase);
        launchVariables.Remove("DotNetSolutionFile");
        var executor = CreateExecutor(workspace, new FakeWorkspaceProcessHost());

        var result = await executor.TryExecuteAsync(
            CreateAssignment(productRoot, launchVariables: launchVariables));

        Assert.NotNull(result);
        Assert.False(result!.Succeeded);
        Assert.Equal(ProcessRuntimeOwnedStepFailures.ContractInvalid, result.Failure);
    }

    [Fact]
    public async Task TryExecuteAsync_rejects_mismatched_dotnet_descriptor_plan_key_and_kind()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var launchVariables = new Dictionary<string, string>(CreateLaunchVariables(productRoot), StringComparer.OrdinalIgnoreCase)
        {
            [ProcessRuntimeLaunchVariables.ProcessStepScriptHelperDescriptorJson] =
                ProcessRuntimeLaunchVariables.SerializeProcessStepScriptHelperDescriptor(
                    new ProcessRuntimeScriptHelperDescriptor(
                        "DotNetCreateProjectScript",
                        "DotNetCreateProjectScriptRef",
                        "DotNetCreateProjectSideEffectManifest",
                        "dotnet.create-project",
                        "DotNetSolutionAddTestProject",
                        "DotNetCreateProjectExecutionPlan"))
        };
        var processHost = new FakeWorkspaceProcessHost();
        var executor = CreateExecutor(workspace, processHost);

        var result = await executor.TryExecuteAsync(CreateAssignment(productRoot, launchVariables: launchVariables));

        Assert.NotNull(result);
        Assert.False(result!.Succeeded);
        Assert.Contains("dotnet.setup.plan.descriptor_invalid", result.Summary, StringComparison.Ordinal);
        Assert.Empty(processHost.Requests);
    }

    private static DotNetSolutionSetupRuntimeExecutor CreateExecutor(
        RuntimeExecutorWorkspace workspace,
        IWorkspaceProcessHost processHost)
    {
        var externalTargets = TestExternalTargetPathRegistry.Create();
        var workspaceFiles = TestWorkspaceServices.CreateFileService(
            workspace.WorkspaceRoot,
            externalTargetRegistry: externalTargets);
        var workspaceCommands = TestWorkspaceServices.CreateCommandExecutionService(
            workspace.WorkspaceRoot,
            processHost,
            externalTargetRegistry: externalTargets);
        return new DotNetSolutionSetupRuntimeExecutor(
            workspaceCommands,
            new WorkspaceManagedScriptPlanExecutor(
                workspaceFiles,
                workspaceCommands,
                externalTargets,
                TestWorkspaceServices.PhysicalPathPolicyFactory),
            externalTargets,
            new DotNetExistingSolutionVerifier(TestWorkspaceServices.PhysicalPathPolicyFactory),
            TestWorkspaceServices.PhysicalPathPolicyFactory);
    }

    private static ProcessRuntimeStepAssignment CreateAssignment(
        string productRoot,
        string stepKey = "create-dotnet-project",
        IReadOnlyDictionary<string, string>? launchVariables = null)
    {
        return new ProcessRuntimeStepAssignment(
            ProcessRunId.New(),
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            stepKey,
            "dotnet-developer",
            "dotnet-developer",
            ".NET developer",
            ProcessLaunchExecutorKinds.Agent,
            Guid.NewGuid().ToString("D"),
            ".NET Application Developer",
            "Runtime-owned .NET setup.",
            "sha256:readiness",
            "Resolved from role fit.",
            [ArtifactSlotId.New()],
            [],
            [
                ProcessOperationContractNames.MutateProductTarget,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ],
            ProcessOperationContractNames.ExternalProductTargetMutable,
            launchVariables ?? CreateLaunchVariables(productRoot),
            BranchGate: null,
            DateTimeOffset.UtcNow);
    }

    private static IReadOnlyDictionary<string, string> CreateLaunchVariables(string productRoot)
    {
        var solutionFile = Path.Combine(productRoot, "Calculator.slnx");
        var appProjectFile = Path.Combine(productRoot, "src", "Calculator", "Calculator.csproj");
        var scriptRef = $"artifacts/process-runs/{Guid.NewGuid():D}/scripts/create-dotnet-project.wire-solution.ps1";
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["OutputRoot"] = productRoot,
            ["ProductRoot"] = productRoot,
            ["ExternalTargetRoot"] = productRoot,
            ["DotNetSolutionFile"] = solutionFile,
            ["DotNetSolutionFileCandidates"] = solutionFile,
            ["DotNetAppProjectFile"] = appProjectFile,
            ["DotNetAppTemplate"] = "blazorwasm",
            ["DotNetTargetFramework"] = "net8.0",
            ["DotNetCreateProjectScriptRef"] = scriptRef,
            ["DotNetCreateProjectScript"] = "dotnet sln $SolutionFile add $AppProjectFile; dotnet sln $SolutionFile list",
            ["DotNetCreateProjectSideEffectManifest"] = JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["version"] = 1,
                ["mode"] = "ProductMutation",
                ["declaredReadPaths"] = new[] { solutionFile, appProjectFile },
                ["declaredWritePaths"] = new[] { solutionFile },
                ["allowShellDelegation"] = true
            }),
            ["DotNetCreateProjectExecutionPlan"] =
                JsonSerializer.Serialize(new
                {
                    PlanKey = "dotnet.create-project",
                    ScriptRef = scriptRef,
                    WorkspaceAlias = ToExternalTargetAlias(productRoot),
                    RequiresScaffold = true
                }),
            [ProcessRuntimeLaunchVariables.ProcessStepRuntimeOwnedExecutorKey] = "dotnet.solution-setup",
            [ProcessRuntimeLaunchVariables.ProcessStepDeterministicToolPlanDescriptorJson] =
                ProcessRuntimeLaunchVariables.SerializeProcessStepDeterministicToolPlanDescriptor(
                    new ProcessRuntimeDeterministicToolPlanDescriptor(
                        "dotnet.create-project",
                        "DotNetSolutionCreate",
                        "DotNetCreateProjectExecutionPlan",
                        CreateCreateProjectOperationPolicies())),
            [ProcessRuntimeLaunchVariables.ProcessStepScriptHelperDescriptorJson] =
                ProcessRuntimeLaunchVariables.SerializeProcessStepScriptHelperDescriptor(
                    new ProcessRuntimeScriptHelperDescriptor(
                        "DotNetCreateProjectScript",
                        "DotNetCreateProjectScriptRef",
                        "DotNetCreateProjectSideEffectManifest",
                        "dotnet.create-project",
                        "DotNetSolutionCreate",
                        "DotNetCreateProjectExecutionPlan",
                        CreateCreateProjectOperationPolicies())),
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] = JsonSerializer.Serialize(
            new[]
            {
                "template=sln",
                "template=blazorwasm",
                "workspace_pwsh_run_script"
            }),
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths] = JsonSerializer.Serialize(
            new[]
            {
                solutionFile,
                appProjectFile
            }),
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks] = JsonSerializer.Serialize(
            new object[]
            {
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["pathCandidates"] = new[] { solutionFile },
                    ["requiredTextAnyGroups"] = new[] { new[] { "src/Calculator/Calculator.csproj" } }
                },
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["pathCandidates"] = new[] { appProjectFile },
                    ["requiredTextAnyGroups"] = new[] { new[] { "<TargetFramework>net8.0</TargetFramework>" } }
                }
            })
        };
    }

    private static IReadOnlyDictionary<string, string> CreateAddTestProjectLaunchVariables(string productRoot)
    {
        var solutionFile = Path.Combine(productRoot, "Calculator.slnx");
        var appProjectFile = Path.Combine(productRoot, "src", "Calculator", "Calculator.csproj");
        var testProjectFile = Path.Combine(productRoot, "tests", "Calculator.Tests", "Calculator.Tests.csproj");
        var scriptRef = $"artifacts/process-runs/{Guid.NewGuid():D}/scripts/add-test-project.wire-solution.ps1";
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["OutputRoot"] = productRoot,
            ["ProductRoot"] = productRoot,
            ["ExternalTargetRoot"] = productRoot,
            ["DotNetSolutionFile"] = solutionFile,
            ["DotNetSolutionFileCandidates"] = solutionFile,
            ["DotNetAppProjectFile"] = appProjectFile,
            ["DotNetTestProjectFile"] = testProjectFile,
            ["DotNetTestTemplate"] = "mstest",
            ["DotNetTestProjectName"] = "Calculator.Tests",
            ["DotNetAddTestProjectScriptRef"] = scriptRef,
            ["DotNetAddTestProjectScript"] = "dotnet new mstest -n Calculator.Tests -o tests/Calculator.Tests; dotnet sln $SolutionFile add $TestProjectFile; dotnet add $TestProjectFile reference $AppProjectFile; dotnet sln $SolutionFile list",
            ["DotNetAddTestProjectSideEffectManifest"] = JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["version"] = 1,
                ["mode"] = "ProductMutation",
                ["declaredReadPaths"] = new[] { solutionFile, appProjectFile, testProjectFile },
                ["declaredWritePaths"] = new[] { solutionFile, testProjectFile },
                ["allowShellDelegation"] = true
            }),
            ["DotNetAddTestProjectExecutionPlan"] =
                JsonSerializer.Serialize(new
                {
                    PlanKey = "dotnet.add-test-project",
                    ScriptRef = scriptRef,
                    WorkspaceAlias = ToExternalTargetAlias(productRoot),
                    RequiresScaffold = false
                }),
            [ProcessRuntimeLaunchVariables.ProcessStepRuntimeOwnedExecutorKey] = "dotnet.solution-setup",
            [ProcessRuntimeLaunchVariables.ProcessStepScriptHelperDescriptorJson] =
                ProcessRuntimeLaunchVariables.SerializeProcessStepScriptHelperDescriptor(
                    new ProcessRuntimeScriptHelperDescriptor(
                        "DotNetAddTestProjectScript",
                        "DotNetAddTestProjectScriptRef",
                        "DotNetAddTestProjectSideEffectManifest",
                        "dotnet.add-test-project",
                        "DotNetSolutionAddTestProject",
                        "DotNetAddTestProjectExecutionPlan")),
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] = JsonSerializer.Serialize(
            new[]
            {
                "workspace_pwsh_run_script"
            }),
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths] = JsonSerializer.Serialize(
            new[]
            {
                solutionFile,
                appProjectFile,
                testProjectFile
            }),
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks] = JsonSerializer.Serialize(
            new object[]
            {
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["pathCandidates"] = new[] { solutionFile },
                    ["requiredTextAnyGroups"] = new[] { new[] { "src/Calculator/Calculator.csproj" } }
                },
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["pathCandidates"] = new[] { solutionFile },
                    ["requiredTextAnyGroups"] = new[] { new[] { "tests/Calculator.Tests/Calculator.Tests.csproj" } }
                },
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["pathCandidates"] = new[] { testProjectFile },
                    ["requiredTextAnyGroups"] = new[] { new[] { "../../src/Calculator/Calculator.csproj" } }
                }
            })
        };
    }

    private static IReadOnlyList<ProcessToolOperationExecutionPolicy> CreateCreateProjectOperationPolicies()
        =>
        [
            CreateOperationPolicy("create-solution", "workspace_dotnet_new"),
            CreateOperationPolicy("create-app-project", "workspace_dotnet_new"),
            CreateOperationPolicy("write-helper-script", "workspace_write_file"),
            CreateOperationPolicy(
                "run-helper-script",
                "workspace_pwsh_run_script",
                ProcessToolOperationFailureReconciliationPolicy.AuthoritativeReadbackConvergence),
            CreateOperationPolicy("solution-membership-readback", "workspace_read_file")
        ];

    private static ProcessToolOperationExecutionPolicy CreateOperationPolicy(
        string operationKey,
        string toolName,
        ProcessToolOperationFailureReconciliationPolicy failureReconciliation =
            ProcessToolOperationFailureReconciliationPolicy.None)
        => new(
            operationKey,
            toolName,
            ProcessToolOperationIdempotencyPolicy.CurrentRunRepeatable,
            failureReconciliation);

    private static IReadOnlyDictionary<string, string> CreateVerifyExistingLaunchVariables(
        string productRoot,
        IReadOnlyList<string> implementationProjectFiles)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductRoot"] = productRoot,
            ["ExternalTargetRoot"] = productRoot,
            ["DotNetProvisioningMode"] = "verify-existing",
            ["DotNetSolutionFile"] = Path.Combine(productRoot, "build", "EnterpriseSuite.sln"),
            ["DotNetRequiredProjectFiles"] = JsonSerializer.Serialize(implementationProjectFiles),
            ["DotNetTestProjectFiles"] = "[]",
            [ProcessRuntimeLaunchVariables.ProcessStepRuntimeOwnedExecutorKey] = "dotnet.solution-setup"
        };
    }

    private static void CreateDotNetNewOutput(
        WorkspaceProcessExecutionRequest request,
        string solutionExtension = ".slnx")
    {
        var template = request.Arguments[1];
        var nameIndex = request.Arguments.ToList().IndexOf("-n");
        var name = request.Arguments[nameIndex + 1];
        if (string.Equals(template, "sln", StringComparison.OrdinalIgnoreCase))
        {
            File.WriteAllText(
                Path.Combine(request.WorkingDirectory, $"{name}{solutionExtension}"),
                string.Empty);
            return;
        }

        var projectDirectory = Path.Combine(request.WorkingDirectory, name);
        Directory.CreateDirectory(projectDirectory);
        var frameworkIndex = request.Arguments.ToList().IndexOf("--framework");
        var targetFramework = frameworkIndex >= 0 && frameworkIndex + 1 < request.Arguments.Count
            ? request.Arguments[frameworkIndex + 1]
            : string.Empty;
        File.WriteAllText(
            Path.Combine(projectDirectory, $"{name}.csproj"),
            string.IsNullOrWhiteSpace(targetFramework)
                ? "<Project Sdk=\"Microsoft.NET.Sdk.BlazorWebAssembly\" />"
                : $"<Project Sdk=\"Microsoft.NET.Sdk.BlazorWebAssembly\"><PropertyGroup><TargetFramework>{targetFramework}</TargetFramework></PropertyGroup></Project>");
    }

    private static void WriteSolutionMembership(
        string productRoot,
        string solutionExtension = ".slnx")
    {
        File.WriteAllText(
            Path.Combine(productRoot, $"Calculator{solutionExtension}"),
            "src/Calculator/Calculator.csproj");
    }

    private static void WriteExistingAppProject(string productRoot)
    {
        Directory.CreateDirectory(Path.Combine(productRoot, "src", "Calculator"));
        File.WriteAllText(
            Path.Combine(productRoot, "src", "Calculator", "Calculator.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.BlazorWebAssembly\" />");
    }

    private static void WriteTestProjectMembership(string productRoot)
    {
        Directory.CreateDirectory(Path.Combine(productRoot, "tests", "Calculator.Tests"));
        File.WriteAllText(
            Path.Combine(productRoot, "Calculator.slnx"),
            """
            src/Calculator/Calculator.csproj
            tests/Calculator.Tests/Calculator.Tests.csproj
            """);
        File.WriteAllText(
            Path.Combine(productRoot, "tests", "Calculator.Tests", "Calculator.Tests.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="../../src/Calculator/Calculator.csproj" />
              </ItemGroup>
            </Project>
            """);
    }

    private static string ToExternalTargetAlias(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var rootPath = Path.GetPathRoot(fullPath)
            ?? throw new InvalidOperationException($"Path '{path}' has no root.");
        var trimmedRoot = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (trimmedRoot.Length != 2 ||
            trimmedRoot[1] != ':' ||
            !char.IsLetter(trimmedRoot[0]))
        {
            return fullPath;
        }

        var relativePath = fullPath.Length <= rootPath.Length
            ? string.Empty
            : fullPath[rootPath.Length..]
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);
        return string.IsNullOrWhiteSpace(relativePath)
            ? $"external-target/{char.ToUpperInvariant(trimmedRoot[0])}"
            : $"external-target/{char.ToUpperInvariant(trimmedRoot[0])}/{relativePath.Replace(Path.DirectorySeparatorChar, '/')}";
    }

    private sealed class RuntimeExecutorWorkspace : IDisposable
    {
        private readonly List<string> roots = [];

        public RuntimeExecutorWorkspace()
        {
            WorkspaceRoot = CreateRoot("Workspace");
        }

        public string WorkspaceRoot { get; }

        public string CreateProductRoot()
            => CreateRoot("Product");

        public void Dispose()
        {
            foreach (var root in roots)
            {
                try
                {
                    if (Directory.Exists(root))
                    {
                        Directory.Delete(root, recursive: true);
                    }
                }
                catch
                {
                }
            }
        }

        private string CreateRoot(string name)
        {
            var root = Path.Combine(Path.GetTempPath(), $"CanDoItAll.DotNetSolutionSetupRuntimeExecutorTests.{name}.{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            roots.Add(root);
            return root;
        }
    }

    private sealed class FakeWorkspaceProcessHost(Action<WorkspaceProcessExecutionRequest>? onExecute = null) : IWorkspaceProcessHost
    {
        public List<WorkspaceProcessExecutionRequest> Requests { get; } = [];

        public int ExitCode { get; init; }

        public Func<WorkspaceProcessExecutionRequest, int>? ExitCodeResolver { get; init; }

        public string Stdout { get; init; } = "ok";

        public string Stderr { get; init; } = string.Empty;

        public bool TimedOut { get; init; }

        public string FailureMessage { get; init; } = string.Empty;

        public ExecutionBoundaryDescriptor DescribeBoundary()
        {
            return new ExecutionBoundaryDescriptor(
                "Test",
                "Workspace",
                "None",
                "None",
                "Fake",
                IsEnforcedByHost: false,
                "Unit test host.");
        }

        public Task<WorkspaceProcessExecutionResult> ExecuteAsync(
            WorkspaceProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            onExecute?.Invoke(request);
            var now = DateTimeOffset.UtcNow;
            var exitCode = ExitCodeResolver?.Invoke(request) ?? ExitCode;
            return Task.FromResult(new WorkspaceProcessExecutionResult(
                Started: true,
                ExitCode: exitCode,
                Stdout: Stdout,
                Stderr: Stderr,
                StdoutTruncated: false,
                StderrTruncated: false,
                StartedAtUtc: now,
                CompletedAtUtc: now,
                TimedOut: TimedOut,
                Boundary: DescribeBoundary(),
                FailureMessage: FailureMessage));
        }
    }
}
