using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceCommandReceiptWriterTests
{
    [Fact]
    public void Descriptor_receipts_redact_a_sensitive_value_in_the_next_argv_element()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.WorkspaceCommandReceiptWriterTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);
        const string secretValue = "descriptor-secret-value-that-must-not-be-serialized";
        var writer = new WorkspaceCommandReceiptWriter(workspaceRoot);
        var now = DateTimeOffset.UtcNow;

        try
        {
            var receipt = writer.PersistDescriptorReceipt(
                toolName: "descriptor_test",
                recipeId: "descriptor_test",
                riskClass: "LocalExecution",
                approvalRequired: false,
                workingDirectory: ".",
                arguments: ["--password", secretValue],
                targetPaths: [],
                message: "Prepared descriptor.",
                boundary: new ExecutionBoundaryDescriptor(
                    Mode: "Test",
                    FilesystemScope: "Workspace",
                    NetworkScope: "None",
                    CredentialScope: "None",
                    HostLabel: "Receipt test host",
                    IsEnforcedByHost: false,
                    Notes: "Unit test boundary."),
                startedAtUtc: now,
                completedAtUtc: now,
                extraPayload: new { kind = "test" });

            var receiptJson = File.ReadAllText(
                ResolveArtifactPath(workspaceRoot, receipt.ReceiptRelativePath));

            Assert.DoesNotContain(secretValue, receiptJson, StringComparison.Ordinal);
            Assert.Contains("[REDACTED]", receiptJson, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task Process_receipts_record_sorted_environment_names_without_values()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.WorkspaceCommandReceiptWriterTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);
        const string secretValue = "receipt-secret-value-that-must-not-be-serialized";
        var processHost = new SuccessfulWorkspaceProcessHost(
            Stdout: "ok api_key=receipt-output-secret",
            Stderr: "password=receipt-error-secret");
        var runner = new WorkspaceCommandProcessRunner(
            processHost,
            new WorkspaceCommandEnvironmentPolicy(),
            new WorkspaceExecutableLocator(),
            new WorkspaceCommandReceiptWriter(workspaceRoot),
            TestWorkspaceServices.CreatePathPolicy(
                workspaceRoot,
                externalTargetRegistry: TestExternalTargetPathRegistry.Create()));
        var plan = new WorkspaceCommandPlan(
            Decision: new ToolExecutionDecision(
                ToolName: "workspace_receipt_test",
                RecipeId: "environment-receipt-test",
                RiskClass: "ReadOnly",
                Allowed: true,
                ApprovalRequired: false,
                NetworkAllowed: false,
                ExternalRootsAllowed: false,
                Reason: "Unit test receipt serialization."),
            MutatesWorkspace: false,
            TargetPaths: [],
            WorkspaceRootPath: workspaceRoot,
            WorkingDirectory: ".",
            WorkingDirectoryPath: workspaceRoot,
            ExecutableCandidates: ["dotnet"],
            Arguments:
            [
                "--info",
                "--api_key=argument-secret-value",
                "--client-token",
                "separate-argument-secret-value"
            ],
            TimeoutSeconds: 30,
            StdoutLimitCharacters: 4096,
            StderrLimitCharacters: 4096,
            EnvironmentVariables: new Dictionary<string, string?>
            {
                ["Z_PRIVATE_TOKEN"] = secretValue,
                ["A_RECIPE_SETTING"] = "public-recipe-value"
            });

        try
        {
            var result = await runner.ExecuteAsync(plan);

            Assert.True(result.Succeeded);
            Assert.NotNull(processHost.LastRequest);
            Assert.Equal(secretValue, processHost.LastRequest.EnvironmentVariables["Z_PRIVATE_TOKEN"]);

            var requestReference = Assert.Single(
                result.ArtifactReferences,
                reference => reference.DisplayName.Equals(
                    "environment-receipt-test request",
                    StringComparison.Ordinal));
            var requestJson = await File.ReadAllTextAsync(
                ResolveArtifactPath(workspaceRoot, requestReference.RelativePath));
            var receiptJson = await File.ReadAllTextAsync(
                ResolveArtifactPath(workspaceRoot, result.Receipt.ReceiptRelativePath));
            var requestEnvironmentNames = ReadEnvironmentVariableNames(requestJson);
            var receiptEnvironmentNames = ReadEnvironmentVariableNames(receiptJson);

            Assert.Contains("A_RECIPE_SETTING", requestEnvironmentNames);
            Assert.Contains("Z_PRIVATE_TOKEN", requestEnvironmentNames);
            Assert.Equal(
                requestEnvironmentNames
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(name => name, StringComparer.Ordinal),
                requestEnvironmentNames);
            Assert.Equal(requestEnvironmentNames, receiptEnvironmentNames);
            Assert.DoesNotContain(secretValue, requestJson, StringComparison.Ordinal);
            Assert.DoesNotContain(secretValue, receiptJson, StringComparison.Ordinal);
            Assert.DoesNotContain("argument-secret-value", requestJson, StringComparison.Ordinal);
            Assert.DoesNotContain("argument-secret-value", receiptJson, StringComparison.Ordinal);
            Assert.DoesNotContain("argument-secret-value", result.ArgumentsSummary, StringComparison.Ordinal);
            Assert.DoesNotContain("separate-argument-secret-value", requestJson, StringComparison.Ordinal);
            Assert.DoesNotContain("separate-argument-secret-value", receiptJson, StringComparison.Ordinal);
            Assert.DoesNotContain("separate-argument-secret-value", result.ArgumentsSummary, StringComparison.Ordinal);
            Assert.DoesNotContain("receipt-output-secret", result.StdoutPreview, StringComparison.Ordinal);
            Assert.DoesNotContain("receipt-error-secret", result.StderrPreview, StringComparison.Ordinal);

            var stdoutReference = Assert.Single(
                result.ArtifactReferences,
                reference => reference.DisplayName.Equals(
                    "environment-receipt-test stdout",
                    StringComparison.Ordinal));
            var stderrReference = Assert.Single(
                result.ArtifactReferences,
                reference => reference.DisplayName.Equals(
                    "environment-receipt-test stderr",
                    StringComparison.Ordinal));
            var stdout = await File.ReadAllTextAsync(
                ResolveArtifactPath(workspaceRoot, stdoutReference.RelativePath));
            var stderr = await File.ReadAllTextAsync(
                ResolveArtifactPath(workspaceRoot, stderrReference.RelativePath));
            Assert.DoesNotContain("receipt-output-secret", stdout, StringComparison.Ordinal);
            Assert.DoesNotContain("receipt-error-secret", stderr, StringComparison.Ordinal);
            Assert.Contains("[REDACTED]", stdout, StringComparison.Ordinal);
            Assert.Contains("[REDACTED]", stderr, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    private static string[] ReadEnvironmentVariableNames(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement
            .GetProperty("environmentVariableNames")
            .EnumerateArray()
            .Select(element => element.GetString())
            .OfType<string>()
            .ToArray();
    }

    private static string ResolveArtifactPath(string workspaceRoot, string relativePath)
        => Path.Combine(workspaceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private sealed class SuccessfulWorkspaceProcessHost(string Stdout = "ok", string Stderr = "") : IWorkspaceProcessHost
    {
        private static readonly ExecutionBoundaryDescriptor Boundary = new(
            Mode: "Test",
            FilesystemScope: "Workspace",
            NetworkScope: "None",
            CredentialScope: "None",
            HostLabel: "Receipt test host",
            IsEnforcedByHost: false,
            Notes: "Unit test boundary.");

        public WorkspaceProcessExecutionRequest? LastRequest { get; private set; }

        public ExecutionBoundaryDescriptor DescribeBoundary() => Boundary;

        public Task<WorkspaceProcessExecutionResult> ExecuteAsync(
            WorkspaceProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(
                new WorkspaceProcessExecutionResult(
                    Started: true,
                    ExitCode: 0,
                    Stdout: Stdout,
                    Stderr: Stderr,
                    StdoutTruncated: false,
                    StderrTruncated: false,
                    StartedAtUtc: now,
                    CompletedAtUtc: now,
                    TimedOut: false,
                    Boundary: Boundary,
                    FailureMessage: string.Empty));
        }
    }
}
