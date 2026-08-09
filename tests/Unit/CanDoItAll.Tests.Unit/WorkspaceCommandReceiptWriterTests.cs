using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceCommandReceiptWriterTests
{
    [Fact]
    public async Task Process_receipts_record_sorted_environment_names_without_values()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.WorkspaceCommandReceiptWriterTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);
        const string secretValue = "receipt-secret-value-that-must-not-be-serialized";
        var processHost = new SuccessfulWorkspaceProcessHost();
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
            Arguments: ["--info"],
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
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase),
                requestEnvironmentNames);
            Assert.Equal(requestEnvironmentNames, receiptEnvironmentNames);
            Assert.DoesNotContain(secretValue, requestJson, StringComparison.Ordinal);
            Assert.DoesNotContain(secretValue, receiptJson, StringComparison.Ordinal);
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

    private sealed class SuccessfulWorkspaceProcessHost : IWorkspaceProcessHost
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
                    Stdout: "ok",
                    Stderr: string.Empty,
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
