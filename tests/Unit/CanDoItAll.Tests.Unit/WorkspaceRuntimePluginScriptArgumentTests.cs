using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceRuntimePluginScriptArgumentTests : IDisposable
{
    private readonly string rootPath = Path.Combine(
        Path.GetTempPath(),
        "CanDoItAll.WorkspaceRuntimePluginScriptArgumentTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void PowerShell_script_argument_outside_allowed_roots_is_denied_before_process_execution()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var allowedRoot = CreateDirectory("allowed");
        var deniedRoot = CreateDirectory("denied");
        var scriptPath = CreateScript(workspaceRoot, "Read-Input.ps1", "Write-Output 'ok'");
        var processHost = new RecordingWorkspaceProcessHost();
        var plugin = CreatePlugin(workspaceRoot, allowedRoot, processHost);

        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            _ = plugin.RunWorkspacePowerShellScript(
                Path.GetRelativePath(workspaceRoot, scriptPath),
                arguments: [$"--input={Path.Combine(deniedRoot, "secret.json")}"]);
        });

        Assert.Contains("not in this agent's allowed external workspace roots", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(processHost.LastRequest);
    }

    [Fact]
    public async Task PowerShell_script_argument_under_allowed_root_is_canonicalized_before_process_execution()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var allowedRoot = CreateDirectory("allowed");
        var scriptPath = CreateScript(workspaceRoot, "Read-Input.ps1", "Write-Output 'ok'");
        var inputPath = Path.Combine(allowedRoot, "request.json");
        await File.WriteAllTextAsync(inputPath, "{}");
        var processHost = new RecordingWorkspaceProcessHost();
        var plugin = CreatePlugin(workspaceRoot, allowedRoot, processHost);

        var result = await plugin.RunWorkspacePowerShellScript(
            Path.GetRelativePath(workspaceRoot, scriptPath),
            arguments:
            [
                $"--input={inputPath}",
                "--mode=validate"
            ]);

        Assert.True(result.Succeeded, result.Message);
        Assert.NotNull(processHost.LastRequest);
        Assert.Contains(
            $"--input={inputPath}",
            processHost.LastRequest!.Arguments,
            StringComparer.OrdinalIgnoreCase);
        Assert.Contains("--mode=validate", processHost.LastRequest.Arguments, StringComparer.Ordinal);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
        catch
        {
        }
    }

    private WorkspaceRuntimePlugin CreatePlugin(
        string workspaceRoot,
        string allowedRoot,
        IWorkspaceProcessHost processHost)
    {
        var accessSettings = AgentWorkspaceToolAccessProfiles.CreateSettings(
            AgentWorkspaceToolProfileKind.SoftwareDevelopment);
        accessSettings.AllowedExternalTargetAliases =
        [
            AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(allowedRoot)
            ?? throw new InvalidOperationException("The test external root could not be normalized.")
        ];
        var commandService = new WorkspaceCommandExecutionService(workspaceRoot, processHost);

        return new WorkspaceRuntimePlugin(
            commandService,
            null!,
            workspaceRoot,
            WorkspaceScopeDescriptor.Sandbox,
            accessSettings,
            CreateProvider(),
            "test-model",
            null!);
    }

    private string CreateDirectory(string name)
    {
        var path = Path.Combine(rootPath, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateScript(string workspaceRoot, string fileName, string content)
    {
        var scriptDirectory = Path.Combine(workspaceRoot, "scripts");
        Directory.CreateDirectory(scriptDirectory);
        var scriptPath = Path.Combine(scriptDirectory, fileName);
        File.WriteAllText(scriptPath, content);
        return scriptPath;
    }

    private static ProviderProfile CreateProvider()
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            "Test Provider",
            ProviderKind.OpenAi,
            "https://provider.example.test",
            "PROVIDER_API_KEY",
            "test-model",
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: false,
            SupportsTools: false,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: ["test-model"],
            Purpose: ProviderProfilePurpose.Chat);
    }

    private sealed class RecordingWorkspaceProcessHost : IWorkspaceProcessHost
    {
        public WorkspaceProcessExecutionRequest? LastRequest { get; private set; }

        public ExecutionBoundaryDescriptor DescribeBoundary()
        {
            return new ExecutionBoundaryDescriptor(
                Mode: "Test",
                FilesystemScope: "Workspace",
                NetworkScope: "None",
                CredentialScope: "None",
                HostLabel: "Fake",
                IsEnforcedByHost: false,
                Notes: "Unit test host.");
        }

        public Task<WorkspaceProcessExecutionResult> ExecuteAsync(
            WorkspaceProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new WorkspaceProcessExecutionResult(
                Started: true,
                ExitCode: 0,
                Stdout: "ok",
                Stderr: string.Empty,
                StdoutTruncated: false,
                StderrTruncated: false,
                StartedAtUtc: now,
                CompletedAtUtc: now,
                TimedOut: false,
                Boundary: DescribeBoundary(),
                FailureMessage: string.Empty));
        }
    }
}
