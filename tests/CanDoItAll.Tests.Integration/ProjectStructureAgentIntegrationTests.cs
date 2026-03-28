using System.IO.Compression;
using System.Text;
using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectStructureAgentIntegrationTests
{
    private static readonly ProjectStructureAgentContext DefaultAgent = new(
        "integration-agent",
        "Integration Agent",
        "integration-machine",
        @"C:\repositories\CanDoItAll",
        "tests/project-structure",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LeaseService_AcquireAsync_reports_conflict_details_for_other_agents()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var leaseService = scope.ServiceProvider.GetRequiredService<ProjectStructureLeaseService>();

        var initialLease = await leaseService.AcquireAsync(
            new ProjectStructureLeaseAcquireRequest(ProjectStructureLeaseScopeKind.Project, "project:alpha", "Initial mutation", 15),
            DefaultAgent);

        Assert.True(initialLease.IsActive);

        var competitor = DefaultAgent with
        {
            AgentId = "other-agent",
            AgentName = "Other Agent",
            MachineName = "other-machine"
        };

        var conflict = await Assert.ThrowsAsync<ProjectStructureLeaseConflictException>(() =>
            leaseService.AcquireAsync(
                new ProjectStructureLeaseAcquireRequest(ProjectStructureLeaseScopeKind.Project, "project:alpha", "Competing mutation", 15),
                competitor));

        Assert.Equal(ProjectStructureLeaseScopeKind.Project, conflict.Conflict.ScopeKind);
        Assert.Equal("project:alpha", conflict.Conflict.ScopeKey);
        Assert.Equal(DefaultAgent.AgentId, conflict.Conflict.AgentId);
        Assert.Equal(DefaultAgent.AgentName, conflict.Conflict.AgentName);
        Assert.Equal(DefaultAgent.MachineName, conflict.Conflict.MachineName);
    }

    [Fact]
    public async Task LeaseService_RunWithProjectMutationLeaseAsync_preserves_existing_owned_lease()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var leaseService = scope.ServiceProvider.GetRequiredService<ProjectStructureLeaseService>();

        var projectId = Guid.NewGuid();
        var initialLease = await leaseService.AcquireAsync(
            new ProjectStructureLeaseAcquireRequest(ProjectStructureLeaseScopeKind.Project, projectId.ToString(), "Long-lived validation lease", 30),
            DefaultAgent);

        var result = await leaseService.RunWithProjectMutationLeaseAsync(
            projectId,
            null,
            DefaultAgent,
            "Temporary mutation without explicit token",
            _ => Task.FromResult("ok"));

        var preservedLease = await leaseService.ValidateOwnedLeaseAsync(
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            initialLease.LeaseToken,
            DefaultAgent);

        Assert.Equal("ok", result);
        Assert.NotNull(preservedLease);
        Assert.Equal(initialLease.LeaseToken, preservedLease!.LeaseToken);
    }

    [Fact]
    public async Task ChecklistService_GetChecklistAsync_propagates_child_priority_and_stops_at_paused_parent()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var checklistService = scope.ServiceProvider.GetRequiredService<ProjectStructureChecklistService>();

        var projectId = await CreateProjectAsync(projects, "Checklist propagation");
        var grandparent = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Delivery branch",
                string.Empty,
                "Top-level delivery branch.",
                $"project:{projectId}",
                360,
                220,
                null,
                null,
                "delivery"));
        var parent = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Execution lane",
                string.Empty,
                "Mid-level branch.",
                grandparent.Id,
                540,
                320,
                null,
                null,
                "implementation"));
        var child = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Ship release",
                string.Empty,
                "Highest priority task.",
                parent.Id,
                760,
                440,
                null,
                null,
                "task"));

        await workbench.UpdateObjectPriorityAsync(projectId, [child.Id], 1);

        var checklist = await checklistService.GetChecklistAsync(projectId, new ProjectStructureChecklistRequest(IncludePaused: true));
        var grandparentItem = Assert.Single(checklist.Items, item => item.NodeId == grandparent.Id);
        var parentItem = Assert.Single(checklist.Items, item => item.NodeId == parent.Id);
        var childItem = Assert.Single(checklist.Items, item => item.NodeId == child.Id);

        Assert.Equal(1, grandparentItem.EffectivePriority);
        Assert.Equal(1, parentItem.EffectivePriority);
        Assert.Equal(1, childItem.EffectivePriority);
        Assert.Contains(childItem.Prerequisites, prerequisite => prerequisite.NodeId == parent.Id && prerequisite.Reason == "parent");
        Assert.Contains(childItem.Prerequisites, prerequisite => prerequisite.NodeId == grandparent.Id && prerequisite.Reason == "parent");

        await workbench.UpdateObjectMarkerAsync(projectId, [parent.Id], "pause", "warn", "Paused");

        var pausedChecklist = await checklistService.GetChecklistAsync(projectId, new ProjectStructureChecklistRequest(IncludePaused: true));
        var pausedGrandparent = Assert.Single(pausedChecklist.Items, item => item.NodeId == grandparent.Id);
        var pausedParent = Assert.Single(pausedChecklist.Items, item => item.NodeId == parent.Id);
        var pausedChild = Assert.Single(pausedChecklist.Items, item => item.NodeId == child.Id);

        Assert.Equal(0, pausedGrandparent.EffectivePriority);
        Assert.Equal(0, pausedParent.EffectivePriority);
        Assert.Equal(1, pausedChild.EffectivePriority);
    }

    [Fact]
    public async Task AgentService_CreateAssetRevisionAsync_creates_child_asset_and_derivedfrom_link()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();

        var projectId = await CreateProjectAsync(projects, "Asset revision");
        var original = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                "Delivery packet",
                "Original PDF",
                "Seed original document.",
                $"project:{projectId}",
                420,
                240,
                null,
                null,
                "pdf",
                CreateMediaPayload("delivery-packet.pdf", "application/pdf", "%PDF-1.4 original packet"),
                null));

        var revision = await agentService.CreateAssetRevisionAsync(
            projectId,
            original.Id,
            new ProjectStructureAssetRevisionRequest(
                "Delivery packet v2",
                "Revised PDF",
                "Create a revised document node.",
                CreateMediaPayload("delivery-packet-v2.pdf", "application/pdf", "%PDF-1.4 revised packet"),
                "pdf",
                null,
                null),
            DefaultAgent);

        Assert.Equal(projectId, revision.ProjectId);
        Assert.Equal(original.Id, revision.RevisionParentNodeId);

        var surface = await workbench.GetStructureAsync(projectId);
        var revisionNode = Assert.Single(surface.Nodes, node => node.Id == revision.NodeId);
        Assert.Equal(original.Id, revisionNode.ParentId);
        Assert.Equal("delivery-packet-v2.pdf", revisionNode.MediaOriginalFileName);
        Assert.Contains(surface.Links, link =>
            string.Equals(link.SourceId, revision.NodeId, StringComparison.Ordinal) &&
            string.Equals(link.TargetId, original.Id, StringComparison.Ordinal) &&
            link.Kind == ProjectObjectLinkKind.DerivedFrom);
    }

    [Fact]
    public async Task AgentService_ImportAsync_accepts_mermaid_mindmap()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();

        var projectId = await CreateProjectAsync(projects, "Mermaid import");
        var result = await agentService.ImportAsync(
            new ProjectStructureImportRequest(
                projectId,
                null,
                ProjectStructureImportSourceKind.Mermaid,
                "Imported Mermaid",
                """
                mindmap
                  Root
                    Delivery
                      Checklist
                """),
            DefaultAgent);

        Assert.Contains(result.Warnings, warning => warning.Contains("indentation", StringComparison.OrdinalIgnoreCase));

        var surface = await workbench.GetStructureAsync(projectId);
        Assert.Contains(surface.Nodes, node => node.Title == "Imported Mermaid");
        Assert.Contains(surface.Nodes, node => node.Title == "Root");
        Assert.Contains(surface.Nodes, node => node.Title == "Delivery");
        Assert.Contains(surface.Nodes, node => node.Title == "Checklist");
    }

    [Fact]
    public async Task AgentService_ImportAsync_accepts_docx_headings()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();

        var projectId = await CreateProjectAsync(projects, "Docx import");
        var docxPayload = CreateMediaPayload(
            "outline.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            BuildDocx("Launch plan", ("Heading2", "Checklist"), ("Heading2", "Evidence")));

        var result = await agentService.ImportAsync(
            new ProjectStructureImportRequest(
                projectId,
                null,
                ProjectStructureImportSourceKind.DocxOutline,
                "Imported DOCX",
                null,
                docxPayload),
            DefaultAgent);

        Assert.NotEmpty(result.CreatedNodeIds);

        var surface = await workbench.GetStructureAsync(projectId);
        Assert.Contains(surface.Nodes, node => node.Title == "Imported DOCX");
        Assert.Contains(surface.Nodes, node => node.Title == "Launch plan");
        Assert.Contains(surface.Nodes, node => node.Title == "Checklist");
        Assert.Contains(surface.Nodes, node => node.Title == "Evidence");
    }

    [Fact]
    public async Task AgentService_ImportAsync_accepts_xmind_json_packages()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();

        var projectId = await CreateProjectAsync(projects, "XMind import");
        var xmindPayload = CreateMediaPayload(
            "outline.xmind",
            "application/octet-stream",
            BuildXmindJsonPackage());

        var result = await agentService.ImportAsync(
            new ProjectStructureImportRequest(
                projectId,
                null,
                ProjectStructureImportSourceKind.XmindMap,
                "Imported XMind",
                null,
                xmindPayload),
            DefaultAgent);

        Assert.NotEmpty(result.CreatedNodeIds);

        var surface = await workbench.GetStructureAsync(projectId);
        Assert.Contains(surface.Nodes, node => node.Title == "Imported XMind");
        Assert.Contains(surface.Nodes, node => node.Title == "Roadmap");
        Assert.Contains(surface.Nodes, node => node.Title == "Execution");
        Assert.Contains(surface.Nodes, node => node.Title == "Validation");
    }

    [Fact]
    public async Task AgentService_ImportAsync_accepts_xmind_xml_packages_across_all_sheets()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();

        var projectId = await CreateProjectAsync(projects, "XMind xml import");
        var xmindPayload = CreateMediaPayload(
            "outline.xmind",
            "application/octet-stream",
            BuildXmindXmlPackage());

        var result = await agentService.ImportAsync(
            new ProjectStructureImportRequest(
                projectId,
                null,
                ProjectStructureImportSourceKind.XmindMap,
                "Imported XMind XML",
                null,
                xmindPayload),
            DefaultAgent);

        Assert.NotEmpty(result.CreatedNodeIds);

        var surface = await workbench.GetStructureAsync(projectId);
        Assert.Contains(surface.Nodes, node => node.Title == "Imported XMind XML");
        Assert.Contains(surface.Nodes, node => node.Title == "Features");
        Assert.Contains(surface.Nodes, node => node.Title == "Management of projects");
        Assert.Contains(surface.Nodes, node => node.Title == "Implementation");
        Assert.Contains(surface.Nodes, node => node.Title == "Shared");
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = "Execution"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static ProjectObjectMediaPayload CreateMediaPayload(string fileName, string contentType, string textContent)
    {
        return new ProjectObjectMediaPayload(
            fileName,
            contentType,
            Convert.ToBase64String(Encoding.UTF8.GetBytes(textContent)));
    }

    private static ProjectObjectMediaPayload CreateMediaPayload(string fileName, string contentType, byte[] bytes)
    {
        return new ProjectObjectMediaPayload(
            fileName,
            contentType,
            Convert.ToBase64String(bytes));
    }

    private static byte[] BuildDocx(string rootHeading, params (string Style, string Text)[] children)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("word/document.xml");
            using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream, Encoding.UTF8, leaveOpen: false);
            writer.WriteLine(
                """
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                  <w:body>
                """);
            WriteParagraph(writer, "Heading1", rootHeading);
            foreach (var (style, text) in children)
            {
                WriteParagraph(writer, style, text);
            }

            writer.WriteLine(
                """
                  </w:body>
                </w:document>
                """);
        }

        return stream.ToArray();
    }

    private static byte[] BuildXmindJsonPackage()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("content.json");
            using var entryStream = entry.Open();
            using var writer = new Utf8JsonWriter(entryStream);
            writer.WriteStartArray();
            writer.WriteStartObject();
            writer.WritePropertyName("rootTopic");
            writer.WriteStartObject();
            writer.WriteString("title", "Roadmap");
            writer.WritePropertyName("children");
            writer.WriteStartObject();
            writer.WritePropertyName("attached");
            writer.WriteStartArray();
            WriteXmindChild(writer, "Execution");
            WriteXmindChild(writer, "Validation");
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.Flush();
        }

        return stream.ToArray();
    }

    private static byte[] BuildXmindXmlPackage()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("content.xml");
            using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream, Encoding.UTF8, leaveOpen: false);
            writer.Write(
                """
                <xmap-content xmlns="urn:xmind:xmap:xmlns:content:2.0">
                  <sheet>
                    <topic>
                      <title>Features</title>
                      <children>
                        <topics>
                          <topic>
                            <title>Management of projects</title>
                          </topic>
                        </topics>
                      </children>
                    </topic>
                  </sheet>
                  <sheet>
                    <topic>
                      <title>Implementation</title>
                      <children>
                        <topics>
                          <topic>
                            <title>Shared</title>
                          </topic>
                        </topics>
                      </children>
                    </topic>
                  </sheet>
                </xmap-content>
                """);
        }

        return stream.ToArray();
    }

    private static void WriteParagraph(StreamWriter writer, string style, string text)
    {
        writer.WriteLine(
            $"""
                <w:p>
                  <w:pPr>
                    <w:pStyle w:val="{style}" />
                  </w:pPr>
                  <w:r>
                    <w:t>{System.Security.SecurityElement.Escape(text)}</w:t>
                  </w:r>
                </w:p>
            """);
    }

    private static void WriteXmindChild(Utf8JsonWriter writer, string title)
    {
        writer.WriteStartObject();
        writer.WriteString("title", title);
        writer.WriteEndObject();
    }
}
