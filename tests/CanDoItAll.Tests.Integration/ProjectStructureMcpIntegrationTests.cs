using System.Text;
using CanDoItAll.Mcp.ProjectStructure;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectStructureMcpIntegrationTests
{
    [Fact]
    public async Task ProjectStructureMcp_supports_delivery_and_document_asset_roundtrip_through_real_tool_path()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        var tools = CreateTools(host, "Project Structure MCP Roundtrip Agent");

        var project = await AssertOkAsync(tools.ProjectStructureProjectCreateAsync(new ProjectStructureProjectSaveRequest(
            "MCP roundtrip project",
            "Validate the real tool path.",
            "Create a delivery structure with multiple document assets.",
            "Execution",
            ProjectStatus.Active)));

        var lease = await AssertOkAsync(tools.ProjectStructureProjectLeaseAcquireAsync(project.Id, "Create delivery validation assets"));

        var deliveryBlock = await AssertOkAsync(tools.ProjectStructureNodeCreateAsync(
            project.Id,
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.ProjectBlock,
                "Delivery block",
                "Execution",
                "Real MCP path validation.",
                $"project:{project.Id}",
                420,
                220,
                null,
                null,
                "delivery",
                null,
                null,
                lease.LeaseToken)));

        var excelAsset = await CreateAssetAsync(tools, project.Id, deliveryBlock.Id, "Delivery workbook", "Excel evidence", "excel", "delivery-workbook.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "excel payload", lease.LeaseToken);
        var wordAsset = await CreateAssetAsync(tools, project.Id, deliveryBlock.Id, "Delivery brief", "Word evidence", "word", "delivery-brief.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "word payload", lease.LeaseToken);
        var powerpointAsset = await CreateAssetAsync(tools, project.Id, deliveryBlock.Id, "Delivery deck", "PowerPoint evidence", "powerpoint", "delivery-deck.pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation", "powerpoint payload", lease.LeaseToken);
        var pdfAsset = await CreateAssetAsync(tools, project.Id, deliveryBlock.Id, "Delivery packet", "PDF evidence", "pdf", "delivery-packet.pdf", "application/pdf", "%PDF-1.4 payload", lease.LeaseToken);

        var readback = await AssertOkAsync(tools.ProjectStructureReadAsync(
            project.Id,
            new ProjectStructureReadRequest(
                IncludeLinks: true,
                IncludeAssets: true,
                IncludeNotes: true)));

        Assert.Contains(readback.Nodes, node => node.Id == deliveryBlock.Id && node.Title == "Delivery block");
        Assert.Contains(readback.Nodes, node => node.Id == excelAsset.Id && node.MediaOriginalFileName == "delivery-workbook.xlsx");
        Assert.Contains(readback.Nodes, node => node.Id == wordAsset.Id && node.MediaOriginalFileName == "delivery-brief.docx");
        Assert.Contains(readback.Nodes, node => node.Id == powerpointAsset.Id && node.MediaOriginalFileName == "delivery-deck.pptx");
        Assert.Contains(readback.Nodes, node => node.Id == pdfAsset.Id && node.MediaOriginalFileName == "delivery-packet.pdf");
        Assert.Contains(readback.Links, link => link.SourceId == deliveryBlock.Id && link.TargetId == excelAsset.Id);
        Assert.Contains(readback.Links, link => link.SourceId == deliveryBlock.Id && link.TargetId == wordAsset.Id);
        Assert.Contains(readback.Links, link => link.SourceId == deliveryBlock.Id && link.TargetId == powerpointAsset.Id);
        Assert.Contains(readback.Links, link => link.SourceId == deliveryBlock.Id && link.TargetId == pdfAsset.Id);
    }

    [Fact]
    public async Task ProjectStructureMcp_returns_clear_lock_conflicts_and_exposes_current_lease()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        var primaryTools = CreateTools(host, "Primary project-structure agent");
        var competingTools = CreateTools(host, "Competing project-structure agent");

        var lease = await AssertOkAsync(primaryTools.ProjectStructureRepoBranchLeaseAcquireAsync(
            "Mutate branch-owned work",
            repositoryRoot: @"C:\repositories\CanDoItAll",
            branchName: "feature/project-structure"));

        var currentLease = await AssertOkAsync(competingTools.ProjectStructureLeaseGetAsync(
            new ProjectStructureScopeInput(
                ProjectStructureLeaseScopeKind.RepoBranch,
                RepositoryRoot: @"C:\repositories\CanDoItAll",
                BranchName: "feature/project-structure")));

        Assert.NotNull(currentLease);
        Assert.Equal(lease.LeaseToken, currentLease!.LeaseToken);
        Assert.Equal("Primary project-structure agent", currentLease.AgentName);

        var conflict = await competingTools.ProjectStructureRepoBranchLeaseAcquireAsync(
            "Compete for the same branch",
            repositoryRoot: @"C:\repositories\CanDoItAll",
            branchName: "feature/project-structure");

        Assert.False(conflict.Ok);
        Assert.Equal("LeaseConflict", conflict.Error!.Code);
        Assert.Equal("lease_conflict", conflict.Status);
    }

    [Fact]
    public async Task ProjectStructureMcp_returns_policy_failure_when_estimate_is_required()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();

        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var administrationService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentAdministrationService>();
            var profileId = (await administrationService.ListProfilesAsync()).Single().Id;
            var profile = await administrationService.GetProfileAsync(profileId);
            profile.AutoApproveMinutes = 15;
            profile.ApprovalRequiredMinutes = 60;
            profile.RequireApprovalForAllMutations = false;

            var saveResult = await administrationService.SaveProfileAsync(profile);
            Assert.True(saveResult.IsSuccess, string.Join(" ", saveResult.Errors.Select(error => error.Message)));
        }

        var tools = CreateTools(host, "Estimate required agent");
        var result = await tools.ProjectStructureProjectCreateAsync(new ProjectStructureProjectSaveRequest(
            "Policy gate project",
            "Estimate-required validation",
            "Exercise approval policy through the real tool path.",
            "Planning"));

        Assert.False(result.Ok);
        Assert.Equal("EstimateRequired", result.Error!.Code);
        Assert.Equal("estimate_required", result.Status);
    }

    private static async Task<ProjectStructureNodeSummary> CreateAssetAsync(
        ProjectStructureTools tools,
        Guid projectId,
        string parentNodeId,
        string title,
        string subtitle,
        string subtype,
        string fileName,
        string contentType,
        string textContent,
        string leaseToken)
    {
        return await AssertOkAsync(tools.ProjectStructureNodeCreateAsync(
            projectId,
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.File,
                title,
                subtitle,
                $"Create asset {fileName}.",
                parentNodeId,
                null,
                null,
                null,
                null,
                subtype,
                new ProjectObjectMediaPayload(
                    fileName,
                    contentType,
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(textContent))),
                null,
                leaseToken)));
    }

    private static async Task<T> AssertOkAsync<T>(Task<CanDoItAll.Mcp.Core.Contracts.McpToolEnvelope<T>> task)
    {
        var envelope = await task;
        Assert.True(envelope.Ok, envelope.Error?.Message ?? "Tool returned a failed envelope.");
        return envelope.Data!;
    }

    private static ProjectStructureTools CreateTools(ProjectStructureAgentApiTestHost host, string agentName)
    {
        var token = host.Client.DefaultRequestHeaders.GetValues(ProjectStructureAgentHttpHeaders.AgentToken).Single();
        var options = Options.Create(new McpServerOptions
        {
            Server = new ServerOptions
            {
                BaseUrl = host.Client.BaseAddress!.ToString().TrimEnd('/'),
                AgentToken = token,
                AgentName = agentName,
                RepositoryRoot = @"C:\repositories\CanDoItAll",
                BranchName = "tests/project-structure",
                TimeoutSeconds = 30
            }
        });

        var runtime = new RuntimeConfiguration(options, new CanDoItAll.Mcp.Core.Identity.ServerInstanceIdentity());
        var httpClient = new ProjectStructureHttpClient(new HttpClient(), runtime, NullLogger<ProjectStructureHttpClient>.Instance);
        var coordinator = new ProjectStructureCoordinator(httpClient, runtime);
        return new ProjectStructureTools(coordinator, NullLogger<ProjectStructureTools>.Instance);
    }
}
