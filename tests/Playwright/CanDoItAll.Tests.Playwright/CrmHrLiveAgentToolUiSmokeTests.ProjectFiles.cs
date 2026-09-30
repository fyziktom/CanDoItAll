using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

public sealed partial class CrmHrLiveAgentToolUiSmokeTests {
    [Fact]
    [Trait("Category", "LiveAgent")]
    public async Task Project_structure_scoped_agent_reads_creates_attaches_and_reopens_actual_file_content() {
        const string scenario = "ui-project-structure-agent-file-roundtrip";
        if (!IsLiveValidationEnabled()) {
            await UiEvidence.WriteNotRunAsync(scenario, "The dedicated live gates are closed.");
            return;
        }
        var evidence = new UiEvidence(scenario);
        await using var host = await LiveUiHost.StartAsync();
        try {
            var fixture = await CreateFileJourneyAsync(host);
            var originalProfile = await host.SeedAsync(services => {
                var canonical = services.GetRequiredService<ICanonicalRuntimeDatabase>();
                return Task.FromResult((Id: canonical.Profile.Profile.Id, canonical.Generation));
            });
            var page = await host.NewPageAsync();
            var oracle = CrmHrBrowserOracle.Attach(page);
            await GrantFileJourneyAsync(host, oracle, page, fixture);
            host.Watch(fixture.Agent.Id);
            await evidence.DescribeProviderAsync(host, await host.SeedAsync(services => FindAgentAsync(services, fixture.Agent.Id)));
            evidence.Targets["projectId"] = fixture.ProjectId;
            evidence.Targets["selectedNode"] = fixture.ParentId;
            evidence.Targets["seedAssetNode"] = fixture.SeedAssetId;
            evidence.Targets["siblingProjectId"] = fixture.SiblingId;
            var chat = await OpenFileChatAsync(host, oracle, page, fixture);
            await page.ScreenshotAsync(new() { Path = host.Artifact("files-01-selected-chat.png") });
            if (IsRehearsal()) {
                evidence.Execution = "rehearsal";
                evidence.Observations["rehearsal"] = "saved-scoped-grants-and-selected-node; stopped-before-send";
                evidence.Passed = true;
                return;
            }

            var read = await FileTurnAsync(host, page, chat, fixture.Agent.Id,
                $"Call {ProjectStructureToolPolicy.ProjectStructureRead} with projectId {fixture.ProjectId:D}, " +
                $"request source InvocationSnapshot and subtreeRootIds [\"{fixture.ParentId}\"]. " +
                "Report the selected node and immediate contents. Do not mutate anything or call any other tool.");
            evidence.RecordRun("invocation-snapshot", read);
            AssertSuccessfulTool(read, ProjectStructureToolPolicy.ProjectStructureRead);
            var admission = Assert.IsType<AgentToolJournalRecord>(read.Run.ToolAdmission);
            Assert.Equal(AgentChatTrustedSourceKinds.ProjectStructure, read.Run.SourceKind);
            Assert.Equal(fixture.ProjectId.ToString("D"), read.Run.SourceId);
            Assert.Equal(originalProfile.Id, admission.Session.Profile.ProfileId);
            Assert.Equal(originalProfile.Generation, admission.Session.Profile.Generation.Value);
            var context = Assert.IsType<AgentToolAdmittedRuntimeContext>(admission.RuntimeContext);
            Assert.NotEmpty(context.Attachments);
            Assert.All(context.Attachments, item => {
                Assert.Equal(fixture.ProjectId.ToString("D"), item.Source.Id.Value);
                Assert.Equal(admission.Session.Profile.Generation, item.DatabaseProfileGeneration);
            });
            Assert.Contains(context.Attachments, item => item.Payload.PayloadJson.Contains(fixture.ParentId, StringComparison.Ordinal));
            evidence.Observations["admittedContext"] = new {
                read.Run.SourceKind, read.Run.SourceId, admission.Session.Profile,
                attachments = context.Attachments.Select(item => new { item.ScopeId, item.Source, item.DatabaseProfileGeneration,
                    item.ContentFingerprint, item.PublicationRevision, item.CoverageFingerprint })
            };

            var nonceRead = await FileTurnAsync(host, page, chat, fixture.Agent.Id,
                $"For the existing asset {fixture.SeedAssetId} in project {fixture.ProjectId:D}, call both " +
                $"{ProjectStructureToolPolicy.ProjectStructureAssetGet} and {ProjectStructureToolPolicy.ProjectStructureAssetContentGet}. " +
                "Issue both read calls together in one tool batch. Read the content and return its nonce exactly. Do not write or infer it from the title.");
            evidence.RecordRun("existing-asset-read", nonceRead);
            AssertSuccessfulTool(nonceRead, ProjectStructureToolPolicy.ProjectStructureAssetGet);
            AssertSuccessfulTool(nonceRead, ProjectStructureToolPolicy.ProjectStructureAssetContentGet);
            Assert.Contains(fixture.SeedNonce, nonceRead.Run.ResultSummary, StringComparison.Ordinal);
            Assert.Equal(fixture.SeedContent, await ReadFileContentAsync(host, fixture.ProjectId, fixture.SeedAssetId));

            var createdNonce = "created-" + Guid.NewGuid().ToString("D");
            var title = "Live agent file " + Guid.NewGuid().ToString("N")[..8];
            var relativePath = $"live-proof/{Guid.NewGuid():N}/roundtrip.md";
            var bytes = $"# Live proof\n{createdNonce}\n\n| Item | Value |\n| --- | --- |\n| Safe | 7 |\n";
            var created = await FileTurnAsync(host, page, chat, fixture.Agent.Id,
                $"Create exactly one UTF-8 file using {ToolContractCatalog.WorkspaceWriteFile} at relative path " +
                $"{relativePath}, overwrite false, with content equal to this JSON string: {JsonSerializer.Serialize(bytes)}. " +
                $"Then attach that existing file using {ProjectStructureToolPolicy.ProjectStructureAssetCreate}: top-level projectId " +
                $"{fixture.ProjectId:D}, nested request objectType File, title {JsonSerializer.Serialize(title)}, parentNodeKey " +
                $"{JsonSerializer.Serialize(fixture.ParentId)}, sourceWorkspacePath {JsonSerializer.Serialize(relativePath)}, " +
                "sourceFileName roundtrip.md, sourceContentType text/markdown. Preserve the returned node ID. " +
                $"Finally call both {ProjectStructureToolPolicy.ProjectStructureAssetGet} and " +
                $"{ProjectStructureToolPolicy.ProjectStructureAssetContentGet} for that node together in one tool batch. No other writes or tools.",
                approveFileMutations: true);
            evidence.RecordRun("write-attach-two-readbacks", created);
            foreach (var tool in new[] { ToolContractCatalog.WorkspaceWriteFile, ProjectStructureToolPolicy.ProjectStructureAssetCreate,
                ProjectStructureToolPolicy.ProjectStructureAssetGet, ProjectStructureToolPolicy.ProjectStructureAssetContentGet }) {
                AssertSuccessfulTool(created, tool);
            }
            var structure = await host.SeedAsync(services => services.GetRequiredService<ProjectStructureAgentService>()
                .GetStructureAsync(fixture.ProjectId, new(IncludeAssets: true, IncludeNotes: true)));
            var node = Assert.Single(structure.Nodes, item => item.Title == title);
            Assert.Equal(fixture.ParentId, node.ParentId);
            Assert.Equal(ProjectObjectType.File, node.ObjectType);
            Assert.Equal(bytes, await ReadFileContentAsync(host, fixture.ProjectId, node.Id));
            evidence.Targets["createdAssetNode"] = node.Id;
            evidence.Observations["content"] = new { node.Id, node.ParentId, node.ArtifactId, node.MediaRelativePath,
                relativeWorkspacePath = relativePath, byteLength = Encoding.UTF8.GetByteCount(bytes),
                sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(bytes))) };
            var sibling = await host.SeedAsync(services => services.GetRequiredService<ProjectStructureAgentService>()
                .GetStructureAsync(fixture.SiblingId, new(IncludeAssets: true)));
            Assert.DoesNotContain(sibling.Nodes, item => item.Title == title);
            Assert.Equal(fixture.SiblingContent, await ReadFileContentAsync(host, fixture.SiblingId, fixture.SiblingAssetId));
            var expand = chat.GetByTestId("chat-execution-summary").Or(chat.GetByTestId("chat-execution-history"));
            await expand.ClickAsync();
            await Assertions.Expect(page.GetByTestId("agent-execution-log-dialog-body"))
                .ToContainTextAsync(ProjectStructureToolPolicy.ProjectStructureAssetContentGet);
            await page.ScreenshotAsync(new() { Path = host.Artifact("files-02-execution-log.png") });

            await oracle.NavigateAsync($"{host.BaseUrl}/projects/{fixture.ProjectId:D}/structure");
            await ReadyFileCanvasAsync(page);
            await SelectFileNodeAsync(page, node.Id, title);
            await page.GetByTestId("project-structure-selection-window").GetByRole(AriaRole.Button,
                new() { Name = "Expand preview", Exact = true }).ClickAsync();
            var preview = page.GetByRole(AriaRole.Dialog, new() { Name = "roundtrip.md file interaction", Exact = true });
            await Assertions.Expect(preview).ToContainTextAsync(createdNonce);
            await page.ScreenshotAsync(new() { Path = host.Artifact("files-03-persistent-preview.png") });
            await preview.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
            await oracle.NavigateAsync(page.Url);
            await ReadyFileCanvasAsync(page);
            await SelectFileNodeAsync(page, node.Id, title);
            Assert.Equal(bytes, await ReadFileContentAsync(host, fixture.ProjectId, node.Id));
            await oracle.AssertCleanAsync();
            evidence.Passed = true;
        } finally {
            await evidence.RecordLatestRunUnlessPassedAsync(host);
            await evidence.WriteAsync(host);
        }
    }

    [Fact]
    [Trait("Category", "LiveAgent")]
    public async Task Project_structure_sibling_scope_is_denied_and_rejected_asset_has_no_effect() {
        const string scenario = "ui-project-structure-denied-sibling-and-rejected-asset";
        if (!IsLiveValidationEnabled()) {
            await UiEvidence.WriteNotRunAsync(scenario, "The dedicated live gates are closed.");
            return;
        }
        var evidence = new UiEvidence(scenario);
        await using var host = await LiveUiHost.StartAsync();
        try {
            var fixture = await CreateFileJourneyAsync(host);
            var page = await host.NewPageAsync();
            var oracle = CrmHrBrowserOracle.Attach(page);
            await GrantFileJourneyAsync(host, oracle, page, fixture);
            host.Watch(fixture.Agent.Id);
            await evidence.DescribeProviderAsync(host, fixture.Agent);
            var chat = await OpenFileChatAsync(host, oracle, page, fixture);
            if (IsRehearsal()) {
                evidence.Execution = "rehearsal";
                evidence.Passed = true;
                return;
            }
            var denied = await FileTurnAsync(host, page, chat, fixture.Agent.Id,
                $"Try {ProjectStructureToolPolicy.ProjectStructureAssetContentGet} once for project {fixture.SiblingId:D} " +
                $"and nodeId {fixture.SiblingAssetId}. If access is denied, report denial and stop. Do not request wider access.",
                requireCompleted: false);
            evidence.RecordRun("sibling-scope-denied", denied);
            Assert.DoesNotContain(fixture.SiblingContent, denied.Run.ResultSummary ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain(denied.ToolReceipts, item => item.ToolName == ProjectStructureToolPolicy.ProjectStructureAssetContentGet &&
                item.InvocationOutcome == AgentToolInvocationOutcome.Succeeded);
            Assert.Contains(Proposals(denied), item => item.Payload.ToolName == ProjectStructureToolPolicy.ProjectStructureAssetContentGet);
            var before = await host.SeedAsync(services => services.GetRequiredService<ProjectStructureAgentService>()
                .GetStructureAsync(fixture.ProjectId, new(IncludeAssets: true)));
            var rejectedTitle = "Rejected file " + Guid.NewGuid().ToString("N")[..8];
            var rejected = await FileTurnAsync(host, page, chat, fixture.Agent.Id,
                $"Call {ProjectStructureToolPolicy.ProjectStructureAssetCreate} exactly once with top-level projectId {fixture.ProjectId:D}, " +
                $"nested request objectType File, title {JsonSerializer.Serialize(rejectedTitle)}, parentNodeKey " +
                $"{JsonSerializer.Serialize(fixture.ParentId)}, media {{\"fileName\":\"rejected.txt\",\"contentType\":\"text/plain\",\"base64Data\":\"c2FmZQ==\"}}. " +
                "Wait for the operator decision; if rejected, stop without retrying or writing any other file.",
                rejectAsset: true, requireCompleted: false);
            evidence.RecordRun("asset-approval-rejected", rejected);
            var proposal = Assert.Single(Proposals(rejected), item => item.Payload.ToolName == ProjectStructureToolPolicy.ProjectStructureAssetCreate);
            Assert.Equal(ExecutionApprovalStatus.Rejected, proposal.ApprovalStatus);
            Assert.NotEqual(AgentToolEffectState.Committed, proposal.EffectState);
            var after = await host.SeedAsync(services => services.GetRequiredService<ProjectStructureAgentService>()
                .GetStructureAsync(fixture.ProjectId, new(IncludeAssets: true)));
            Assert.Equal(before.Nodes.Select(item => item.Id).Order(), after.Nodes.Select(item => item.Id).Order());
            Assert.Equal(fixture.SiblingContent, await ReadFileContentAsync(host, fixture.SiblingId, fixture.SiblingAssetId));
            evidence.Observations["noEffect"] = new { beforeNodes = before.Nodes.Count, afterNodes = after.Nodes.Count,
                proposal.ApprovalId, proposal.ApprovalStatus, siblingContentUnchanged = true };
            await oracle.AssertCleanAsync();
            evidence.Passed = true;
        } finally {
            await evidence.RecordLatestRunUnlessPassedAsync(host);
            await evidence.WriteAsync(host);
        }
    }

    private static void AssertSuccessfulTool(ExecutionRunDetail run, string name) {
        Assert.Contains(run.ToolReceipts, receipt => receipt.ToolName == name && receipt.InvocationOutcome == AgentToolInvocationOutcome.Succeeded);
        Assert.Contains(Proposals(run), proposal => proposal.Payload.ToolName == name && proposal.State == AgentToolProposalState.Completed);
    }

    private static async Task<ExecutionRunDetail> FileTurnAsync(LiveUiHost host, IPage page, ILocator chat, Guid agentId,
        string prompt, bool approveFileMutations = false, bool rejectAsset = false, bool requireCompleted = true) {
        var previous = await host.SeedAsync(async services => (await services.GetRequiredService<IAgentFrameworkWorkspaceService>()
            .ListExecutionRunsAsync(new ExecutionRunQuery(AgentId: agentId))).Select(item => item.Id).ToHashSet());
        var decisions = new HashSet<AgentToolBusinessIntentId>();
        await SendAsync(chat, prompt);
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(ModelTurnTimeoutMilliseconds);
        while (DateTimeOffset.UtcNow < deadline) {
            var approvals = chat.Locator("[data-testid^='chat-approval-approve-']");
            if (await approvals.CountAsync() > 0) {
                var pending = await host.SeedAsync(services => ReadLatestRunAsync(services, agentId));
                var proposals = Proposals(pending).Where(item => item.ApprovalStatus == ExecutionApprovalStatus.Pending &&
                    !string.IsNullOrWhiteSpace(item.ApprovalId) && !decisions.Contains(item.IntentId)).ToArray();
                if (proposals.Length > 0) {
                    Assert.DoesNotContain(pending.Run.Id, previous);
                    var proposal = Assert.Single(proposals);
                    Assert.Contains(proposal.Payload.ToolName, new[] { ToolContractCatalog.WorkspaceWriteFile, ProjectStructureToolPolicy.ProjectStructureAssetCreate });
                    Assert.True(approveFileMutations || rejectAsset, "An unexpected approval cannot be accepted by this journey.");
                    var decision = chat.GetByTestId($"chat-approval-{(rejectAsset ? "reject" : "approve")}-{proposal.ApprovalId}");
                    if (!await decision.IsVisibleAsync() || !await decision.IsEnabledAsync()) {
                        await Task.Delay(200);
                        continue;
                    }
                    decisions.Add(proposal.IntentId);
                    if (rejectAsset) {
                        Assert.Equal(ProjectStructureToolPolicy.ProjectStructureAssetCreate, proposal.Payload.ToolName);
                        await page.ScreenshotAsync(new() { Path = host.Artifact("files-04-rejected-approval.png") });
                    }
                    await decision.ClickAsync();
                }
            }
            var runs = await host.SeedAsync(services => services.GetRequiredService<IAgentFrameworkWorkspaceService>()
                .ListExecutionRunsAsync(new ExecutionRunQuery(AgentId: agentId)));
            var current = runs.Where(item => !previous.Contains(item.Id)).OrderByDescending(item => item.CreatedAtUtc).FirstOrDefault();
            if (current is not null && current.State is ExecutionState.Completed or ExecutionState.Failed) {
                var result = await host.SeedAsync(services => services.GetRequiredService<IAgentFrameworkWorkspaceService>()
                    .GetExecutionRunDetailAsync(current.Id));
                await Assertions.Expect(chat.GetByTestId("agent-execution-activity-phase")).ToHaveTextAsync(TerminalPhase);
                if (requireCompleted) {
                    Assert.Equal(ExecutionState.Completed, result.Run.State);
                }
                return result;
            }
            await Task.Delay(200);
        }
        throw new TimeoutException("The bounded live file turn did not reach a persisted terminal state.");
    }

    private static Task<string> ReadFileContentAsync(LiveUiHost host, Guid projectId, string nodeId) => host.SeedAsync(async services => {
        var content = await services.GetRequiredService<ProjectStructureAgentService>().GetAssetContentAsync(projectId, nodeId);
        Assert.False(content.Base64DataOmitted);
        return Encoding.UTF8.GetString(Convert.FromBase64String(content.Base64Data));
    });

    private static Task<FileJourney> CreateFileJourneyAsync(LiveUiHost host) => host.SeedAsync(async services => {
        var projects = services.GetRequiredService<ProjectsService>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var projectName = "Live files " + suffix;
        var project = await projects.SaveAsync(new ProjectEditorModel { Name = projectName, Description = "Synthetic live file proof." });
        var sibling = await projects.SaveAsync(new ProjectEditorModel { Name = "Denied sibling " + suffix });
        Assert.True(project.IsSuccess);
        Assert.True(sibling.IsSuccess);
        var parent = await services.GetRequiredService<ProjectWorkbenchService>().CreateObjectAsync(project.Value,
            new(ProjectObjectType.ProjectBlock, "Selected files", "", "Synthetic file proof parent.", $"project:{project.Value:D}", X: 450, Y: 220));
        var agentService = services.GetRequiredService<ProjectStructureAgentService>();
        var setup = new ProjectStructureAgentContext(FixtureActor, FixtureActor, Environment.MachineName, "", "", suffix);
        var nonce = "seed-" + Guid.NewGuid().ToString("D");
        var seededBytes = "Existing asset nonce: " + nonce;
        var seed = await agentService.CreateAssetAsync(project.Value, new(ProjectObjectType.File, "Existing nonce asset", "", "",
            new("seed.txt", "text/plain", Convert.ToBase64String(Encoding.UTF8.GetBytes(seededBytes))), parent.Id), setup);
        var canary = "sibling-" + Guid.NewGuid().ToString("D");
        var siblingAsset = await agentService.CreateAssetAsync(sibling.Value, new(ProjectObjectType.File, "Private sibling asset", "", "",
            new("canary.txt", "text/plain", Convert.ToBase64String(Encoding.UTF8.GetBytes(canary))), $"project:{sibling.Value:D}"), setup);
        var providerAgent = await SelectOrdinaryPlannerAsync(services);
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var agentId = await workspace.SaveAgentAsync(new AgentEditorModel {
            Name = "Scoped file agent " + suffix, Status = AgentLifecycleStatus.Active, ProviderProfileId = providerAgent.ProviderProfileId,
            Model = providerAgent.Model, Instructions = "Use only the exact requested tools and scoped project. Respect approvals and denials. Never invent tool results.",
            WorkspaceToolAccess = new() { Profile = AgentWorkspaceToolProfileKind.Custom }
        });
        return new FileJourney(project.Value, projectName, parent.Id, seed.Id, nonce, seededBytes, sibling.Value,
            siblingAsset.Id, canary, await FindAgentAsync(services, agentId));
    });

    private static async Task GrantFileJourneyAsync(LiveUiHost host, CrmHrBrowserOracle oracle, IPage page, FileJourney fixture) {
        await oracle.NavigateAsync($"{host.BaseUrl}/agents?tab=agents&agentId={fixture.Agent.Id:D}");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.WaitForFunctionAsync("() => typeof databaseSwitchStorageListener === 'function'");
        var dialog = page.GetByTestId("agents-details-dialog").Last;
        await Assertions.Expect(dialog.GetByTestId("agents-catalog-name")).ToHaveValueAsync(fixture.Agent.Name, new() { Timeout = 60_000 });
        await dialog.GetByRole(AriaRole.Tab, new() { Name = "Project Structure Access", Exact = true }).ClickAsync();
        await dialog.GetByTestId("agents-catalog-project-structure-read").CheckAsync();
        await dialog.GetByTestId("agents-catalog-project-structure-non-task-write").CheckAsync();
        await Assertions.Expect(dialog.GetByTestId("agents-catalog-project-structure-all")).Not.ToBeCheckedAsync();
        await dialog.GetByTestId("agents-catalog-project-structure-projects").GetByLabel(fixture.ProjectName, new() { Exact = true }).CheckAsync();
        await dialog.GetByRole(AriaRole.Tab, new() { Name = "Workspace Tools", Exact = true }).ClickAsync();
        await dialog.GetByTestId("agents-catalog-workspace-profile").SelectOptionAsync(nameof(AgentWorkspaceToolProfileKind.Custom));
        await dialog.GetByTestId("agents-catalog-workspace-read").CheckAsync();
        await dialog.GetByTestId("agents-catalog-workspace-write").CheckAsync();
        await dialog.GetByTestId("agents-catalog-save").ClickAsync();
        await page.GetByText("Agent saved", new() { Exact = true }).First.WaitForAsync(new() { Timeout = 60_000 });
        var saved = await host.SeedAsync(services => services.GetRequiredService<IAgentFrameworkWorkspaceService>().GetAgentEditorAsync(fixture.Agent.Id));
        Assert.Equal([fixture.ProjectId], saved.ProjectStructureAccess.AllowedProjectIds);
        Assert.False(saved.ProjectStructureAccess.AllowAllProjects);
        Assert.False(saved.ProjectStructureAccess.CanWrite);
        Assert.True(saved.ProjectStructureAccess.CanRead);
        Assert.True(saved.ProjectStructureAccess.CanWriteNonTaskStructure);
        Assert.True(saved.WorkspaceToolAccess.CanWriteFiles);
        Assert.False(saved.WorkspaceToolAccess.CanWriteStorage);
        Assert.Empty(saved.SelectedCapabilityIds);
        Assert.Empty(saved.AllowedSecretReferences);
    }

    private static async Task<ILocator> OpenFileChatAsync(LiveUiHost host, CrmHrBrowserOracle oracle, IPage page, FileJourney fixture) {
        await oracle.NavigateAsync($"{host.BaseUrl}/projects/{fixture.ProjectId:D}/structure");
        await ReadyFileCanvasAsync(page);
        await SelectFileNodeAsync(page, fixture.ParentId, "Selected files");
        return await OpenFloatingChatFromCatalogAsync(page, "project-structure-agents-toggle", fixture.Agent.Id,
            host.Artifact("files-catalog-failure.png"));
    }

    private static async Task ReadyFileCanvasAsync(IPage page) {
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.GetByTestId("project-structure-canvas-loaded").WaitForAsync(new() { Timeout = 60_000, State = WaitForSelectorState.Attached });
        await page.WaitForFunctionAsync("() => Array.from(document.querySelectorAll('.cw-canvas-host')).some(host => !!host.__canvasWorkbenchState)");
    }

    private static async Task SelectFileNodeAsync(IPage page, string nodeId, string title) {
        var window = page.GetByTestId("project-structure-object-index-window");
        if (!await window.IsVisibleAsync()) {
            await page.GetByTestId("project-structure-object-index-toggle").ClickAsync();
        }
        if (await window.EvaluateAsync<bool>("node => node.classList.contains('is-minimized')")) {
            await window.GetByRole(AriaRole.Button, new() { Name = "Expand window" }).ClickAsync();
        }
        var outlineId = "project-structure-outline-node-" + new string(nodeId.Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-').ToArray());
        await page.GetByTestId(outlineId).ClickAsync();
        var selection = page.GetByTestId("project-structure-selection-window");
        await Assertions.Expect(selection).ToContainTextAsync(title);
        if (await selection.EvaluateAsync<bool>("node => node.classList.contains('is-minimized')")) {
            await selection.GetByRole(AriaRole.Button, new() { Name = "Expand window" }).ClickAsync();
        }
    }

    private sealed record FileJourney(Guid ProjectId, string ProjectName, string ParentId, string SeedAssetId, string SeedNonce,
        string SeedContent, Guid SiblingId, string SiblingAssetId, string SiblingContent, AgentDefinition Agent);
}
