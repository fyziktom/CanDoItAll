using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Playwright;
using static CanDoItAll.Tests.Playwright.Smoke.ProjectFilesUiJourney;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class SharedProviderNativeConsumerTests {
    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public Task Final_image_WB5_native_operators_secret_scope_and_agent_file_approvals_preserve_exact_effects() =>
        RunFileConsumerAsync(planning: false, operators: true);

    private sealed record OperatorResume(Guid ProjectId, Guid SiblingId, string ProjectName, string ParentId, string Address,
        Dictionary<ProjectPartyQuickCreateKind, string> Nodes, Dictionary<ProjectPartyQuickCreateKind, ProjectPartyAssignmentDetail> Assignments,
        bool PartiesComplete = false, string? MeetingId = null, ProjectPartyAssignmentDetail[]? MeetingAssignments = null,
        OperatorSecretReceipt[]? Secrets = null, string? ReusedSecretNodeId = null, OperatorFileSetup? Files = null);

    private sealed record OperatorSecretReceipt(string Id, Guid SecretId, string Sha256, string Suffix);
    private sealed record OperatorFileSetup(AgentEditorModel Agent, string SeedId, string SeedSha256, string SiblingId, string SiblingSha256);

    private static async Task<OperatorResume?> ReadOperatorResumeAsync(SharedProviderConsumerFixture fixture) {
        var path = Environment.GetEnvironmentVariable("CANDOITALL_WB5_OPERATORS_RESUME");
        if (string.IsNullOrWhiteSpace(path)) {
            return null;
        }
        var resume = JsonSerializer.Deserialize<OperatorResume>(await File.ReadAllTextAsync(path), SharedProviderConsumerFixture.ReadJson)!;
        Assert.Equal(fixture.Address, resume.Address);
        Assert.Matches("^PP2C files PP2C_FILES_[a-f0-9]{32}$", resume.ProjectName);
        var project = await fixture.Api.GetFromJsonAsync<ProjectEditorModel>($"api/projects/{resume.ProjectId:D}", SharedProviderConsumerFixture.Json);
        Assert.Equal(resume.ProjectId, project!.Id);
        Assert.Equal(resume.ProjectName, project.Name);
        var sibling = await fixture.Api.GetFromJsonAsync<ProjectEditorModel>($"api/projects/{resume.SiblingId:D}", SharedProviderConsumerFixture.Json);
        Assert.Equal(resume.SiblingId, sibling!.Id);
        Assert.Equal("PP2C untouched " + resume.ProjectName["PP2C files ".Length..], sibling.Name);
        var tree = await TreeAsync(fixture, resume.ProjectId, includeMetadata: true);
        Assert.Equal(resume.Nodes.Count + (resume.PartiesComplete ? 3 : 2) + (resume.Secrets?.Length ?? 0) +
            (resume.ReusedSecretNodeId is null ? 0 : 1) + (resume.Files is null ? 0 : 1), tree.Nodes.Count);
        Assert.Contains(tree.Nodes, node => node.Id == resume.ParentId && node.ObjectType == ProjectObjectType.ProjectBlock);
        foreach (var (kind, id) in resume.Nodes) {
            var node = Assert.Single(tree.Nodes, node => node.Id == id);
            Assert.Equal(ProjectObjectType.Participant, node.ObjectType);
            Assert.Equal($"WB5 {kind} {resume.ProjectName["PP2C files ".Length..]}", node.Title);
        }
        if (resume.PartiesComplete) {
            Assert.Contains(tree.Nodes, node => node.Id == resume.MeetingId && node.ObjectType == ProjectObjectType.Meeting);
        }
        var assignments = await OperatorAssignmentsAsync(fixture, resume.ProjectId);
        var expected = resume.Assignments.Values.Concat(resume.MeetingAssignments ?? []).ToArray();
        Assert.Equal(expected.Length, assignments.Length);
        foreach (var accepted in expected) {
            Assert.Equal(JsonSerializer.Serialize(accepted), JsonSerializer.Serialize(Assert.Single(assignments, item => item.Id == accepted.Id)));
        }
        if (resume.Secrets is not null) {
            Assert.InRange(resume.Secrets.Length, 1, 2);
            foreach (var secret in resume.Secrets) {
                var node = Assert.Single(tree.Nodes, node => node.Id == secret.Id);
                Assert.Equal(secret.SecretId, ProjectObjectMetadataSerializer.Parse(node.MetadataJson).SecretReference!.SecretId);
            }
            if (resume.ReusedSecretNodeId is not null) {
                var reused = Assert.Single(tree.Nodes, node => node.Id == resume.ReusedSecretNodeId);
                Assert.Equal(Assert.Single(resume.Secrets, secret => secret.Suffix == "intended").SecretId,
                    ProjectObjectMetadataSerializer.Parse(reused.MetadataJson).SecretReference!.SecretId);
            }
            await fixture.EvidenceAsync("wb5-native-secret-references", resume.Secrets);
        }
        if (resume.Files is not null) {
            var agent = await fixture.Api.GetFromJsonAsync<AgentEditorModel>($"api/agents/{resume.Files.Agent.Id:D}", SharedProviderConsumerFixture.Json);
            Assert.Equal(JsonSerializer.Serialize(resume.Files.Agent), JsonSerializer.Serialize(agent));
            Assert.Empty(agent!.AllowedSecretReferences);
            Assert.Equal(resume.Files.SeedSha256, SharedProviderConsumerFixture.Hash(await ContentAsync(fixture, resume.ProjectId, resume.Files.SeedId)));
            Assert.Equal(resume.Files.SiblingSha256, SharedProviderConsumerFixture.Hash(await ContentAsync(fixture, resume.SiblingId, resume.Files.SiblingId)));
        }
        await fixture.EvidenceAsync("wb5-resumed-native-context", new { resume, tree, assignments });
        return resume;
    }

    private static async Task ConfigureOperatorsAsync(SharedProviderConsumerFixture fixture, Guid projectId, string marker, OperatorResume? resume) {
        if (resume?.PartiesComplete is true) {
            if (resume.ReusedSecretNodeId is null) {
                await CreateOperatorSecretsAsync(fixture, projectId, marker, resume.Secrets);
            }
            return;
        }
        var page = fixture.Page;
        var original = await TreeAsync(fixture, projectId, includeMetadata: true);
        var agentsBefore = (await fixture.GetAsync("api/agents")).EnumerateArray().Select(agent => agent.GetProperty("id").GetGuid()).Order().ToArray();
        if (resume is not null) {
            await fixture.NavigateAsync($"/projects/{projectId:D}/structure");
            await ReadyFileCanvasAsync(page);
        }
        var created = new List<(ProjectStructureNodeSummary Node, Guid PartyId, string Name)>();
        foreach (var kind in Enum.GetValues<ProjectPartyQuickCreateKind>()) {
            var name = $"WB5 {kind} {marker}";
            var node = resume?.Nodes.TryGetValue(kind, out var acceptedNodeId) is true
                ? Assert.Single(original.Nodes, node => node.Id == acceptedNodeId)
                : await CreateOperatorNodeAsync(fixture, projectId, "people", "add-participant-freelancer", name, meeting: false);
            if (resume?.Assignments.TryGetValue(kind, out var accepted) is true) {
                var observed = await fixture.GetAsync($"api/crm-hr/parties/{accepted.PartyId:D}");
                Assert.Equal(accepted.PartyId, observed.GetProperty("id").GetGuid());
                Assert.Equal(node.Id, accepted.NodeKey);
                created.Add((node, accepted.PartyId, name));
                await fixture.EvidenceAsync("wb5-resumed-" + kind, new { accepted, observed });
                continue;
            }
            await SelectFileNodeAsync(page, node.Id, node.Title);
            var editor = page.GetByTestId("project-structure-party-editor");
            var local = editor.GetByTestId("project-structure-participant-local-only");
            await Assertions.Expect(local).ToBeCheckedAsync();
            await local.UncheckAsync();
            await editor.GetByTestId("project-structure-participant-quick-kind").SelectOptionAsync(kind.ToString());
            await editor.GetByTestId("project-structure-participant-quick-name").FillAsync(name);
            await editor.GetByTestId("project-structure-participant-quick-email").FillAsync($"{kind}@example.invalid");
            await editor.GetByTestId("project-structure-participant-quick-create").ClickAsync();
            await Assertions.Expect(editor.GetByTestId("project-structure-party-editor-message")).ToContainTextAsync("created. Save participant sync");
            var message = await editor.InnerTextAsync();
            var partyId = Guid.Parse(Regex.Match(message, @"Accepted directory party ([0-9a-f-]{36})", RegexOptions.CultureInvariant).Groups[1].Value);
            var directory = await fixture.GetAsync($"api/crm-hr/parties/{partyId:D}");
            Assert.Equal(partyId, directory.GetProperty("id").GetGuid());
            Assert.DoesNotContain((await OperatorAssignmentsAsync(fixture, projectId)), assignment => assignment.NodeKey == node.Id);
            await fixture.EvidenceAsync("wb5-created-" + kind, new { projectId, NodeId = node.Id, PartyId = partyId, Directory = directory, AssignmentNotYetSaved = true });
            await editor.GetByTestId("project-structure-participant-save").ClickAsync();
            await Assertions.Expect(editor.GetByTestId("operator-party-receipt")).ToContainTextAsync("saved and observed");
            var assignment = Assert.Single(await OperatorAssignmentsAsync(fixture, projectId), assignment => assignment.NodeKey == node.Id);
            Assert.Equal(partyId, assignment.PartyId);
            Assert.True(assignment.IsPrimary);
            Assert.Equal(ProjectPartyAssignmentRole.TeamMember, assignment.Role);
            created.Add((node, partyId, name));
            await fixture.EvidenceAsync("wb5-linked-" + kind, assignment);
        }
        Assert.Equal(agentsBefore, (await fixture.GetAsync("api/agents")).EnumerateArray().Select(agent => agent.GetProperty("id").GetGuid()).Order().ToArray());
        var first = created[0];
        await SelectFileNodeAsync(page, first.Node.Id, first.Name);
        var participant = page.GetByTestId("project-structure-party-editor");
        await participant.GetByTestId("project-structure-participant-local-only").CheckAsync();
        await participant.GetByTestId("project-structure-participant-save").ClickAsync();
        await Assertions.Expect(participant.GetByTestId("project-structure-party-editor-message")).ToContainTextAsync("project-local only");
        Assert.DoesNotContain(await OperatorAssignmentsAsync(fixture, projectId), assignment => assignment.NodeKey == first.Node.Id);
        await participant.GetByTestId("project-structure-participant-local-only").UncheckAsync();
        await participant.GetByTestId($"project-structure-participant-party-option-{first.PartyId:N}").ClickAsync();
        await participant.GetByTestId("project-structure-participant-save").ClickAsync();
        await Assertions.Expect(participant.GetByTestId("project-structure-party-editor-message")).ToContainTextAsync("linked to the directory");
        Assert.Equal(first.PartyId, Assert.Single(await OperatorAssignmentsAsync(fixture, projectId), assignment => assignment.NodeKey == first.Node.Id).PartyId);
        var meeting = await CreateOperatorNodeAsync(fixture, projectId, "meetings", "add-meeting-online", "WB5 meeting " + marker, meeting: true);
        await SelectFileNodeAsync(page, meeting.Id, meeting.Title);
        var meetingEditor = page.GetByTestId("project-structure-party-editor");
        var beforeDefaults = await OperatorAssignmentsAsync(fixture, projectId);
        await meetingEditor.GetByTestId("project-structure-meeting-project-defaults").ClickAsync();
        Assert.Equal(JsonSerializer.Serialize(beforeDefaults), JsonSerializer.Serialize(await OperatorAssignmentsAsync(fixture, projectId)));
        foreach (var party in created.Take(2)) {
            await meetingEditor.GetByTestId("project-structure-meeting-party-option").Filter(new() { HasText = party.Name })
                .GetByTestId("project-structure-meeting-party-checkbox").CheckAsync();
        }
        await meetingEditor.GetByTestId("project-structure-meeting-save").ClickAsync();
        await Assertions.Expect(meetingEditor.GetByTestId("operator-party-receipt")).ToContainTextAsync("saved and observed");
        var meetings = (await OperatorAssignmentsAsync(fixture, projectId)).Where(assignment => assignment.NodeKey == meeting.Id).ToArray();
        Assert.Equal(created.Take(2).Select(party => party.PartyId).Order(), meetings.Select(assignment => assignment.PartyId).Order());
        Assert.All(meetings, assignment => Assert.Equal(ProjectPartyAssignmentRole.MeetingParticipant, assignment.Role));
        await fixture.ScreenshotAsync("wb5-native-meeting");
        await fixture.EvidenceAsync("wb5-native-assignments", await fixture.ReadOperatorsAsync(projectId));
        var after = await TreeAsync(fixture, projectId, includeMetadata: true);
        foreach (var neighbor in original.Nodes.Where(node => resume?.Nodes.ContainsValue(node.Id) is not true)) {
            Assert.Equal(JsonSerializer.Serialize(neighbor), JsonSerializer.Serialize(Assert.Single(after.Nodes, node => node.Id == neighbor.Id)));
        }
        await CreateOperatorSecretsAsync(fixture, projectId, marker);
    }

    private static async Task<ProjectStructureNodeSummary> CreateOperatorNodeAsync(SharedProviderConsumerFixture fixture,
        Guid projectId, string group, string action, string name, bool meeting) {
        await OpenOperatorToolboxAsync(fixture, projectId, group, action);
        var composer = fixture.Page.Locator(".cw-canvas-composer");
        await composer.WaitForAsync();
        await composer.Locator(".cw-canvas-composer__input").Nth(0).FillAsync(name);
        if (meeting) {
            await composer.Locator("input[type='datetime-local']").First.FillAsync(DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-ddTHH:mm"));
            await composer.Locator("select").First.SelectOptionAsync("msTeams");
        }
        await composer.GetByRole(AriaRole.Button, new() { Name = meeting ? "Schedule meeting" : "Add participant", Exact = true }).ClickAsync();
        await Assertions.Expect(composer).Not.ToBeVisibleAsync();
        await Assertions.Expect(fixture.Page.GetByTestId("project-structure-selection-window")).ToContainTextAsync(name + " was created (");
        return Assert.Single((await TreeAsync(fixture, projectId, includeMetadata: true)).Nodes, node => node.Title == name);
    }

    private static async Task OpenOperatorToolboxAsync(SharedProviderConsumerFixture fixture, Guid projectId, string group, string action) {
        await fixture.NavigateAsync($"/projects/{projectId:D}/structure");
        await ReadyFileCanvasAsync(fixture.Page);
        var root = Assert.Single((await TreeAsync(fixture, projectId)).Nodes, node => node.Id == $"project:{projectId:D}");
        await SelectContentNodeAsync(fixture.Page, root.Id, root.Title);
        var toolbox = fixture.Page.GetByTestId("project-structure-standard-blocks-toolbox");
        if (!await toolbox.IsVisibleAsync()) {
            await fixture.Page.GetByTestId("project-structure-toolbox-toggle").ClickAsync();
        }
        if (await toolbox.GetByTestId("project-structure-toolbox-" + action).CountAsync() == 0) {
            await toolbox.GetByTestId("project-structure-toolbox-group-" + group).ClickAsync();
        }
        await toolbox.GetByTestId("project-structure-toolbox-" + action).ClickAsync();
    }

    private static async Task<ProjectPartyAssignmentDetail[]> OperatorAssignmentsAsync(SharedProviderConsumerFixture fixture, Guid projectId) =>
        (await fixture.ReadOperatorsAsync(projectId)).GetProperty("assignments").Deserialize<ProjectPartyAssignmentDetail[]>(SharedProviderConsumerFixture.ReadJson)!;

    private static async Task CreateOperatorSecretsAsync(SharedProviderConsumerFixture fixture, Guid projectId, string marker,
        OperatorSecretReceipt[]? retained = null) {
        var accepted = new List<OperatorSecretReceipt>(retained ?? []);
        Guid? first = accepted.SingleOrDefault(item => item.Suffix == "intended")?.SecretId;
        foreach (var suffix in new[] { "intended", "neighbor" }) {
            if (accepted.Any(item => item.Suffix == suffix)) {
                continue;
            }
            await OpenOperatorToolboxAsync(fixture, projectId, "assets", "add-secret-reference");
            var dialog = fixture.Page.GetByTestId("project-structure-secret-dialog");
            var value = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            await dialog.GetByTestId("project-structure-secret-create-name").FillAsync($"WB5 {suffix} {marker}");
            var field = dialog.GetByTestId("project-structure-secret-create-value");
            await field.FillAsync(value);
            await fixture.Page.GetByTestId("project-structure-secret-create-use").ClickAsync();
            await Assertions.Expect(dialog.GetByTestId("project-structure-secret-receipt")).ToContainTextAsync("saved and observed");
            var node = Assert.Single((await TreeAsync(fixture, projectId, includeMetadata: true)).Nodes, node => node.Title == $"WB5 {suffix} {marker}");
            var id = Assert.IsType<Guid>(ProjectObjectMetadataSerializer.Parse(node.MetadataJson).SecretReference!.SecretId);
            first ??= id;
            Assert.False((await fixture.Page.Locator("body").InnerTextAsync()).Contains(value, StringComparison.Ordinal));
            Assert.False((await fixture.Page.ContentAsync()).Contains(value, StringComparison.Ordinal));
            Assert.True(string.IsNullOrEmpty(await field.InputValueAsync()), "The accepted private draft must be cleared.");
            accepted.Add(new(node.Id, id, SharedProviderConsumerFixture.Hash(value), suffix));
            await fixture.EvidenceAsync("wb5-native-secret-references", accepted);
        }
        await OpenOperatorToolboxAsync(fixture, projectId, "assets", "add-secret-reference");
        await fixture.Page.GetByTestId("project-structure-secret-select").SelectOptionAsync(first!.Value.ToString("D"));
        await fixture.Page.GetByTestId("project-structure-secret-purpose").FillAsync("Explicit reuse of accepted identity");
        await fixture.Page.GetByTestId("project-structure-secret-use-selected").ClickAsync();
        await Assertions.Expect(fixture.Page.GetByTestId("project-structure-secret-receipt")).ToContainTextAsync("saved and observed");
        var references = (await TreeAsync(fixture, projectId, includeMetadata: true)).Nodes
            .Select(node => ProjectObjectMetadataSerializer.Parse(node.MetadataJson).SecretReference?.SecretId).OfType<Guid>().ToArray();
        Assert.Equal(3, references.Length);
        Assert.Equal(2, references.Count(id => id == first));
        await fixture.ScreenshotAsync("wb5-native-secret-reference-reuse");
    }

    private static async Task AssertOperatorSecretScopeAsync(SharedProviderConsumerFixture fixture, Guid projectId, Guid agentId) {
        var before = await fixture.Api.GetFromJsonAsync<AgentEditorModel>($"api/agents/{agentId:D}", SharedProviderConsumerFixture.Json);
        Assert.Empty(before!.AllowedSecretReferences);
        var denied = await fixture.ReadOperatorsAsync(projectId, agentId);
        Assert.Equal(2, denied.GetProperty("secrets").GetArrayLength());
        Assert.All(denied.GetProperty("secrets").EnumerateArray(), item => Assert.Equal("denied", item.GetProperty("outcome").GetString()));
        var receipts = JsonSerializer.Deserialize<OperatorSecretReceipt[]>(await File.ReadAllTextAsync(
            Path.Combine(fixture.Settings.Evidence, "wb5-native-secret-references.json")), SharedProviderConsumerFixture.ReadJson)!;
        var accepted = Assert.Single(receipts, item => item.Suffix == "intended");
        var intended = Assert.Single((await TreeAsync(fixture, projectId, includeMetadata: true)).Nodes, node => node.Id == accepted.Id);
        var id = ProjectObjectMetadataSerializer.Parse(intended.MetadataJson).SecretReference!.SecretId;
        Assert.Equal(accepted.SecretId, id);
        await fixture.NavigateAsync($"/agents?tab=agents&agentId={agentId:D}");
        var dialog = fixture.Page.GetByTestId("agents-details-dialog").Last;
        await dialog.GetByRole(AriaRole.Tab, new() { Name = "Secrets", Exact = true }).ClickAsync();
        await dialog.Locator($"input[data-secret-id='{id:D}']").CheckAsync();
        await dialog.GetByTestId("agents-catalog-save").ClickAsync();
        await fixture.Page.GetByText("Agent saved", new() { Exact = true }).WaitForAsync();
        var after = await fixture.Api.GetFromJsonAsync<AgentEditorModel>($"api/agents/{agentId:D}", SharedProviderConsumerFixture.Json);
        Assert.Equal(id, Assert.Single(after!.AllowedSecretReferences).SecretId);
        Assert.Equal(JsonSerializer.Serialize(before.ProjectStructureAccess), JsonSerializer.Serialize(after.ProjectStructureAccess));
        Assert.Equal(JsonSerializer.Serialize(before.WorkspaceToolAccess), JsonSerializer.Serialize(after.WorkspaceToolAccess));
        var observed = await fixture.ReadOperatorsAsync(projectId, agentId);
        var resolved = Assert.Single(observed.GetProperty("secrets").EnumerateArray(), item => item.GetProperty("outcome").GetString() == "resolved");
        Assert.Equal(id, resolved.GetProperty("secretId").GetGuid());
        Assert.Equal(accepted.Sha256, resolved.GetProperty("sha256").GetString());
        Assert.Single(observed.GetProperty("secrets").EnumerateArray(), item => item.GetProperty("outcome").GetString() == "denied");
        await fixture.EvidenceAsync("wb5-native-agent-secret-scope", new { agentId, Before = denied, After = observed,
            Qualification = "Actual saved Agent permissions and native secret resolver; no MCP network request or secret value is exported." });
    }
}
