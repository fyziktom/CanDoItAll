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
using static CanDoItAll.Tests.Playwright.Smoke.ProjectFilesUiJourney;

namespace CanDoItAll.Tests.Playwright.Smoke;

public sealed class ProjectFilesAgentUiTests {
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
                expected: new(fixture.ProjectId, fixture.ParentId, title, bytes, "roundtrip.md", "text/markdown", relativePath));
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
                expected: new(fixture.ProjectId, fixture.ParentId, rejectedTitle, "safe", "rejected.txt", "text/plain"),
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

}
