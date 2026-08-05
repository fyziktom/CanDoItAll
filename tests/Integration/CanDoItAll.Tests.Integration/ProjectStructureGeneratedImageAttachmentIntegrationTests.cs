using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tools.Documents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectStructureGeneratedImageAttachmentIntegrationTests
{
    private static readonly byte[] GeneratedPngBytes =
        Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    private static readonly JsonSerializerOptions FunctionResultJsonOptions = CreateFunctionResultJsonOptions();

    [Fact]
    public async Task Generated_image_draft_round_trips_through_the_governed_asset_tool_under_the_explicit_parent()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projects = services.GetRequiredService<ProjectsService>();
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var agentService = services.GetRequiredService<ProjectStructureAgentService>();
        var workspacePaths = services.GetRequiredService<IWorkspacePathResolutionService>();
        var projectId = await CreateProjectAsync(projects);
        var parent = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Generated visual evidence",
                "Canonical image parent",
                string.Empty,
                $"project:{projectId:D}",
                ObjectSubtype: "delivery"));
        var chatProvider = CreateProvider(ProviderProfilePurpose.Chat);
        var imageProvider = CreateProvider(ProviderProfilePurpose.ImageGeneration);
        var imageService = new RecordingImageGenerationService();
        var agent = CreateAgent(projectId, chatProvider.Id, imageProvider.Id, canWriteProjectStructure: true, canStoreProjectAssets: true);
        var context = CreateContext(agent, chatProvider, projectId);
        var imageToolProvider = new ImageGenerationAgentRuntimeToolProvider(
            new StaticProviderSource([imageProvider]),
            workspacePaths,
            imageService,
            services);
        var projectToolProvider = services
            .GetServices<IAgentRuntimeToolProvider>()
            .OfType<ProjectStructureAgentRuntimeToolProvider>()
            .Single();
        var imageTools = await imageToolProvider.CreateToolsAsync(context, CancellationToken.None);
        var projectTools = await projectToolProvider.CreateToolsAsync(context, CancellationToken.None);
        var outputWorkspacePath = $"artifacts/integration-tests/generated-images/{Guid.NewGuid():N}/layout-proof";
        string? generatedFullPath = null;

        try
        {
            var imageTool = FindTool(imageTools, AgentToolInvocationPolicyMetadata.ImageGenerationCreate);
            var imageFunction = Assert.IsAssignableFrom<AIFunction>(imageTool);
            var targetSchema = imageFunction.JsonSchema
                .GetProperty("properties")
                .GetProperty("request")
                .GetProperty("properties")
                .GetProperty("projectAssetTarget");
            var requiredTargetProperties = targetSchema
                .GetProperty("required")
                .EnumerateArray()
                .Select(property => property.GetString())
                .ToArray();

            Assert.Contains("projectId", requiredTargetProperties);
            Assert.Contains("parentNodeKey", requiredTargetProperties);
            Assert.Contains("title", requiredTargetProperties);
            Assert.Contains("separate project_structure_asset_create", imageTool.Description, StringComparison.Ordinal);

            var generated = await InvokeAsync<ImageGenerationCreateResult>(
                imageTool,
                new AIFunctionArguments
                {
                    ["request"] = new ImageGenerationCreateInput(
                        "A clean visual summary of the delivery plan, no text.",
                        outputWorkspacePath,
                        OutputFormat: "png",
                        ProjectAssetTarget: new ImageGenerationProjectAssetTarget(
                            projectId,
                            parent.Id,
                            "Delivery plan visual",
                            "Generated project evidence",
                            "Generated through the image runtime and attached through the governed asset tool.",
                            "generated"))
                });
            var draft = Assert.IsType<ImageGenerationProjectAssetCreateDraft>(generated.ProjectAssetCreateDraft);
            var generatedResolution = workspacePaths.ResolveFilePath(generated.OutputWorkspacePath, allowMissing: false);
            generatedFullPath = generatedResolution.FullPath;

            Assert.Equal(projectId, draft.ProjectId);
            Assert.Equal(ProjectObjectType.ImageAsset, draft.Request.ObjectType);
            Assert.Equal(parent.Id, draft.Request.ParentNodeKey);
            Assert.Equal(generated.OutputWorkspacePath, draft.Request.SourceWorkspacePath);
            Assert.Equal("layout-proof.png", draft.Request.SourceFileName);
            Assert.Equal("image/png", draft.Request.SourceContentType);
            Assert.Null(draft.Request.Media);
            Assert.Equal(GeneratedPngBytes.LongLength, generated.ContentLength);
            Assert.Equal(GeneratedPngBytes, await File.ReadAllBytesAsync(generatedFullPath));
            Assert.Contains("unchanged", generated.ProjectAssetStorageInstruction, StringComparison.OrdinalIgnoreCase);

            var assetNode = await InvokeAsync<ProjectStructureNodeSummary>(
                FindTool(projectTools, AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreate),
                new AIFunctionArguments
                {
                    ["projectId"] = draft.ProjectId,
                    ["request"] = draft.Request
                });
            var asset = await InvokeAsync<ProjectStructureAssetDescriptor>(
                FindTool(projectTools, "project_structure_asset_get"),
                new AIFunctionArguments
                {
                    ["projectId"] = projectId,
                    ["nodeId"] = assetNode.Id
                });
            var content = await agentService.GetAssetContentAsync(projectId, assetNode.Id);
            var canonical = await workbench.GetStructureAsync(projectId);
            var canonicalAsset = Assert.Single(canonical.Nodes, node => node.Id == assetNode.Id);

            Assert.Equal(parent.Id, canonicalAsset.ParentId);
            Assert.Equal(parent.Id, assetNode.ParentId);
            Assert.Equal(ProjectObjectType.ImageAsset, asset.ObjectType);
            Assert.Equal("generated", asset.ObjectSubtype);
            Assert.Equal("layout-proof.png", asset.MediaOriginalFileName);
            Assert.Equal("image/png", asset.MediaContentType);
            Assert.Equal(GeneratedPngBytes.LongLength, content.ContentLength);
            Assert.Equal(GeneratedPngBytes, Convert.FromBase64String(content.Base64Data));
            Assert.Single(imageService.Requests);
        }
        finally
        {
            DeleteGeneratedFileAndEmptyParent(generatedFullPath);
        }
    }

    [Theory]
    [InlineData(ProjectManagedRoot.Artifacts)]
    [InlineData(ProjectManagedRoot.Output)]
    [InlineData(ProjectManagedRoot.Data)]
    [InlineData(ProjectManagedRoot.IntegrationMap)]
    public async Task Asset_tool_accepts_an_existing_source_from_the_exact_target_project_scope(
        ProjectManagedRoot managedRoot)
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projects = services.GetRequiredService<ProjectsService>();
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var agentService = services.GetRequiredService<ProjectStructureAgentService>();
        var spreadsheets = services.GetRequiredService<ISpreadsheetDocumentService>();
        var projectId = await CreateProjectAsync(projects);
        var chatProvider = CreateProvider(ProviderProfilePurpose.Chat);
        var imageProvider = CreateProvider(ProviderProfilePurpose.ImageGeneration);
        var agent = CreateAgent(projectId, chatProvider.Id, imageProvider.Id, canWriteProjectStructure: true, canStoreProjectAssets: true);
        var projectToolProvider = services
            .GetServices<IAgentRuntimeToolProvider>()
            .OfType<ProjectStructureAgentRuntimeToolProvider>()
            .Single();
        var projectTools = await projectToolProvider.CreateToolsAsync(
            CreateContext(agent, chatProvider, projectId),
            CancellationToken.None);
        var projectScope = WorkspaceScopeDescriptor.Project(projectId.ToString("D"));
        var projectPaths = new WorkspacePathResolutionService(
            services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot(),
            projectScope);
        var (rootName, scopedRoot) = ResolveManagedRoot(projectScope, managedRoot);
        var source = projectPaths.ResolveFilePath(
            $"{rootName}/agent-project-structure-hardening/{Guid.NewGuid():N}/project-finance.xlsx",
            allowMissing: true);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(source.FullPath)!);
            spreadsheets.Write(new SpreadsheetWriteRequest(
                source.FullPath,
                source.FullPath,
                "Summary",
                [],
                [
                    new SpreadsheetRangeWrite(
                        "A1:B3",
                        [
                            ["Metric", "Estimate"],
                            ["Quantity", "24"],
                            ["Total", "=B2*3"]
                        ])
                ],
                CreateWorkbookIfMissing: true,
                Overwrite: true));
            var workbookBytes = await File.ReadAllBytesAsync(source.FullPath);

            Assert.StartsWith(scopedRoot + "/", source.RelativePath, StringComparison.Ordinal);

            var assetNode = await InvokeAsync<ProjectStructureNodeSummary>(
                FindTool(projectTools, AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreate),
                new AIFunctionArguments
                {
                    ["projectId"] = projectId,
                    ["request"] = new ProjectStructureAgentAssetCreateInput(
                        ProjectObjectType.File,
                        "Project finance workbook",
                        "Produced by a project-scoped agent",
                        "Attached by a separate Project Structure writer.",
                        Media: null,
                        ParentNodeKey: $"project:{projectId:D}",
                        ObjectSubtype: "xlsx",
                        SourceWorkspacePath: source.RelativePath,
                        SourceFileName: "project-finance.xlsx",
                        SourceContentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
                });
            var asset = await InvokeAsync<ProjectStructureAssetDescriptor>(
                FindTool(projectTools, "project_structure_asset_get"),
                new AIFunctionArguments
                {
                    ["projectId"] = projectId,
                    ["nodeId"] = assetNode.Id
                });
            var runtimeContent = await InvokeAsync<ProjectStructureAssetContentDescriptor>(
                FindTool(projectTools, AgentToolInvocationPolicyMetadata.ProjectStructureAssetContentGet),
                new AIFunctionArguments
                {
                    ["projectId"] = projectId,
                    ["nodeId"] = assetNode.Id
                });
            var content = await agentService.GetAssetContentAsync(projectId, assetNode.Id);
            var preview = spreadsheets.PreviewWorkbook(new SpreadsheetWorkbookContentPreviewRequest(
                asset.MediaOriginalFileName,
                Convert.FromBase64String(content.Base64Data),
                MaxWorksheets: 1,
                MaxRows: 3,
                MaxColumns: 2));
            var canonical = await workbench.GetStructureAsync(projectId);
            var canonicalAsset = Assert.Single(canonical.Nodes, node => node.Id == assetNode.Id);

            Assert.Equal($"project:{projectId:D}", canonicalAsset.ParentId);
            Assert.Equal("project-finance.xlsx", asset.MediaOriginalFileName);
            Assert.Equal(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                asset.MediaContentType);
            Assert.True(runtimeContent.Base64DataOmitted);
            Assert.Empty(runtimeContent.Base64Data);
            Assert.Contains("workspace_spreadsheet_summary", runtimeContent.ContentSummary, StringComparison.Ordinal);
            Assert.Equal(workbookBytes, Convert.FromBase64String(content.Base64Data));
            var previewWorksheet = Assert.Single(preview.Worksheets);
            Assert.Equal("Summary", previewWorksheet.Name);
            Assert.Equal("Metric", previewWorksheet.Values[0][0]);
            Assert.Equal("=B2*3", previewWorksheet.Values[2][1]);
        }
        finally
        {
            DeleteGeneratedFileAndEmptyParent(source.FullPath);
        }
    }

    [Theory]
    [InlineData(ProjectManagedRoot.Artifacts)]
    [InlineData(ProjectManagedRoot.Output)]
    [InlineData(ProjectManagedRoot.Data)]
    [InlineData(ProjectManagedRoot.IntegrationMap)]
    public async Task Asset_tool_rejects_a_source_from_a_different_project_scope(
        ProjectManagedRoot managedRoot)
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projects = services.GetRequiredService<ProjectsService>();
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projects);
        var foreignProjectId = await CreateProjectAsync(projects);
        var chatProvider = CreateProvider(ProviderProfilePurpose.Chat);
        var imageProvider = CreateProvider(ProviderProfilePurpose.ImageGeneration);
        var agent = CreateAgent(projectId, chatProvider.Id, imageProvider.Id, canWriteProjectStructure: true, canStoreProjectAssets: true);
        var projectToolProvider = services
            .GetServices<IAgentRuntimeToolProvider>()
            .OfType<ProjectStructureAgentRuntimeToolProvider>()
            .Single();
        var projectTools = await projectToolProvider.CreateToolsAsync(
            CreateContext(agent, chatProvider, projectId),
            CancellationToken.None);
        var foreignScope = WorkspaceScopeDescriptor.Project(foreignProjectId.ToString("D"));
        var foreignPaths = new WorkspacePathResolutionService(
            services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot(),
            foreignScope);
        var (rootName, _) = ResolveManagedRoot(foreignScope, managedRoot);
        var source = foreignPaths.ResolveFilePath(
            $"{rootName}/agent-project-structure-hardening/{Guid.NewGuid():N}/foreign-project.xlsx",
            allowMissing: true);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(source.FullPath)!);
            await File.WriteAllBytesAsync(source.FullPath, [0x50, 0x4B, 0x03, 0x04]);

            var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(
                () => InvokeAsync<ProjectStructureNodeSummary>(
                    FindTool(projectTools, AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreate),
                    new AIFunctionArguments
                    {
                        ["projectId"] = projectId,
                        ["request"] = new ProjectStructureAgentAssetCreateInput(
                            ProjectObjectType.File,
                            "Foreign project workbook",
                            "Must be rejected",
                            "A target-project writer must not cross into a different project scope.",
                            Media: null,
                            ParentNodeKey: $"project:{projectId:D}",
                            ObjectSubtype: "xlsx",
                            SourceWorkspacePath: source.RelativePath,
                            SourceFileName: "foreign-project.xlsx")
                    }));

            Assert.Equal("SourceWorkspaceScopeDenied", exception.ErrorCode);
            Assert.True(exception.IsSafeToExpose);
            Assert.True(exception.CanRetryWithCorrectedInput);
            var canonical = await workbench.GetStructureAsync(projectId);
            Assert.DoesNotContain(canonical.Nodes, node => node.Title == "Foreign project workbook");
        }
        finally
        {
            DeleteGeneratedFileAndEmptyParent(source.FullPath);
        }
    }

    [Fact]
    public async Task Asset_tool_rejects_a_source_from_a_different_organization_scope()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projects = services.GetRequiredService<ProjectsService>();
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projects);
        var chatProvider = CreateProvider(ProviderProfilePurpose.Chat);
        var imageProvider = CreateProvider(ProviderProfilePurpose.ImageGeneration);
        var agent = CreateAgent(projectId, chatProvider.Id, imageProvider.Id, canWriteProjectStructure: true, canStoreProjectAssets: true);
        var projectToolProvider = services
            .GetServices<IAgentRuntimeToolProvider>()
            .OfType<ProjectStructureAgentRuntimeToolProvider>()
            .Single();
        var projectTools = await projectToolProvider.CreateToolsAsync(
            CreateContext(agent, chatProvider, projectId),
            CancellationToken.None);
        var foreignPaths = new WorkspacePathResolutionService(
            services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot(),
            WorkspaceScopeDescriptor.Organization(Guid.NewGuid().ToString("N")));
        var source = foreignPaths.ResolveFilePath(
            $"artifacts/agent-project-structure-hardening/{Guid.NewGuid():N}/foreign-organization.xlsx",
            allowMissing: true);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(source.FullPath)!);
            await File.WriteAllBytesAsync(source.FullPath, [0x50, 0x4B, 0x03, 0x04]);

            var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(
                () => InvokeAsync<ProjectStructureNodeSummary>(
                    FindTool(projectTools, AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreate),
                    new AIFunctionArguments
                    {
                        ["projectId"] = projectId,
                        ["request"] = new ProjectStructureAgentAssetCreateInput(
                            ProjectObjectType.File,
                            "Foreign organization workbook",
                            "Must be rejected",
                            "A project writer must not cross into another organization scope.",
                            Media: null,
                            ParentNodeKey: $"project:{projectId:D}",
                            ObjectSubtype: "xlsx",
                            SourceWorkspacePath: source.RelativePath,
                            SourceFileName: "foreign-organization.xlsx")
                    }));

            Assert.Equal("SourceWorkspacePathInvalid", exception.ErrorCode);
            Assert.True(exception.IsSafeToExpose);
            Assert.True(exception.CanRetryWithCorrectedInput);
            var canonical = await workbench.GetStructureAsync(projectId);
            Assert.DoesNotContain(canonical.Nodes, node => node.Title == "Foreign organization workbook");
        }
        finally
        {
            DeleteGeneratedFileAndEmptyParent(source.FullPath);
        }
    }

    [Fact]
    public async Task Project_asset_target_is_rejected_before_generation_when_image_storage_authority_is_disabled()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projects = services.GetRequiredService<ProjectsService>();
        var workspacePaths = services.GetRequiredService<IWorkspacePathResolutionService>();
        var projectId = await CreateProjectAsync(projects);
        var chatProvider = CreateProvider(ProviderProfilePurpose.Chat);
        var imageProvider = CreateProvider(ProviderProfilePurpose.ImageGeneration);
        var imageService = new RecordingImageGenerationService();
        var agent = CreateAgent(projectId, chatProvider.Id, imageProvider.Id, canWriteProjectStructure: true, canStoreProjectAssets: false);
        var imageToolProvider = new ImageGenerationAgentRuntimeToolProvider(
            new StaticProviderSource([imageProvider]),
            workspacePaths,
            imageService,
            services);
        var tools = await imageToolProvider.CreateToolsAsync(
            CreateContext(agent, chatProvider, projectId),
            CancellationToken.None);

        var exception = await Assert.ThrowsAnyAsync<InvalidOperationException>(
            () => InvokeAsync<ImageGenerationCreateResult>(
                FindTool(tools, AgentToolInvocationPolicyMetadata.ImageGenerationCreate),
                new AIFunctionArguments
                {
                    ["request"] = new ImageGenerationCreateInput(
                        "A denied project-asset target.",
                        $"artifacts/integration-tests/generated-images/{Guid.NewGuid():N}/denied",
                        ProjectAssetTarget: new ImageGenerationProjectAssetTarget(
                            projectId,
                            $"project:{projectId:D}",
                            "Denied generated image"))
                }));
        var failure = Assert.IsAssignableFrom<IAgentToolFailure>(exception);

        Assert.Equal("ProjectAssetStorageDenied", failure.ErrorCode);
        Assert.True(failure.IsSafeToExpose);
        Assert.False(failure.CanRetryWithCorrectedInput);
        Assert.Empty(imageService.Requests);
    }

    [Fact]
    public async Task Image_storage_setting_does_not_grant_project_structure_mutation_authority()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projects = services.GetRequiredService<ProjectsService>();
        var projectId = await CreateProjectAsync(projects);
        var chatProvider = CreateProvider(ProviderProfilePurpose.Chat);
        var imageProvider = CreateProvider(ProviderProfilePurpose.ImageGeneration);
        var agent = CreateAgent(projectId, chatProvider.Id, imageProvider.Id, canWriteProjectStructure: false, canStoreProjectAssets: true);
        var context = CreateContext(agent, chatProvider, projectId);
        var imageToolProvider = new ImageGenerationAgentRuntimeToolProvider(
            new StaticProviderSource([imageProvider]),
            services.GetRequiredService<IWorkspacePathResolutionService>(),
            new RecordingImageGenerationService(),
            services);
        var projectToolProvider = services
            .GetServices<IAgentRuntimeToolProvider>()
            .OfType<ProjectStructureAgentRuntimeToolProvider>()
            .Single();

        var imageTools = await imageToolProvider.CreateToolsAsync(context, CancellationToken.None);
        var projectTools = await projectToolProvider.CreateToolsAsync(context, CancellationToken.None);

        Assert.Contains(imageTools, tool => tool.Name == AgentToolInvocationPolicyMetadata.ImageGenerationCreate);
        Assert.DoesNotContain(projectTools, tool => tool.Name == AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreate);
        Assert.DoesNotContain(
            projectTools,
            tool => AgentToolInvocationPolicyMetadata.ProjectStructureMutationTools.Contains(
                tool.Name,
                StringComparer.Ordinal));
    }

    [Fact]
    public async Task Asset_tool_rejects_a_generated_image_draft_whose_workspace_source_is_tampered_outside_the_workspace()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projects = services.GetRequiredService<ProjectsService>();
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projects);
        var chatProvider = CreateProvider(ProviderProfilePurpose.Chat);
        var imageProvider = CreateProvider(ProviderProfilePurpose.ImageGeneration);
        var agent = CreateAgent(projectId, chatProvider.Id, imageProvider.Id, canWriteProjectStructure: true, canStoreProjectAssets: true);
        var context = CreateContext(agent, chatProvider, projectId);
        var imageService = new RecordingImageGenerationService();
        var imageToolProvider = new ImageGenerationAgentRuntimeToolProvider(
            new StaticProviderSource([imageProvider]),
            services.GetRequiredService<IWorkspacePathResolutionService>(),
            imageService,
            services);
        var projectToolProvider = services
            .GetServices<IAgentRuntimeToolProvider>()
            .OfType<ProjectStructureAgentRuntimeToolProvider>()
            .Single();
        var imageTools = await imageToolProvider.CreateToolsAsync(context, CancellationToken.None);
        var projectTools = await projectToolProvider.CreateToolsAsync(context, CancellationToken.None);
        string? generatedFullPath = null;

        try
        {
            var generated = await InvokeAsync<ImageGenerationCreateResult>(
                FindTool(imageTools, AgentToolInvocationPolicyMetadata.ImageGenerationCreate),
                new AIFunctionArguments
                {
                    ["request"] = new ImageGenerationCreateInput(
                        "A generated image with a subsequently tampered source.",
                        $"artifacts/integration-tests/generated-images/{Guid.NewGuid():N}/source-proof",
                        OutputFormat: "png",
                        ProjectAssetTarget: new ImageGenerationProjectAssetTarget(
                            projectId,
                            $"project:{projectId:D}",
                            "Tampered source must not persist"))
                });
            var draft = Assert.IsType<ImageGenerationProjectAssetCreateDraft>(generated.ProjectAssetCreateDraft);
            generatedFullPath = services
                .GetRequiredService<IWorkspacePathResolutionService>()
                .ResolveFilePath(generated.OutputWorkspacePath, allowMissing: false)
                .FullPath;
            var tamperedRequest = draft.Request with
            {
                SourceWorkspacePath = "../outside/generated.png"
            };

            var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(
                () => InvokeAsync<ProjectStructureNodeSummary>(
                    FindTool(projectTools, AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreate),
                    new AIFunctionArguments
                    {
                        ["projectId"] = projectId,
                        ["request"] = tamperedRequest
                    }));

            Assert.Equal("SourceWorkspacePathInvalid", exception.ErrorCode);
            Assert.True(exception.IsSafeToExpose);
            var canonical = await workbench.GetStructureAsync(projectId);
            Assert.DoesNotContain(canonical.Nodes, node => node.Title == tamperedRequest.Title);
        }
        finally
        {
            DeleteGeneratedFileAndEmptyParent(generatedFullPath);
        }
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projects)
    {
        var result = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = "Generated image attachment boundary",
            Description = "Isolated project for generated-image asset integration coverage.",
            Objective = "Prove explicit, governed generated-image attachment.",
            CurrentPhase = "Validation"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static AgentDefinition CreateAgent(
        Guid projectId,
        Guid chatProviderId,
        Guid imageProviderId,
        bool canWriteProjectStructure,
        bool canStoreProjectAssets)
    {
        var configurationJson = AgentProjectStructureAccessMetadata.Write(
            "{}",
            new AgentProjectStructureAccessSettings
            {
                CanRead = true,
                CanWriteNonTaskStructure = canWriteProjectStructure,
                AllowedProjectIds = [projectId]
            });
        configurationJson = AgentImageGenerationAccessMetadata.Write(
            configurationJson,
            new AgentImageGenerationAccessSettings
            {
                CanGenerateImages = true,
                PreferredProviderProfileId = imageProviderId,
                DefaultModel = "gpt-image-1-mini",
                CanStoreImagesAsProjectAssets = canStoreProjectAssets
            });
        var now = DateTimeOffset.UtcNow;

        return new AgentDefinition(
            Guid.NewGuid(),
            "Generated Image Attachment Agent",
            "Generated image asset writer",
            "Exercises the typed image-to-project-asset handoff.",
            "Generate an image, then submit the exact returned asset draft through the governed project-structure asset tool.",
            AgentLifecycleStatus.Active,
            chatProviderId,
            "gpt-5-mini",
            AgentWorkloadKind.General,
            AgentChatHistoryMode.ProviderDefault,
            0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            configurationJson,
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            [],
            [],
            now,
            now);
    }

    private static AgentRuntimeToolProviderContext CreateContext(
        AgentDefinition agent,
        ProviderProfile chatProvider,
        Guid projectId)
    {
        var intent = AgentRuntimeContextIntent.Empty with
        {
            SourceKind = "project-structure",
            SourceId = projectId.ToString("D")
        };

        return new AgentRuntimeToolProviderContext(
            agent,
            chatProvider,
            [],
            SuppressApprovalRequirements: false,
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            RuntimeSessionKey: $"generated-image-attachment:{projectId:D}",
            intent,
            Tags: new Dictionary<string, string>());
    }

    private static ProviderProfile CreateProvider(ProviderProfilePurpose purpose)
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            purpose == ProviderProfilePurpose.ImageGeneration ? "Integration image provider" : "Integration chat provider",
            ProviderKind.OpenAi,
            "https://api.openai.com",
            "OPENAI_API_KEY",
            purpose == ProviderProfilePurpose.ImageGeneration ? "gpt-image-1-mini" : "gpt-5-mini",
            ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: string.Empty,
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: [],
            purpose);
    }

    private static AITool FindTool(IReadOnlyList<AITool> tools, string name)
    {
        return Assert.Single(tools, tool => string.Equals(tool.Name, name, StringComparison.Ordinal));
    }

    private static async Task<T> InvokeAsync<T>(AITool tool, AIFunctionArguments arguments)
    {
        var function = Assert.IsAssignableFrom<AIFunction>(tool);
        var rawResult = await function.InvokeAsync(arguments);

        return rawResult switch
        {
            T result => result,
            JsonElement json => JsonSerializer.Deserialize<T>(json.GetRawText(), FunctionResultJsonOptions)
                ?? throw new InvalidOperationException($"Tool '{tool.Name}' returned null JSON."),
            _ => throw new InvalidOperationException(
                $"Tool '{tool.Name}' returned unexpected result type '{rawResult?.GetType().FullName ?? "<null>"}'.")
        };
    }

    private static JsonSerializerOptions CreateFunctionResultJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static (string RootName, string ScopedRoot) ResolveManagedRoot(
        WorkspaceScopeDescriptor scope,
        ProjectManagedRoot managedRoot)
    {
        return managedRoot switch
        {
            ProjectManagedRoot.Artifacts => (WorkspaceScopeDescriptor.ArtifactManagedRootName, scope.ArtifactRootRelativePath),
            ProjectManagedRoot.Output => (WorkspaceScopeDescriptor.OutputManagedRootName, scope.OutputRootRelativePath),
            ProjectManagedRoot.Data => (WorkspaceScopeDescriptor.DataManagedRootName, scope.DataRootRelativePath),
            ProjectManagedRoot.IntegrationMap => (WorkspaceScopeDescriptor.IntegrationMapManagedRootName, scope.IntegrationMapRootRelativePath),
            _ => throw new ArgumentOutOfRangeException(nameof(managedRoot), managedRoot, null)
        };
    }

    private static void DeleteGeneratedFileAndEmptyParent(string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
        {
            return;
        }

        File.Delete(fullPath);
        var parent = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(parent) &&
            Directory.Exists(parent) &&
            !Directory.EnumerateFileSystemEntries(parent).Any())
        {
            Directory.Delete(parent, recursive: false);
        }
    }

    private sealed class StaticProviderSource(IReadOnlyList<ProviderProfile> providers) : IProviderRuntimeProfileSource
    {
        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(providers);
        }

        public Task<ProviderProfile?> GetProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(providers.FirstOrDefault(provider => provider.Id == providerId));
        }
    }

    public enum ProjectManagedRoot
    {
        Artifacts,
        Output,
        Data,
        IntegrationMap
    }

    private sealed class RecordingImageGenerationService : IAgentImageGenerationService
    {
        public List<AgentImageGenerationRequest> Requests { get; } = [];

        public Task<AgentImageGenerationResult> GenerateAsync(
            AgentImageGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(new AgentImageGenerationResult(
                request.Model,
                request.Format,
                [new AgentGeneratedImage("image/png", GeneratedPngBytes)]));
        }
    }
}
