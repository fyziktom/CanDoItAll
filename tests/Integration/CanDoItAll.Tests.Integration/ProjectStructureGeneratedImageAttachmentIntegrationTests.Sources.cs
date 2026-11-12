using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class ProjectStructureGeneratedImageAttachmentIntegrationTests {
    [Theory]
    [InlineData("layout.svg", "image/svg+xml")]
    [InlineData("layout.gif", "image/gif")]
    [InlineData("layout.png", "image/png")]
    [InlineData("layout.jpg", "image/jpeg")]
    public async Task Unsupported_project_image_sources_are_retryable_without_provider_dispatch(string fileName, string contentType) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projectId = await CreateProjectAsync(services.GetRequiredService<ProjectsService>());
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var sourceData = Convert.ToBase64String("<svg xmlns='http://www.w3.org/2000/svg' width='16' height='16'/>"u8);
        var source = await workbench.CreateObjectAsync(projectId, new(
            ProjectObjectType.ImageAsset, "Layout reference", string.Empty, string.Empty, $"project:{projectId:D}",
            Media: new(fileName, contentType, sourceData)));
        var chatProvider = await CreateProviderAsync(services, ProviderProfilePurpose.Chat);
        var imageProvider = await CreateProviderAsync(services, ProviderProfilePurpose.ImageGeneration);
        var agent = await CreateAgentAsync(services, projectId, chatProvider.Id, imageProvider.Id,
            canWriteProjectStructure: true, canStoreProjectAssets: true);
        var paths = services.GetRequiredService<IWorkspacePathResolutionService>();
        var generation = new RecordingImageGenerationService();
        var provider = new ImageGenerationAgentRuntimeToolProvider(new StaticProviderSource([imageProvider]), paths, generation, services);
        var tool = FindTool(await provider.CreateToolsAsync(CreateContext(agent, chatProvider, projectId), default),
            ImageGenerationToolPolicy.ImageGenerationCreate);
        var request = new ImageGenerationCreateInput("A realistic garden based on the reviewed layout.",
            $"artifacts/integration-tests/generated-images/{Guid.NewGuid():N}/garden.png",
            SourceProjectAssets: [new(projectId, source.Id)]);
        string? generatedPath = null;
        try {
            var exception = await Assert.ThrowsAnyAsync<Exception>(() => InvokeAsync<ImageGenerationCreateResult>(tool,
                new AIFunctionArguments { ["request"] = request }));
            Assert.True(MafAgentToolFailureMapper.TryMap(exception, out var failure));
            Assert.Equal("SourceImageFormatUnsupported", failure.ErrorCode);
            Assert.True(failure.CanRetryWithCorrectedInput);
            Assert.Equal(AgentToolEffectState.NotCommitted, failure.EffectState);
            Assert.Contains(ProjectStructureToolPolicy.ProjectStructureAssetTextGet, failure.Message, StringComparison.Ordinal);
            Assert.Empty(generation.Requests);
            Assert.False(File.Exists(paths.ResolveFilePath(request.OutputWorkspacePath, allowMissing: true).FullPath));

            var corrected = await InvokeAsync<ImageGenerationCreateResult>(tool,
                new AIFunctionArguments { ["request"] = request with { SourceProjectAssets = null } });
            generatedPath = paths.ResolveFilePath(corrected.OutputWorkspacePath, allowMissing: false).FullPath;
            Assert.True(corrected.Success);
            Assert.Empty(Assert.Single(generation.Requests).Sources);
            Assert.Equal(GeneratedPngBytes, await File.ReadAllBytesAsync(generatedPath));
        } finally {
            DeleteGeneratedFileAndEmptyParent(generatedPath);
        }
    }
}
