using System.Text.Json.Nodes;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Playwright;
using Npgsql;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class AppSmokeTests
{
    [Fact]
    [Trait("Category", "UnixRuntimePortability")]
    [Trait("Surface", "SharedCanvas")]
    public async Task Runtime_node_actions_show_direct_optional_and_dependency_missing_states()
    {
        var artifactsDirectory = Path.Combine(GetRepoRoot(), "output", "playwright");
        Directory.CreateDirectory(artifactsDirectory);
        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1000
            }
        });
        var createProjectResponse = await context.APIRequest.PostAsync(
            $"{fixture.BaseUrl}/_dev/projects?name={Uri.EscapeDataString("Playwright Runtime Capabilities")}&phase=Execution");
        var createProjectBody = await createProjectResponse.TextAsync();
        Assert.True(
            createProjectResponse.Ok,
            $"Expected the development project endpoint to return 2xx, got {createProjectResponse.Status}: {createProjectBody}");
        var createProjectPayload = JsonNode.Parse(createProjectBody)
            ?? throw new InvalidOperationException("The development project endpoint returned no payload.");
        var projectId = Guid.Parse(
            createProjectPayload["projectId"]?.GetValue<string>()
            ?? throw new InvalidOperationException("The development project endpoint returned no project id."));
        Assert.False(string.IsNullOrWhiteSpace(fixture.StorageWorkspaceRoot));
        var runtimeDirectory = Path.Combine(fixture.StorageWorkspaceRoot!, "runtime", "b02-capability-app");
        Directory.CreateDirectory(runtimeDirectory);
        var projectPath = Path.Combine(runtimeDirectory, "CapabilityApp.csproj");
        await File.WriteAllTextAsync(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

        var runtimeNodeId = await CreateRuntimeNodeAsync(
            context,
            projectId,
            ProjectObjectType.Environment,
            "dotnet-runtime",
            "Portable runtime",
            ".NET runtime",
            "Validate host-aware runtime actions.",
            new ProjectObjectMetadataEnvelope
            {
                Environment = new ProjectEnvironmentMetadata
                {
                    EnvironmentKind = ProjectEnvironmentKind.DotNetRuntime,
                    ProjectPath = projectPath,
                    WorkingDirectory = runtimeDirectory
                }
            });

        var explicitScriptNodeId = await CreateRuntimeNodeAsync(
            context,
            projectId,
            ProjectObjectType.Script,
            OperatingSystem.IsWindows() ? "powershell" : "posix-shell",
            "Approval-gated script",
            "Explicit script",
            "Validate one-launch approval enforcement.",
            new ProjectObjectMetadataEnvelope
            {
                Script = new ProjectScriptMetadata
                {
                    ScriptKind = OperatingSystem.IsWindows() ? ProjectScriptKind.PowerShell : ProjectScriptKind.PosixShell,
                    Command = OperatingSystem.IsWindows() ? "Write-Output approval-test" : "printf approval-test",
                    WorkingDirectory = fixture.StorageWorkspaceRoot!
                }
            });
        var missingDependencyNodeId = await CreateRuntimeNodeAsync(
            context,
            projectId,
            ProjectObjectType.Script,
            "console",
            "Missing runtime dependency",
            "Console script",
            "Validate dependency-missing runtime guidance.",
            new ProjectObjectMetadataEnvelope
            {
                Script = new ProjectScriptMetadata
                {
                    ScriptKind = ProjectScriptKind.Console,
                    Command = "b02-definitely-missing-executable",
                    WorkingDirectory = fixture.StorageWorkspaceRoot!
                }
            });
        string? headlessNodeId = null;
        if (fixture.IsRuntimePresentationHeadless)
        {
            var pythonDirectory = Path.Combine(fixture.StorageWorkspaceRoot!, "runtime", "b02-headless-python");
            var environmentDirectory = Path.Combine(pythonDirectory, ".venv");
            var interpreterPath = Path.Combine(
                environmentDirectory,
                OperatingSystem.IsWindows() ? "Scripts" : "bin",
                OperatingSystem.IsWindows() ? "python.exe" : "python");
            Directory.CreateDirectory(Path.GetDirectoryName(interpreterPath)!);
            await File.WriteAllTextAsync(interpreterPath, string.Empty);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    interpreterPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            headlessNodeId = await CreateRuntimeNodeAsync(
                context,
                projectId,
                ProjectObjectType.Environment,
                "python",
                "Headless interactive Python",
                "Python environment",
                "Validate terminal-only behavior without a configured terminal.",
                new ProjectObjectMetadataEnvelope
                {
                    Environment = new ProjectEnvironmentMetadata
                    {
                        EnvironmentKind = ProjectEnvironmentKind.PythonEnvironment,
                        PythonProvider = ProjectPythonProvider.Python,
                        EnvironmentName = ".venv",
                        ProjectPath = pythonDirectory,
                        WorkingDirectory = pythonDirectory
                    }
                });
        }

        var page = await context.NewPageAsync();
        await page.GotoAsync($"{fixture.BaseUrl}/projects");
        await DismissStartupModalIfPresentAsync(page, timeoutMs: 15_000);
        var projectCard = page.GetByTestId("project-card").Filter(new LocatorFilterOptions
        {
            HasText = "Playwright Runtime Capabilities"
        });
        await projectCard.WaitForAsync();
        await Task.WhenAll(
            page.WaitForURLAsync($"**/projects/{projectId:D}/structure"),
            projectCard.GetByTestId("project-card-structure-button").ClickAsync());
        await page.GetByTestId("project-structure-canvas-loaded").WaitForAsync();

        await OpenNodeQuickActionsAsync(page, SelectorForNodeId(runtimeNodeId));
        var dialog = page.GetByTestId("project-structure-node-quick-actions");
        await dialog.WaitForAsync();
        var run = page.GetByTestId("project-structure-quick-action-runtime-open");
        await run.WaitForAsync();
        Assert.Contains("Run", await run.TextContentAsync(), StringComparison.Ordinal);
        Assert.Contains("owned process host", await run.TextContentAsync(), StringComparison.OrdinalIgnoreCase);
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(
                !fixture.IsRuntimePresentationHeadless,
                await page.GetByTestId("project-structure-quick-action-runtime-terminal").IsVisibleAsync());
            Assert.True(await page.GetByTestId("project-structure-quick-action-runtime-admin").IsVisibleAsync());
        }
        else
        {
            Assert.False(await page.GetByTestId("project-structure-quick-action-runtime-admin").IsVisibleAsync());
        }

        await CaptureLocatorAsync(
            dialog,
            Path.Combine(artifactsDirectory, "b07-runtime-capabilities-available.png"));
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await dialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });

        await OpenNodeQuickActionsAsync(page, SelectorForNodeId(explicitScriptNodeId));
        dialog = page.GetByTestId("project-structure-node-quick-actions");
        await dialog.WaitForAsync();
        await page.GetByTestId("project-structure-quick-action-runtime-open").ClickAsync();
        var approvalDialog = page.GetByTestId("project-structure-runtime-launch-approval-dialog");
        await approvalDialog.WaitForAsync();
        Assert.DoesNotContain(
            fixture.StorageWorkspaceRoot!,
            await approvalDialog.TextContentAsync() ?? string.Empty,
            StringComparison.Ordinal);
        await page.GetByTestId("project-structure-runtime-launch-approval-cancel").ClickAsync();
        await approvalDialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });

        await OpenNodeQuickActionsAsync(page, SelectorForNodeId(missingDependencyNodeId));
        dialog = page.GetByTestId("project-structure-node-quick-actions");
        await dialog.WaitForAsync();
        var unavailable = page.GetByTestId("project-structure-quick-action-primary");
        await unavailable.WaitForAsync();
        Assert.True(await unavailable.IsDisabledAsync());
        Assert.Contains("Runtime unavailable", await unavailable.TextContentAsync(), StringComparison.Ordinal);
        Assert.Contains("executable dependency", await unavailable.TextContentAsync(), StringComparison.OrdinalIgnoreCase);
        await CaptureLocatorAsync(
            dialog,
            Path.Combine(artifactsDirectory, "b07-runtime-capabilities-dependency-missing.png"));

        if (fixture.IsRuntimePresentationHeadless)
        {
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
            await dialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });
            await OpenNodeQuickActionsAsync(page, SelectorForNodeId(headlessNodeId!));
            dialog = page.GetByTestId("project-structure-node-quick-actions");
            await dialog.WaitForAsync();
            unavailable = page.GetByTestId("project-structure-quick-action-primary");
            await unavailable.WaitForAsync();
            Assert.True(await unavailable.IsDisabledAsync());
            Assert.Contains("terminal", await unavailable.TextContentAsync(), StringComparison.OrdinalIgnoreCase);
            await CaptureLocatorAsync(
                dialog,
                Path.Combine(artifactsDirectory, "b07-runtime-capabilities-headless.png"));
        }

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await dialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });
        await ReplaceRuntimeProjectPathAsync(
            fixture.DatabaseConnectionString!,
            projectId,
            runtimeNodeId,
            OperatingSystem.IsWindows() ? "/tmp/foreign/CapabilityApp.csproj" : @"C:\foreign\CapabilityApp.csproj");
        await page.ReloadAsync();
        await page.WaitForSelectorAsync("[data-testid='project-structure-canvas-loaded']");
        await OpenNodeQuickActionsAsync(page, SelectorForNodeId(runtimeNodeId));
        dialog = page.GetByTestId("project-structure-node-quick-actions");
        await dialog.WaitForAsync();
        unavailable = page.GetByTestId("project-structure-quick-action-primary");
        await unavailable.WaitForAsync();
        Assert.True(await unavailable.IsDisabledAsync());
        Assert.Contains("path syntax", await unavailable.TextContentAsync(), StringComparison.OrdinalIgnoreCase);
        await CaptureLocatorAsync(
            dialog,
            Path.Combine(artifactsDirectory, "b07-runtime-capabilities-foreign-path.png"));

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private async Task<string> CreateRuntimeNodeAsync(
        IBrowserContext context,
        Guid projectId,
        ProjectObjectType objectType,
        string objectSubtype,
        string title,
        string subtitle,
        string notes,
        ProjectObjectMetadataEnvelope metadata)
    {
        var projectRootId = $"project:{projectId:D}";
        var request = new ProjectStructureNodeCreateInput(
            objectType,
            title,
            subtitle,
            notes,
            projectRootId,
            ObjectSubtype: objectSubtype,
            MetadataJson: ProjectObjectMetadataSerializer.Serialize(metadata));
        var response = await context.APIRequest.PostAsync(
            $"{fixture.BaseUrl}/api/project-structure/projects/{projectId:D}/nodes",
            new APIRequestContextOptions
            {
                DataObject = request,
                Timeout = 30_000
            });
        var responseBody = await response.TextAsync();
        Assert.True(
            response.Ok,
            $"Expected runtime node '{title}' creation to return 2xx, got {response.Status}: {responseBody}");
        var payload = JsonNode.Parse(responseBody)
            ?? throw new InvalidOperationException($"Runtime node '{title}' creation returned no payload.");
        return payload["id"]?.GetValue<string>()
            ?? throw new InvalidOperationException($"Runtime node '{title}' creation returned no node id.");
    }

    private static async Task ReplaceRuntimeProjectPathAsync(
        string connectionString,
        Guid projectId,
        string nodeKey,
        string projectPath)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var readCommand = connection.CreateCommand();
        readCommand.CommandText =
            """
            SELECT "MetadataJson"
            FROM "Workbench_ProjectObjects"
            WHERE "ProjectId" = @projectId AND "NodeKey" = @nodeKey
            """;
        readCommand.Parameters.AddWithValue("projectId", projectId);
        readCommand.Parameters.AddWithValue("nodeKey", nodeKey);
        var metadataJson = Assert.IsType<string>(await readCommand.ExecuteScalarAsync());
        var metadata = JsonNode.Parse(metadataJson)?.AsObject()
            ?? throw new InvalidOperationException("The runtime node metadata is missing.");
        var environment = metadata["environment"]?.AsObject()
            ?? throw new InvalidOperationException("The runtime node environment metadata is missing.");
        environment["projectPath"] = projectPath;

        await using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText =
            """
            UPDATE "Workbench_ProjectObjects"
            SET "MetadataJson" = @metadataJson
            WHERE "ProjectId" = @projectId AND "NodeKey" = @nodeKey
            """;
        updateCommand.Parameters.AddWithValue("metadataJson", metadata.ToJsonString());
        updateCommand.Parameters.AddWithValue("projectId", projectId);
        updateCommand.Parameters.AddWithValue("nodeKey", nodeKey);
        Assert.Equal(1, await updateCommand.ExecuteNonQueryAsync());
    }
}
