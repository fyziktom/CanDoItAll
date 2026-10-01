using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.ProjectStructure;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

using static CanDoItAll.Tests.Playwright.Smoke.AgentUiJourneySupport;

namespace CanDoItAll.Tests.Playwright.Smoke;

internal static class ProjectFilesUiJourney {
    internal static void AssertSuccessfulTool(ExecutionRunDetail run, string name) {
        Assert.Contains(run.ToolReceipts, receipt => receipt.ToolName == name && receipt.InvocationOutcome == AgentToolInvocationOutcome.Succeeded);
        Assert.Contains(Proposals(run), proposal => proposal.Payload.ToolName == name && proposal.State == AgentToolProposalState.Completed);
    }

    internal static async Task<ExecutionRunDetail> FileTurnAsync(LiveUiHost host, IPage page, ILocator chat, Guid agentId,
        string prompt, FileApprovalIntent? expected = null, bool rejectAsset = false, bool requireCompleted = true, CancellationToken cancellationToken = default) {
        var previous = await host.SeedAsync(async services => (await services.GetRequiredService<IAgentFrameworkWorkspaceService>()
            .ListExecutionRunsAsync(new ExecutionRunQuery(AgentId: agentId))).Select(item => item.Id).ToHashSet());
        var decisions = new HashSet<AgentToolBusinessIntentId>();
        var acceptedTools = new HashSet<string>(StringComparer.Ordinal);
        var originalProfile = await host.SeedAsync(services => {
            var canonical = services.GetRequiredService<ICanonicalRuntimeDatabase>();
            return Task.FromResult(new AgentToolProfileBinding(canonical.Profile.Profile.Id,
                canonical.Profile.Profile.Runtime.Fingerprint, new(canonical.Generation)));
        });
        Guid? currentRunId = null;
        await SendAsync(chat, prompt);
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(ModelTurnTimeoutMilliseconds);
        while (DateTimeOffset.UtcNow < deadline) {
            cancellationToken.ThrowIfCancellationRequested();
            var approvals = chat.Locator("[data-testid^='chat-approval-approve-']");
            if (await approvals.CountAsync() > 0) {
                var pending = await host.SeedAsync(services => ReadLatestRunAsync(services, agentId));
                var proposals = Proposals(pending).Where(item => item.ApprovalStatus == ExecutionApprovalStatus.Pending &&
                    !string.IsNullOrWhiteSpace(item.ApprovalId) && !decisions.Contains(item.IntentId)).ToArray();
                if (proposals.Length > 0) {
                    Assert.DoesNotContain(pending.Run.Id, previous);
                    var proposal = Assert.Single(proposals);
                    currentRunId ??= pending.Run.Id;
                    var refusal = expected?.Validate(proposal.Payload, acceptedTools) ?? FileProposalRefusal.UnexpectedTool;
                    if (refusal == FileProposalRefusal.None && !await MatchesFileAdmissionAsync(host, pending, agentId, currentRunId.Value, originalProfile, expected!)) {
                        refusal = FileProposalRefusal.WrongTarget;
                    }
                    var reject = rejectAsset || refusal != FileProposalRefusal.None;
                    var decision = chat.GetByTestId($"chat-approval-{(reject ? "reject" : "approve")}-{proposal.ApprovalId}");
                    if (!await decision.IsVisibleAsync() || !await decision.IsEnabledAsync()) {
                        await Task.Delay(200, cancellationToken);
                        continue;
                    }
                    decisions.Add(proposal.IntentId);
                    if (rejectAsset) {
                        Assert.Equal(ProjectStructureToolPolicy.ProjectStructureAssetCreate, proposal.Payload.ToolName);
                        await page.ScreenshotAsync(new() { Path = host.Artifact("files-04-rejected-approval.png") });
                    }
                    await decision.ClickAsync();
                    if (refusal != FileProposalRefusal.None) {
                        throw new InvalidOperationException($"File proposal rejected by the fixture: {refusal}. Run={pending.Run.Id}; Approval={proposal.ApprovalId}.");
                    }
                    if (!reject) {
                        acceptedTools.Add(proposal.Payload.ToolName);
                    }
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
                if (expected is not null && !rejectAsset) {
                    foreach (var tool in acceptedTools) {
                        Assert.Single(Proposals(result), item => item.Payload.ToolName == tool && item.EffectState == AgentToolEffectState.Committed);
                        Assert.Single(result.ToolReceipts, item => item.ToolName == tool && item.InvocationOutcome == AgentToolInvocationOutcome.Succeeded);
                    }
                }
                return result;
            }
            await Task.Delay(200, cancellationToken);
        }
        await host.StopAsync(LiveUiStopReason.Timeout);
        throw new TimeoutException("The bounded live file turn did not reach a persisted terminal state.");
    }

    internal static Task<bool> MatchesFileAdmissionAsync(LiveUiHost host, ExecutionRunDetail run, Guid agentId,
        Guid runId, AgentToolProfileBinding profile, FileApprovalIntent expected) => host.SeedAsync(services => {
        if (run.Run.Id != runId || run.Run.SourceKind != AgentChatTrustedSourceKinds.ProjectStructure ||
            run.Run.SourceId != expected.ProjectId.ToString("D") || run.Run.ToolAdmission is not { } admission ||
            admission.Session.AgentId != agentId || admission.Session.Profile != profile ||
            admission.Session.Reference.ExecutionRunId != runId || admission.RuntimeContext is not { } saved) {
            return Task.FromResult(false);
        }
        var restored = new AgentChatContextAttachmentPersistence(services.GetServices<IAgentChatContextAttachmentCodec>()).Restore(saved);
        var snapshots = restored.Attachments.Select(attachment =>
            attachment.TryGetAttachment<ProjectStructureInvocationSnapshot>(out var snapshot) ? snapshot : null)
            .OfType<ProjectStructureInvocationSnapshot>().ToArray();
        return Task.FromResult(snapshots.Length == 1 && snapshots[0].ProjectId == expected.ProjectId &&
            snapshots[0].SelectedNodeIds.SequenceEqual([expected.ParentId]) && saved.Attachments.All(attachment =>
                attachment.Source.Id.Value == expected.ProjectId.ToString("D") && attachment.DatabaseProfileGeneration == profile.Generation));
    });

    internal static Task<string> ReadFileContentAsync(LiveUiHost host, Guid projectId, string nodeId) => host.SeedAsync(async services => {
        var content = await services.GetRequiredService<ProjectStructureAgentService>().GetAssetContentAsync(projectId, nodeId);
        Assert.False(content.Base64DataOmitted);
        return Encoding.UTF8.GetString(Convert.FromBase64String(content.Base64Data));
    });

    internal static Task<FileJourney> CreateFileJourneyAsync(LiveUiHost host, Guid? existingProjectId = null) => host.SeedAsync(async services => {
        var projects = services.GetRequiredService<ProjectsService>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        Guid projectId;
        string projectName;
        if (existingProjectId is { } acquiredId) {
            var acquired = await projects.GetAsync(acquiredId);
            Assert.Equal(acquiredId, acquired.Id);
            projectId = acquiredId;
            projectName = acquired.Name;
        } else {
            projectName = "Live files " + suffix;
            var project = await projects.SaveAsync(new ProjectEditorModel { Name = projectName, Description = "Synthetic live file proof." });
            Assert.True(project.IsSuccess);
            projectId = project.Value;
        }
        var sibling = await projects.SaveAsync(new ProjectEditorModel { Name = "Denied sibling " + suffix });
        Assert.True(sibling.IsSuccess);
        var parent = await services.GetRequiredService<ProjectWorkbenchService>().CreateObjectAsync(projectId,
            new(ProjectObjectType.ProjectBlock, "Selected files", "", "Synthetic file proof parent.", $"project:{projectId:D}", X: 450, Y: 220));
        var agentService = services.GetRequiredService<ProjectStructureAgentService>();
        var setup = new ProjectStructureAgentContext(FixtureActor, FixtureActor, Environment.MachineName, "", "", suffix);
        var nonce = "seed-" + Guid.NewGuid().ToString("D");
        var seededBytes = "Existing asset nonce: " + nonce;
        var seed = await agentService.CreateAssetAsync(projectId, new(ProjectObjectType.File, "Existing nonce asset", "", "",
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
        return new FileJourney(projectId, projectName, parent.Id, seed.Id, nonce, seededBytes, sibling.Value,
            siblingAsset.Id, canary, await FindAgentAsync(services, agentId));
    });

    internal static async Task GrantFileJourneyAsync(LiveUiHost host, CrmHrBrowserOracle oracle, IPage page, FileJourney fixture) {
        await oracle.NavigateAsync($"{host.BaseUrl}/agents?tab=agents&agentId={fixture.Agent.Id:D}");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.WaitForFunctionAsync("() => typeof databaseSwitchListeners !== 'undefined' && databaseSwitchListeners.size === 1");
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

    internal static async Task<ILocator> OpenFileChatAsync(LiveUiHost host, CrmHrBrowserOracle oracle, IPage page, FileJourney fixture) {
        await oracle.NavigateAsync($"{host.BaseUrl}/projects/{fixture.ProjectId:D}/structure");
        await ReadyFileCanvasAsync(page);
        await SelectFileNodeAsync(page, fixture.ParentId, "Selected files");
        return await OpenFloatingChatFromCatalogAsync(page, "project-structure-agents-toggle", fixture.Agent.Id,
            host.Artifact("files-catalog-failure.png"));
    }

    internal static async Task ReadyFileCanvasAsync(IPage page) {
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.GetByTestId("project-structure-canvas-loaded").WaitForAsync(new() { Timeout = 60_000, State = WaitForSelectorState.Attached });
        await page.WaitForFunctionAsync("() => Array.from(document.querySelectorAll('.cw-canvas-host')).some(host => !!host.__canvasWorkbenchState)");
    }

    internal static async Task SelectFileNodeAsync(IPage page, string nodeId, string title) {
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

    internal sealed record FileJourney(Guid ProjectId, string ProjectName, string ParentId, string SeedAssetId, string SeedNonce,
        string SeedContent, Guid SiblingId, string SiblingAssetId, string SiblingContent, AgentDefinition Agent);
}
