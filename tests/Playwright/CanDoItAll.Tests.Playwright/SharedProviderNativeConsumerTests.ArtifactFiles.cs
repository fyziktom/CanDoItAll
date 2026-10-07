using System.IO.Compression;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using Microsoft.Playwright;
using static CanDoItAll.Tests.Playwright.Smoke.AgentUiJourneySupport;
using static CanDoItAll.Tests.Playwright.Smoke.ProjectFilesUiJourney;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class SharedProviderNativeConsumerTests {
    private const string WorkbookMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string SvgMime = "image/svg+xml";
    private static readonly XNamespace SpreadsheetXml = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public async Task Final_image_WB6_agent_svg_write_download_and_fresh_text_read_keep_exact_bytes() {
        await using var fixture = await SharedProviderConsumerFixture.StartAsync(ApiAccessScopeNames.ExecuteAgents);
        var context = await CreateArtifactContextAsync(fixture, "SVG", ["SVG Authoring Skill"]);
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 720 160">
              <title>Evidence delivery phases</title>
              <rect id="discover" x="10" y="30" width="180" height="90" fill="#c6e9fa"/>
              <rect id="assemble" x="270" y="30" width="180" height="90" fill="#d9efc7"/>
              <rect id="review" x="530" y="30" width="180" height="90" fill="#f7dcc0"/>
              <path id="discover-assemble" d="M190 75 H270" stroke="#333"/>
              <path id="assemble-review" d="M450 75 H530" stroke="#333"/>
              <text x="25" y="80">Discover &amp; scope</text>
              <text x="285" y="80">Assemble</text>
              <text x="545" y="80">Review</text>
            </svg>
            """;
        var expected = new FileApprovalIntent(context.ProjectId, context.ParentId, "WB6 SVG phases", svg,
            "phases.svg", SvgMime, $"pp2c/{context.Marker}/phases.svg");
        var chat = await OpenArtifactChatAsync(fixture, context, context.AuthorId, context.ParentId, context.ProjectName);
        var retained = (await TreeAsync(fixture, context.ProjectId)).Nodes.SingleOrDefault(item => item.Title == expected.Title);
        JsonElement write;
        if (retained is null) {
            var arguments = new { path = expected.WorkspacePath, content = svg, overwrite = false };
            await fixture.ScriptAsync(context.SourceModel, context.Marker,
                ToolsStep(Call(ToolContractCatalog.WorkspaceWriteFile, arguments)),
                ToolsStep(AttachCall(expected)), TextStep("The exact SVG is stored and attached."));
            write = await FileTurnAsync(fixture, chat, context.AuthorId, context.ProjectId, context.ParentId,
                "Create the agreed SVG phase illustration using native geometry and attach the file here.", expected);
            await fixture.AssertScriptCompleteAsync(3);
        } else {
            var runs = (await fixture.GetAsync($"api/agents/{context.AuthorId:D}/execution-runs")).EnumerateArray().ToArray();
            var original = Assert.Single(runs);
            write = await ReadArtifactRunAsync(fixture, context.AuthorId, original.GetProperty("id").GetGuid());
            await fixture.EvidenceAsync("wb6-svg-original-write-observation", write);
        }
        AssertArtifactTool(write, ToolContractCatalog.WorkspaceWriteFile);
        AssertArtifactTool(write, ProjectStructureToolPolicy.ProjectStructureAssetCreate);
        var node = Assert.Single((await TreeAsync(fixture, context.ProjectId)).Nodes, item => item.Title == expected.Title);
        var bytes = await DownloadArtifactAsync(fixture, context, node, "phases.svg", "wb6-svg", SvgMime);
        Assert.Equal(svg, Encoding.UTF8.GetString(bytes));
        var document = XDocument.Parse(Encoding.UTF8.GetString(bytes));
        XNamespace ns = "http://www.w3.org/2000/svg";
        Assert.Equal(ns + "svg", document.Root!.Name);
        Assert.Equal(3, document.Descendants(ns + "rect").Count());
        Assert.Equal(2, document.Descendants(ns + "path").Count());
        Assert.Equal(["Discover & scope", "Assemble", "Review"], document.Descendants(ns + "text").Select(item => item.Value));
        Assert.DoesNotContain(document.Descendants(), item => item.Name.LocalName is "script" or "foreignObject");
        Assert.DoesNotContain(document.Descendants().Attributes(), item => item.Name.LocalName is "href" or "onload");
        var analyst = await CreateArtifactAnalystAsync(fixture, context, "SVG analyst");
        chat = await OpenArtifactChatAsync(fixture, context, analyst, node.Id, node.Title);
        await fixture.ScriptAsync(context.SourceModel, context.Marker,
            ToolsStep(Call(ProjectStructureToolPolicy.ProjectStructureAssetTextGet, new { projectId = context.ProjectId, nodeId = node.Id }),
                Call(ProjectStructureToolPolicy.ProjectStructureAssetContentGet, new { projectId = context.ProjectId, nodeId = node.Id })),
            TextStep("The native SVG source was read as inert text; inspect the recorded tool result."));
        var read = await ArtifactTurnAsync(fixture, chat, analyst,
            "Read the selected vector asset as text. Report its labels, geometry and connections from the actual source.");
        await fixture.AssertScriptCompleteAsync(2);
        AssertArtifactTool(read, ProjectStructureToolPolicy.ProjectStructureAssetTextGet);
        var outputs = await ReadArtifactOutputsAsync(fixture, context.Marker);
        var text = Assert.Single(outputs, output => output.TryGetProperty("textContent", out _))
            .Deserialize<ProjectStructureAssetTextDescriptor>(SharedProviderConsumerFixture.ReadJson)!;
        Assert.Equal(context.ProjectId, text.Asset.ProjectId);
        Assert.Equal(node.Id, text.Asset.NodeId);
        Assert.False(text.IsTruncated);
        Assert.Equal(svg, text.TextContent);
        await fixture.EvidenceAsync("wb6-svg-text-read", new { Analyst = analyst, Run = read.GetProperty("runId"), Outputs = outputs });
        var before = (await TreeAsync(fixture, context.ProjectId)).Nodes.Count;
        using var invalid = await fixture.Api.PostAsJsonAsync($"api/project-structure/projects/{context.ProjectId:D}/assets", new {
            objectType = nameof(ProjectObjectType.File), title = "Rejected malformed SVG", parentNodeKey = context.ParentId,
            media = new { fileName = "invalid.svg", contentType = SvgMime, base64Data = Convert.ToBase64String(Encoding.UTF8.GetBytes("<svg><text>A & B</text></svg>")) }
        }, SharedProviderConsumerFixture.Json);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(before, (await TreeAsync(fixture, context.ProjectId)).Nodes.Count);
        await fixture.EvidenceAsync("wb6-svg-result", new { Lane = "deterministic", context, NodeId = node.Id,
            WriteRun = write.GetProperty("runId"), ReadRun = read.GetProperty("runId"), Analyst = analyst,
            Bytes = bytes.Length, Sha256 = Convert.ToHexString(SHA256.HashData(bytes)), XmlValid = true, MalformedRejected = true });
    }

    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public async Task Final_image_WB6_agent_xlsx_formulas_download_and_fresh_revision_reads_use_native_tools() {
        await using var fixture = await SharedProviderConsumerFixture.StartAsync(ApiAccessScopeNames.ExecuteAgents);
        var context = await CreateArtifactContextAsync(fixture, "XLSX", ["Spreadsheet Skill", "Workspace Write Spreadsheet",
            "Workspace Spreadsheet Summary", "Workspace Read Spreadsheet Range", "Workspace Read Spreadsheet Cell"]);
        var path = $"pp2c/{context.Marker}/costs.xlsx";
        var chat = await OpenArtifactChatAsync(fixture, context, context.AuthorId, context.ParentId, context.ProjectName);
        var sheets = new (string Name, object[][] Values)[] {
            ("Inputs", [["Phase", "Hours", "USD/hour"], ["Discover", 3, 80], ["Assemble", 5, 120], ["Review", 2, 60]]),
            ("Calculations", [["Phase", "Hours", "Cost USD"], ["=Inputs!A2", "=Inputs!B2", "=Inputs!B2*Inputs!C2"],
                ["=Inputs!A3", "=Inputs!B3", "=Inputs!B3*Inputs!C3"], ["=Inputs!A4", "=Inputs!B4", "=Inputs!B4*Inputs!C4"]]),
            ("Summary", [["Metric", "Value", "Unit"], ["Total hours", "=SUM(Calculations!B2:B4)", "hours"],
                ["Total cost", "=SUM(Calculations!C2:C4)", "USD"], ["Assemble share", "=IF(B3=0,0,Calculations!C3/B3)", "ratio"]])
        };
        var writeRuns = new List<Guid>();
        foreach (var (name, values) in sheets) {
            if (context.CompletedWriteRuns is { } retainedWrites && writeRuns.Count < retainedWrites.Length) {
                var accepted = retainedWrites[writeRuns.Count];
                AssertArtifactTool(await ReadArtifactRunAsync(fixture, context.AuthorId, accepted), ToolContractCatalog.WorkspaceWriteSpreadsheet);
                writeRuns.Add(accepted);
                continue;
            }
            var arguments = new { workbookPath = path, worksheetName = name, outputWorkbookPath = path,
                rangeWrites = new[] { new { rangeAddress = "A1:C4", values } }, createWorkbookIfMissing = true, overwrite = name != "Inputs" };
            await fixture.ScriptAsync(context.SourceModel, context.Marker,
                ToolsStep(Call(ToolContractCatalog.WorkspaceWriteSpreadsheet, arguments)), TextStep("The reviewed worksheet was written."));
            var run = await ArtifactTurnAsync(fixture, chat, context.AuthorId,
                $"Write the agreed {name} sheet in the scoped workbook. Preserve the other sheets and formulas.",
                new Dictionary<string, object> { [ToolContractCatalog.WorkspaceWriteSpreadsheet] = arguments });
            await fixture.AssertScriptCompleteAsync(2);
            AssertArtifactTool(run, ToolContractCatalog.WorkspaceWriteSpreadsheet);
            writeRuns.Add(run.GetProperty("runId").GetGuid());
        }
        var attachment = new FileApprovalIntent(context.ProjectId, context.ParentId, "WB6 cost workbook", "",
            "costs.xlsx", WorkbookMime, path);
        JsonElement attach;
        if (context.AttachmentRun is { } acceptedAttachment) {
            attach = await ReadArtifactRunAsync(fixture, context.AuthorId, acceptedAttachment);
            await fixture.EvidenceAsync("wb6-retained-workbook-attachment", attach);
        } else {
            Assert.DoesNotContain((await TreeAsync(fixture, context.ProjectId)).Nodes, item => item.Title == attachment.Title);
            await fixture.ScriptAsync(context.SourceModel, context.Marker, ToolsStep(AttachCall(attachment)), TextStep("The workbook is attached."));
            attach = await FileTurnAsync(fixture, chat, context.AuthorId, context.ProjectId, context.ParentId,
                "Attach the exact completed workbook to this project.", attachment);
            await fixture.AssertScriptCompleteAsync(2);
        }
        AssertArtifactTool(attach, ProjectStructureToolPolicy.ProjectStructureAssetCreate);
        var node = Assert.Single((await TreeAsync(fixture, context.ProjectId)).Nodes, item => item.Title == attachment.Title);
        var original = await DownloadArtifactAsync(fixture, context, node, "costs.xlsx", "wb6-xlsx-original", WorkbookMime);
        var originalOracle = InspectArtifactWorkbook(original);
        Assert.Equal(10m, originalOracle.Hours);
        Assert.Equal(960m, originalOracle.Cost);
        Assert.Equal(600m, originalOracle.LargestCost);
        var firstRead = await ReadArtifactWorkbookAsync(fixture, context, node, "Original workbook analyst", "5");
        var changed = ChangeArtifactInput(original, "B3", "7");
        var revision = await fixture.PostAsync<ProjectStructureAssetDescriptor>(
            $"api/project-structure/projects/{context.ProjectId:D}/assets/{Uri.EscapeDataString(node.Id)}/revisions", new {
                title = "WB6 cost workbook revised", subtitle = "", notes = "Independent input revision",
                media = new { fileName = "costs-revised.xlsx", contentType = WorkbookMime, base64Data = Convert.ToBase64String(changed) }
            });
        Assert.Equal(node.Id, revision.RevisionParentNodeId);
        var revisedNode = Assert.Single((await TreeAsync(fixture, context.ProjectId)).Nodes, item => item.Id == revision.NodeId);
        var revisedBytes = await DownloadArtifactAsync(fixture, context, revisedNode, "costs-revised.xlsx", "wb6-xlsx-revised", WorkbookMime);
        Assert.Equal(changed, revisedBytes);
        var revisedOracle = InspectArtifactWorkbook(revisedBytes);
        Assert.Equal(12m, revisedOracle.Hours);
        Assert.Equal(1200m, revisedOracle.Cost);
        Assert.Equal(840m, revisedOracle.LargestCost);
        var secondRead = await ReadArtifactWorkbookAsync(fixture, context, revisedNode, "Revised workbook analyst", "7");
        var unchanged = await ReadArtifactBytesAsync(fixture, context.ProjectId, node.Id);
        Assert.Equal(original, unchanged);
        await fixture.EvidenceAsync("wb6-xlsx-result", new { Lane = "deterministic", context, WriteRuns = writeRuns,
            AttachmentRun = attach.GetProperty("runId"), OriginalNode = node.Id, RevisedNode = revision.NodeId,
            OriginalSha256 = Convert.ToHexString(SHA256.HashData(original)), RevisedSha256 = Convert.ToHexString(SHA256.HashData(revisedBytes)),
            OriginalOracle = originalOracle, RevisedOracle = revisedOracle, OriginalRead = firstRead, RevisedRead = secondRead,
            Calculation = "Independent arithmetic from actual numeric Inputs cells. Formula text retained; no claim that caches were evaluated." });
    }

    private sealed record ArtifactContext(Guid ProjectId, string ProjectName, string ParentId, Guid AuthorId, string Marker, string SourceModel,
        Guid[]? CompletedWriteRuns = null, Guid? AttachmentRun = null);
    private sealed record ArtifactWorkbookOracle(decimal Hours, decimal Cost, decimal LargestCost, string[] Formulas, string[] FormulaCaches);

    private static async Task<ArtifactContext> CreateArtifactContextAsync(SharedProviderConsumerFixture fixture, string kind, string[] capabilityNames) {
        if (Environment.GetEnvironmentVariable("CANDOITALL_WB6_ARTIFACT_RESUME") is { Length: > 0 } resumePath) {
            var retained = JsonSerializer.Deserialize<Dictionary<string, ArtifactContext>>(await File.ReadAllTextAsync(resumePath), SharedProviderConsumerFixture.Json)!;
            var saved = retained[kind];
            Assert.True(saved.CompletedWriteRuns is null || saved.CompletedWriteRuns.Length <= 3);
            Assert.Equal(saved.CompletedWriteRuns?.Length ?? 0, saved.CompletedWriteRuns?.Distinct().Count() ?? 0);
            var owner = (await fixture.Api.GetFromJsonAsync<AgentEditorModel>($"api/agents/{saved.AuthorId:D}", SharedProviderConsumerFixture.Json))!;
            Assert.Contains(saved.Marker, owner.Instructions, StringComparison.Ordinal);
            Assert.Equal([saved.ProjectId], owner.ProjectStructureAccess.AllowedProjectIds);
            Assert.False(owner.ProjectStructureAccess.AllowAllProjects);
            foreach (var retainedOwner in retained.Values) {
                foreach (var run in (await fixture.GetAsync($"api/agents/{retainedOwner.AuthorId:D}/execution-runs")).EnumerateArray()) {
                    var runId = run.GetProperty("id").GetGuid();
                    var detail = await ReadArtifactRunAsync(fixture, retainedOwner.AuthorId, runId);
                    await fixture.EvidenceAsync($"wb6-retained-{runId:N}", detail);
                    if ((ExecutionState)run.GetProperty("state").GetInt32() != ExecutionState.WaitingOnTool) {
                        continue;
                    }
                    Assert.Empty(detail.GetProperty("receipts").EnumerateArray());
                    using var cleanup = new HttpClient { BaseAddress = fixture.Api.BaseAddress, Timeout = TimeSpan.FromMinutes(4) };
                    foreach (var header in fixture.Api.DefaultRequestHeaders) {
                        cleanup.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                    using var rejected = await cleanup.PostAsJsonAsync($"api/agents/execution-runs/{runId:D}/pending-approvals", new {
                        approved = false, autoApprovePendingToolCalls = false,
                        decisions = run.GetProperty("pendingApprovals").EnumerateArray().Select(item => new {
                            approvalId = item.GetProperty("approvalId").GetString(), approved = false
                        }).ToArray()
                    }, SharedProviderConsumerFixture.Json);
                    rejected.EnsureSuccessStatusCode();
                    var after = await ReadArtifactRunAsync(fixture, retainedOwner.AuthorId, runId);
                    await fixture.EvidenceAsync($"wb6-rejected-unexecuted-{runId:N}", after);
                    Assert.DoesNotContain(after.GetProperty("receipts").EnumerateArray(), item =>
                        item.GetProperty("effectState").GetInt32() == (int)AgentToolEffectState.Committed);
                }
            }
            return saved;
        }
        var profile = await ImportedResponsesAsync(fixture);
        var marker = "PP2C_WB6_" + kind + "_" + Guid.NewGuid().ToString("N");
        var name = "WB6 " + kind + " " + marker;
        var project = await fixture.PostAsync<Guid>("api/projects", new ProjectEditorModel { Name = name });
        var agent = await CreateFileAgentAsync(fixture, profile, profile.DefaultModel, marker, project, name);
        var editor = (await fixture.Api.GetFromJsonAsync<AgentEditorModel>($"api/agents/{agent:D}", SharedProviderConsumerFixture.Json))!;
        var capabilities = (await fixture.Api.GetFromJsonAsync<CapabilityCatalogItem[]>("api/agents/capabilities", SharedProviderConsumerFixture.Json))!;
        editor.SelectedCapabilityIds = capabilityNames.Select(required => Assert.Single(capabilities, item => item.Name == required).Id).ToList();
        editor.Permissions = editor.Permissions with { CanAskOtherAgents = false };
        await fixture.PostAsync<Guid>("api/agents", editor);
        var context = new ArtifactContext(project, name, $"project:{project:D}", agent, marker, profile.GetModelDisplayName(profile.DefaultModel));
        await fixture.EvidenceAsync("wb6-artifact-context-" + kind, context);
        return context;
    }

    private static async Task<Guid> CreateArtifactAnalystAsync(SharedProviderConsumerFixture fixture, ArtifactContext context, string name) {
        var editor = (await fixture.Api.GetFromJsonAsync<AgentEditorModel>($"api/agents/{context.AuthorId:D}", SharedProviderConsumerFixture.Json))!;
        editor.Id = null;
        editor.ExpectedUpdatedAtUtc = null;
        editor.Name = name + " " + context.Marker + " " + Guid.NewGuid().ToString("N");
        editor.TemplateKey = string.Empty;
        editor.Instructions = context.Marker + ". Read only the selected asset using authorized native tools. Report actual tool content; never guess.";
        editor.ProjectStructureAccess.CanWriteNonTaskStructure = false;
        editor.WorkspaceToolAccess.CanWriteFiles = false;
        var analyst = await fixture.PostAsync<Guid>("api/agents", editor);
        await fixture.EvidenceAsync("wb6-analyst-" + analyst.ToString("N"), new { Id = analyst, editor.Name,
            context.ProjectId, editor.ProjectStructureAccess, editor.WorkspaceToolAccess, editor.Instructions });
        return analyst;
    }

    private static async Task<ILocator> OpenArtifactChatAsync(SharedProviderConsumerFixture fixture, ArtifactContext context, Guid agent, string node, string title) {
        await fixture.NavigateAsync($"/projects/{context.ProjectId:D}/structure");
        await ReadyFileCanvasAsync(fixture.Page);
        await SelectContentNodeAsync(fixture.Page, node, title);
        return await OpenFloatingChatFromCatalogAsync(fixture.Page, "project-structure-agents-toggle", agent,
            Path.Combine(fixture.Settings.Evidence, "wb6-artifact-catalog-failure.png"));
    }

    private static async Task<JsonElement> ReadArtifactRunAsync(SharedProviderConsumerFixture fixture, Guid agentId, Guid runId) {
        var detail = await fixture.GetAsync($"api/agents/{agentId:D}/execution-runs/{runId:D}");
        Assert.Equal(agentId, detail.GetProperty("run").GetProperty("agentId").GetGuid());
        return JsonSerializer.SerializeToElement(new { runId, state = detail.GetProperty("run").GetProperty("state"),
            receipts = detail.GetProperty("toolReceipts"), detail }, SharedProviderConsumerFixture.Json);
    }

    private static async Task<JsonElement> ArtifactTurnAsync(SharedProviderConsumerFixture fixture, ILocator chat, Guid agentId,
        string prompt, IReadOnlyDictionary<string, object>? expectedApprovals = null) {
        var previous = (await fixture.GetAsync($"api/agents/{agentId:D}/execution-runs")).EnumerateArray()
            .Select(run => run.GetProperty("id").GetGuid()).ToHashSet();
        await SendAsync(chat, prompt);
        var approved = new HashSet<string>(StringComparer.Ordinal);
        var decided = new HashSet<string>(StringComparer.Ordinal);
        var deadline = DateTimeOffset.UtcNow.AddMinutes(8);
        Guid? runId = null;
        while (DateTimeOffset.UtcNow < deadline) {
            var runs = (await fixture.GetAsync($"api/agents/{agentId:D}/execution-runs")).EnumerateArray()
                .Where(run => !previous.Contains(run.GetProperty("id").GetGuid())).ToArray();
            if (runs.Length == 0) {
                await Task.Delay(200);
                continue;
            }
            var run = Assert.Single(runs);
            runId ??= run.GetProperty("id").GetGuid();
            Assert.Equal(runId, run.GetProperty("id").GetGuid());
            Assert.False(run.GetProperty("autoApprovePendingToolCalls").GetBoolean());
            foreach (var pending in run.GetProperty("pendingApprovals").EnumerateArray()) {
                var approvalId = pending.GetProperty("approvalId").GetString()!;
                if (decided.Contains(approvalId)) {
                    continue;
                }
                var decision = chat.GetByTestId("chat-approval-approve-" + approvalId);
                if (!await decision.IsVisibleAsync() || !await decision.IsEnabledAsync()) {
                    continue;
                }
                var tool = pending.GetProperty("toolName").GetString()!;
                object? expected = null;
                if (expectedApprovals is null || !expectedApprovals.TryGetValue(tool, out expected) || approved.Contains(tool)) {
                    await chat.GetByTestId("chat-approval-reject-" + approvalId).ClickAsync();
                    Assert.Fail("Rejected an unexpected or repeated artifact effect: " + tool);
                }
                var summary = AgentToolArgumentDisplayFormatter.DescribeArguments(JsonSerializer.Serialize(expected, SharedProviderConsumerFixture.Json), tool);
                var card = decision.Locator("xpath=ancestor::div[contains(@class, 'bg-white')][1]");
                await Assertions.Expect(card).ToContainTextAsync(summary);
                await fixture.EvidenceAsync($"wb6-approval-{runId:N}-{approved.Count}", new { runId, approvalId, tool, ExpectedArguments = expected, DisplaySummary = summary });
                await fixture.ScreenshotAsync($"wb6-approval-{runId:N}-{approved.Count}");
                decided.Add(approvalId);
                approved.Add(tool);
                await decision.ClickAsync();
            }
            var state = (ExecutionState)run.GetProperty("state").GetInt32();
            if (state is ExecutionState.Completed or ExecutionState.Failed) {
                var detail = await ReadArtifactRunAsync(fixture, agentId, runId.Value);
                await fixture.EvidenceAsync("wb6-run-" + runId, detail);
                Assert.Equal(ExecutionState.Completed, state);
                Assert.Equal(expectedApprovals?.Count ?? 0, approved.Count);
                return detail;
            }
            await Task.Delay(200);
        }
        throw new TimeoutException($"Artifact run {runId} did not finish; inspect its original pending state before continuing.");
    }

    private static void AssertArtifactTool(JsonElement run, string tool) =>
        Assert.Contains(run.GetProperty("receipts").EnumerateArray(), item => item.GetProperty("toolName").GetString() == tool &&
            item.GetProperty("invocationOutcome").GetInt32() == (int)AgentToolInvocationOutcome.Succeeded);

    private static async Task<byte[]> ReadArtifactBytesAsync(SharedProviderConsumerFixture fixture, Guid projectId, string nodeId) {
        var content = await fixture.GetAsync($"api/project-structure/projects/{projectId:D}/assets/{Uri.EscapeDataString(nodeId)}/content");
        Assert.False(content.GetProperty("base64DataOmitted").GetBoolean());
        return Convert.FromBase64String(content.GetProperty("base64Data").GetString()!);
    }

    private static async Task<byte[]> DownloadArtifactAsync(SharedProviderConsumerFixture fixture, ArtifactContext context,
        ProjectStructureNodeSummary node, string fileName, string evidenceName, string mime) {
        await fixture.NavigateAsync($"/projects/{context.ProjectId:D}/structure");
        await ReadyFileCanvasAsync(fixture.Page);
        await SelectContentNodeAsync(fixture.Page, node.Id, node.Title);
        await fixture.Page.GetByTestId("project-structure-selection-window").GetByRole(AriaRole.Button, new() { Name = "Expand preview", Exact = true }).ClickAsync();
        var preview = fixture.Page.GetByRole(AriaRole.Dialog, new() { Name = fileName + " file interaction", Exact = true });
        await preview.WaitForAsync();
        if (mime == WorkbookMime) {
            await preview.GetByTestId("spreadsheet-preview").WaitForAsync();
            await Assertions.Expect(preview).ToContainTextAsync("Inputs");
            await Assertions.Expect(preview).ToContainTextAsync("Discover");
        } else {
            await Assertions.Expect(preview).ToContainTextAsync("SVG stays metadata-only and is never inserted as active markup.");
        }
        await fixture.ScreenshotAsync(evidenceName + "-preview");
        await preview.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await preview.WaitForAsync(new() { State = WaitForSelectorState.Detached });
        var files = fixture.Page.GetByTestId("project-structure-file-browser-window");
        if (!await files.IsVisibleAsync()) {
            await fixture.Page.GetByTestId("project-structure-files-toggle").ClickAsync();
        }
        var storedName = Path.GetFileName(node.MediaRelativePath!.Replace('\\', '/'));
        await files.GetByRole(AriaRole.Button, new() { Name = "Actions for " + storedName, Exact = true }).ClickAsync();
        var menu = files.GetByRole(AriaRole.Group, new() { Name = "Actions for " + storedName, Exact = true });
        var download = await fixture.Page.RunAndWaitForDownloadAsync(() => menu.GetByRole(AriaRole.Button, new() { Name = "Download", Exact = true }).ClickAsync());
        Assert.Null(await download.FailureAsync());
        var path = Path.Combine(fixture.Settings.Evidence, evidenceName + Path.GetExtension(fileName));
        await download.SaveAsAsync(path);
        var bytes = await File.ReadAllBytesAsync(path);
        Assert.Equal(await ReadArtifactBytesAsync(fixture, context.ProjectId, node.Id), bytes);
        return bytes;
    }

    private static async Task<Guid> ReadArtifactWorkbookAsync(SharedProviderConsumerFixture fixture, ArtifactContext context,
        ProjectStructureNodeSummary node, string name, string expectedInput) {
        var analyst = await CreateArtifactAnalystAsync(fixture, context, name);
        var chat = await OpenArtifactChatAsync(fixture, context, analyst, node.Id, node.Title);
        await fixture.ScriptAsync(context.SourceModel, context.Marker, ReadStep(context.ProjectId, node.Id),
            ToolsStep(Call(ToolContractCatalog.WorkspaceSpreadsheetSummary, new { workbookPath = node.MediaRelativePath }),
                Call(ToolContractCatalog.WorkspaceReadSpreadsheetRange, new { workbookPath = node.MediaRelativePath, worksheetName = "Inputs", rangeAddress = "A1:C4" }),
                Call(ToolContractCatalog.WorkspaceReadSpreadsheetRange, new { workbookPath = node.MediaRelativePath, worksheetName = "Summary", rangeAddress = "A1:C4" }),
                Call(ToolContractCatalog.WorkspaceReadSpreadsheetCell, new { workbookPath = node.MediaRelativePath, worksheetName = "Inputs", cellAddress = "B3" })),
            TextStep("The native workbook summary and ranges were read; inspect the recorded tool results."));
        var read = await ArtifactTurnAsync(fixture, chat, analyst,
            "Inspect the selected workbook using actual cell and range reads. Report the input rows and summary formulas, with their native evidence.");
        await fixture.AssertScriptCompleteAsync(3);
        AssertArtifactTool(read, ProjectStructureToolPolicy.ProjectStructureAssetContentGet);
        AssertArtifactTool(read, ToolContractCatalog.WorkspaceSpreadsheetSummary);
        AssertArtifactTool(read, ToolContractCatalog.WorkspaceReadSpreadsheetRange);
        AssertArtifactTool(read, ToolContractCatalog.WorkspaceReadSpreadsheetCell);
        var results = await ReadArtifactOutputsAsync(fixture, context.Marker);
        Assert.Contains(results, result => result.GetRawText().Contains("SUM", StringComparison.Ordinal));
        Assert.Contains(results, result => result.GetRawText().Contains("Discover", StringComparison.Ordinal));
        var cell = Assert.Single(results, result => result.TryGetProperty("address", out var address) && address.GetString() == "B3");
        Assert.Equal(expectedInput, cell.GetProperty("value").GetString());
        var inputs = Assert.Single(results, result => result.TryGetProperty("worksheetName", out var sheet) && sheet.GetString() == "Inputs");
        Assert.Equal(node.MediaRelativePath, inputs.GetProperty("workbookPath").GetString());
        Assert.Equal(expectedInput, inputs.GetProperty("values")[2][1].GetString());
        await fixture.EvidenceAsync("wb6-workbook-read-" + analyst.ToString("N"), new { Analyst = analyst, NodeId = node.Id, Outputs = results });
        return read.GetProperty("runId").GetGuid();
    }

    private static async Task<JsonElement[]> ReadArtifactOutputsAsync(SharedProviderConsumerFixture fixture, string marker) {
        var captures = await fixture.ReadCapturesAsync();
        var body = captures.GetProperty("requests").EnumerateArray().Last(item => item.GetProperty("body").GetString()!.Contains(marker, StringComparison.Ordinal));
        Assert.False(body.GetProperty("body_truncated").GetBoolean());
        using var request = JsonDocument.Parse(body.GetProperty("body").GetString()!);
        return request.RootElement.GetProperty("input").EnumerateArray()
            .Where(item => item.TryGetProperty("type", out var type) && type.GetString() == "function_call_output")
            .Select(item => JsonSerializer.Deserialize<JsonElement>(item.GetProperty("output").GetString()!)).ToArray();
    }

    private static ArtifactWorkbookOracle InspectArtifactWorkbook(byte[] bytes) {
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Assert.DoesNotContain(archive.Entries, item => item.FullName.Contains("vbaProject", StringComparison.OrdinalIgnoreCase) ||
            item.FullName.StartsWith("xl/externalLinks/", StringComparison.Ordinal));
        var workbook = ReadWorkbookXml(archive, "xl/workbook.xml");
        Assert.Equal(["Inputs", "Calculations", "Summary"], workbook.Descendants(SpreadsheetXml + "sheet").Select(item => (string?)item.Attribute("name")));
        var sheets = Enumerable.Range(1, 3).Select(index => ReadWorkbookXml(archive, $"xl/worksheets/sheet{index}.xml")).ToArray();
        Assert.DoesNotContain(sheets.SelectMany(item => item.Descendants(SpreadsheetXml + "c")), cell => (string?)cell.Attribute("t") == "e");
        var formulas = sheets.SelectMany(item => item.Descendants(SpreadsheetXml + "f")).Select(item => item.Value).ToArray();
        Assert.Contains("SUM(Calculations!B2:B4)", formulas);
        Assert.Contains("SUM(Calculations!C2:C4)", formulas);
        Assert.Contains("IF(B3=0,0,Calculations!C3/B3)", formulas);
        decimal Number(string address) => decimal.Parse(sheets[0].Descendants(SpreadsheetXml + "c")
            .Single(cell => (string?)cell.Attribute("r") == address).Element(SpreadsheetXml + "v")!.Value, System.Globalization.CultureInfo.InvariantCulture);
        var hours = Enumerable.Range(2, 3).Sum(row => Number($"B{row}"));
        var costs = Enumerable.Range(2, 3).Select(row => Number($"B{row}") * Number($"C{row}")).ToArray();
        var caches = sheets.SelectMany(item => item.Descendants(SpreadsheetXml + "c"))
            .Where(cell => cell.Element(SpreadsheetXml + "f") is not null).Select(cell => cell.Element(SpreadsheetXml + "v")?.Value ?? "absent").ToArray();
        return new(hours, costs.Sum(), costs.Max(), formulas, caches);
    }

    private static XDocument ReadWorkbookXml(ZipArchive archive, string path) {
        using var stream = (archive.GetEntry(path) ?? throw new InvalidDataException("Missing workbook part: " + path)).Open();
        return XDocument.Load(stream);
    }

    private static byte[] ChangeArtifactInput(byte[] original, string address, string value) {
        using var copy = new MemoryStream();
        copy.Write(original);
        copy.Position = 0;
        using (var archive = new ZipArchive(copy, ZipArchiveMode.Update, leaveOpen: true)) {
            const string path = "xl/worksheets/sheet1.xml";
            var sheet = ReadWorkbookXml(archive, path);
            sheet.Descendants(SpreadsheetXml + "c").Single(cell => (string?)cell.Attribute("r") == address)
                .Element(SpreadsheetXml + "v")!.Value = value;
            archive.GetEntry(path)!.Delete();
            using var output = archive.CreateEntry(path).Open();
            sheet.Save(output);
        }
        return copy.ToArray();
    }
}
