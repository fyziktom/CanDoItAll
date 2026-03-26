using System.Reflection;
using System.Text;
using System.Text.Json;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Infrastructure.DependencyInjection;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.Validation;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

const string ProjectName = "Phase 2 Bundle Validation";
const string SessionName = "Phase 2 Canvas Validation";

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var webContentRoot = Path.Combine(repoRoot, "src", "CanDoItAll.Web");
var workspaceRoot = Path.Combine(webContentRoot, ".artifacts", "workspace");
var databasePath = Path.Combine(workspaceRoot, "candoitall.db");

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ApplicationName = Assembly.GetExecutingAssembly().GetName().Name,
    ContentRootPath = webContentRoot,
    EnvironmentName = Environments.Development
});

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Database:Provider"] = "Sqlite",
    ["Database:ConnectionString"] = $"Data Source={databasePath}",
    ["Storage:WorkspaceRoot"] = ".artifacts/workspace",
    ["DevelopmentManager:TuningModeEnabled"] = "true",
    ["DevelopmentManager:ReviewBeforeSend"] = "true",
    ["DevelopmentManager:ManagerBaseUrl"] = "http://127.0.0.1:6407"
});

builder.Services.AddCanDoItAllInfrastructure(
    builder.Configuration,
    builder.Environment,
    [
        typeof(ProjectsService).Assembly,
        typeof(ProjectWorkbenchService).Assembly,
        typeof(ResourcesService).Assembly,
        typeof(WorkspaceService).Assembly,
        typeof(ProviderExecutionService).Assembly,
        typeof(PromptsService).Assembly,
        typeof(PromptFactoryService).Assembly,
        typeof(ValidationService).Assembly,
        typeof(TestLabService).Assembly
    ]);
builder.Services.AddScoped<IWorkbenchStateStore, InMemoryWorkbenchStateStore>();
builder.Services.AddSecurityModule();
builder.Services.AddWorkspaceModule();
builder.Services.AddProjectsModule();
builder.Services.AddWorkbenchModule();
builder.Services.AddResourcesModule();
builder.Services.AddPromptsModule();
builder.Services.AddFactoryModule();
builder.Services.AddValidationModule();
builder.Services.AddTestLabModule();

using var host = builder.Build();
await using var scope = host.Services.CreateAsyncScope();
var services = scope.ServiceProvider;

var projectsService = services.GetRequiredService<ProjectsService>();
var workbenchService = services.GetRequiredService<ProjectWorkbenchService>();
var promptFactoryService = services.GetRequiredService<PromptFactoryService>();
var dbContextFactory = services.GetRequiredService<IDbContextFactory<AppDbContext>>();

var projectId = await EnsureProjectAsync(projectsService);
await EnsureStructureOverlayDataAsync(workbenchService, projectId);
var sessionId = await EnsurePromptSessionAsync(promptFactoryService, dbContextFactory, projectId);

Console.WriteLine(JsonSerializer.Serialize(new
{
    databasePath,
    workspaceRoot,
    projectId,
    structureUrl = $"/projects/{projectId}/structure",
    sessionId,
    promptFactoryUrl = $"/prompt-factory?sessionId={sessionId}"
}, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));

static async Task<Guid> EnsureProjectAsync(ProjectsService projectsService)
{
    var existing = (await projectsService.ListAsync())
        .FirstOrDefault(project => string.Equals(project.Name, ProjectName, StringComparison.Ordinal));
    if (existing is not null)
    {
        return existing.Id;
    }

    var project = await projectsService.GetAsync(null);
    project.Name = ProjectName;
    project.Description = "Deterministic validation project for phase 2 bundle screenshots.";
    project.Objective = "Exercise prompt-factory attachments, branch lanes, and project-structure validation overlays.";
    project.CurrentPhase = "Review";

    var saved = await projectsService.SaveAsync(project);
    if (!saved.IsSuccess)
    {
        throw new InvalidOperationException($"Unable to create project: {string.Join("; ", saved.Errors.Select(error => error.Message))}");
    }

    return saved.Value;
}

static async Task EnsureStructureOverlayDataAsync(ProjectWorkbenchService workbenchService, Guid projectId)
{
    var surface = await workbenchService.GetStructureAsync(projectId);
    var rootNodeId = surface.Nodes.FirstOrDefault(node => node.ObjectType == CanDoItAll.SharedKernel.ProjectObjectType.ProjectRoot)?.Id
        ?? $"project:{projectId}";

    var validationNode = surface.Nodes.FirstOrDefault(node => string.Equals(node.Title, "Phase 2 Validation", StringComparison.Ordinal))
        ?? await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                CanDoItAll.SharedKernel.ProjectObjectType.ValidationRun,
                "Phase 2 Validation",
                "Bundle review",
                "Keep a blocked validation artifact visible on the canvas.",
                rootNodeId,
                640,
                180));

    var reviewNode = surface.Nodes.FirstOrDefault(node => string.Equals(node.Title, "Phase 2 Review Queue", StringComparison.Ordinal))
        ?? await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                CanDoItAll.SharedKernel.ProjectObjectType.ValidationRun,
                "Phase 2 Review Queue",
                "Pending review",
                "This node keeps the review badge active for screenshot validation.",
                rootNodeId,
                900,
                320));

    var priorityNode = surface.Nodes.FirstOrDefault(node => string.Equals(node.Title, "Escalated dependency", StringComparison.Ordinal))
        ?? await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                CanDoItAll.SharedKernel.ProjectObjectType.Decision,
                "Escalated dependency",
                "Critical path",
                "High priority decision preserved for validation overlay coverage.",
                rootNodeId,
                1160,
                260));

    await workbenchService.UpdateObjectStatusesAsync(projectId, [validationNode.Id], "Blocked");
    await workbenchService.UpdateObjectStatusesAsync(projectId, [reviewNode.Id], "Pending review");
    await workbenchService.UpdateObjectPriorityAsync(projectId, [priorityNode.Id], 5);
}

static async Task<Guid> EnsurePromptSessionAsync(
    PromptFactoryService promptFactoryService,
    IDbContextFactory<AppDbContext> dbContextFactory,
    Guid projectId)
{
    await using var dbContext = await dbContextFactory.CreateDbContextAsync();
    await PromptFactorySchemaInitializer.EnsureAsync(dbContext);

    var session = (await dbContext.Set<PromptBuildSession>()
            .Where(item => item.ProjectId == projectId && item.Name == SessionName)
            .ToListAsync())
        .OrderByDescending(item => item.UpdatedAtUtc)
        .FirstOrDefault();

    var sessionId = session?.Id
        ?? await promptFactoryService.CreateBlankProjectSessionAsync(projectId, SessionName, "Review");

    var editor = await promptFactoryService.GetEditorAsync(sessionId);
    var catalog = await promptFactoryService.GetLibraryCatalogAsync();
    var blocks = await promptFactoryService.ListBlocksAsync();

    var blueprint = catalog.Blueprints.First();
    var flow = catalog.FlowTemplates.FirstOrDefault(item => item.Id == blueprint.RecommendedFlowTemplateId)
        ?? catalog.FlowTemplates.FirstOrDefault(item => string.Equals(item.Key, blueprint.RecommendedFlowKey, StringComparison.OrdinalIgnoreCase))
        ?? catalog.FlowTemplates.First();

    var recommendedBlockIds = blocks
        .Where(block => blueprint.RecommendedBlockKeys.Contains(block.Key, StringComparer.OrdinalIgnoreCase))
        .Select(block => block.Id)
        .ToList();
    if (recommendedBlockIds.Count == 0)
    {
        recommendedBlockIds = blocks.Take(6).Select(block => block.Id).ToList();
    }

    editor.ProjectId = projectId;
    editor.SessionName = SessionName;
    editor.Phase = "Review";
    editor.BlueprintId = blueprint.Id;
    editor.FlowTemplateId = flow.Id;
    editor.RepositoryName = "CanDoItAll";
    editor.BranchName = "main";
    editor.CommitSha = "phase2";
    editor.SelectedBlockIds = recommendedBlockIds;
    editor.SelectedResourceIds = [];

    if (editor.SessionAttachments.All(item => item.Title != "Phase 2 Spec"))
    {
        var attachment = await promptFactoryService.PrepareAttachmentAsync(
            new PromptSessionAttachmentSummary
            {
                Kind = "file",
                Title = "Phase 2 Spec",
                Subtitle = "Bundle validation evidence",
                Notes = "Keep one explicit file attachment visible on the canvas."
            },
            new CanvasWorkbenchUploadedFile
            {
                FileName = "phase2-spec.pdf",
                ContentType = "application/pdf",
                Base64Data = Convert.ToBase64String(Encoding.UTF8.GetBytes("%PDF-1.4 phase2 bundle validation"))
            });
        editor.SessionAttachments.Add(attachment);
    }

    var saved = await promptFactoryService.SaveSessionStateAsync(editor);
    if (!saved.IsSuccess || saved.Value is null)
    {
        throw new InvalidOperationException($"Unable to save prompt session: {string.Join("; ", saved.Errors.Select(error => error.Message))}");
    }

    var built = await promptFactoryService.BuildAsync(saved.Value);
    if (!built.IsSuccess || built.Value is null)
    {
        throw new InvalidOperationException($"Unable to build prompt session: {string.Join("; ", built.Errors.Select(error => error.Message))}");
    }

    var refreshed = await promptFactoryService.GetEditorAsync(sessionId);
    if (refreshed.Nodes.Count > 0)
    {
        await promptFactoryService.SetNodeStateAsync(refreshed.Nodes[0].Id, PromptRunNodeState.Failed);
        var hasSecondaryBranch = refreshed.Nodes.Any(node => !string.Equals(node.BranchKey, "main", StringComparison.OrdinalIgnoreCase));
        if (!hasSecondaryBranch)
        {
            await promptFactoryService.BranchNodeAsync(refreshed.Nodes[0].Id, "Review branch");
        }
    }

    return sessionId;
}


