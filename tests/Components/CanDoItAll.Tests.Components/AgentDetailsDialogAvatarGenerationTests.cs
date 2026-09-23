using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.Workspace;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ProviderKind = CanDoItAll.AgentFramework.Models.ProviderKind;
using ProviderProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentDetailsDialogAvatarGenerationTests
{
    [Fact]
    public void Avatar_dialog_explains_when_no_default_image_provider_is_available()
    {
        using var context = CreateContext(new UnavailableAgentImageGenerationService());
        var cut = RenderIdentityTab(context, new AgentEditorModel(), []);

        cut.Find("[data-testid='agents-catalog-avatar-open']").Click();

        var warning = cut.Find("[data-testid='agents-catalog-avatar-ai-unavailable']");
        Assert.Contains("No default image provider is set", warning.TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid='agents-catalog-avatar-ai-generate']"));
    }

    [Fact]
    public async Task Avatar_dialog_prefills_and_prepares_prompt_for_the_default_image_provider()
    {
        var imageGenerationService = new RecordingImageGenerationService();
        var provider = CreateImageProvider();
        using var context = CreateContext(imageGenerationService, [provider]);
        var editor = new AgentEditorModel
        {
            Name = "Luna",
            RoleTitle = "Research assistant"
        };
        var cut = RenderIdentityTab(context, editor, [provider]);

        cut.Find("[data-testid='agents-catalog-avatar-open']").Click();

        var prompt = cut.Find("[data-testid='agents-catalog-avatar-ai-prompt']");
        Assert.Contains("Luna", prompt.TextContent, StringComparison.Ordinal);
        Assert.Contains("Research assistant", prompt.TextContent, StringComparison.Ordinal);

        prompt.Input("A calm blue fox with a subtle lunar motif.");
        await cut.Find("[data-testid='agents-catalog-avatar-ai-generate']")
            .ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            Assert.StartsWith("data:image/jpeg;base64,", editor.AvatarImageUrl, StringComparison.Ordinal);
            var request = Assert.IsType<AgentImageGenerationRequest>(imageGenerationService.LastRequest);
            Assert.Equal(provider.Id, request.Provider.Id);
            Assert.Equal(provider.DefaultModel, request.Model);
            Assert.Equal("1024x1024", request.Size);
            Assert.Equal("low", request.Quality);
            Assert.Equal(AgentGeneratedImageFormat.Jpeg, request.Format);
            Assert.Equal(AgentAvatarGenerationService.DefaultOutputCompression, request.OutputCompression);
            Assert.Contains("Create a square professional avatar", request.Prompt, StringComparison.Ordinal);
            Assert.Contains("Do not depict a real identifiable person", request.Prompt, StringComparison.Ordinal);
            Assert.Contains("A calm blue fox with a subtle lunar motif.", request.Prompt, StringComparison.Ordinal);
        });
    }

    internal static BunitContext CreateContext(
        IAgentImageGenerationService imageGenerationService,
        IReadOnlyList<ProviderProfile>? providers = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddStubProviderRuntimeAdministration();
        context.Services.AddSingleton<IExternalTargetPathRegistryFactory>(new ExternalTargetPathRegistryFactory());
        context.Services.AddSingleton<IStorageCatalogSelectionSource>(new EmptyStorageCatalogSelectionSource());
        var generationService = new AgentAvatarGenerationService(
            imageGenerationService,
            NullLogger<AgentAvatarGenerationService>.Instance);
        var workspaceService = DispatchProxy.Create<IAgentFrameworkWorkspaceService, WorkspaceServiceProxy>();
        ((WorkspaceServiceProxy)(object)workspaceService).Providers = providers ?? [];
        context.Services.AddSingleton(generationService);
        context.Services.AddSingleton(workspaceService);
        context.Services.AddSingleton<IAvatarGenerationGateway>(
            new AgentAvatarGenerationGateway(workspaceService, generationService));
        context.Services.AddAgentEditorReadFixture();
        return context;
    }

    private static IRenderedComponent<AgentDetailsDialog> RenderIdentityTab(
        BunitContext context,
        AgentEditorModel editor,
        IReadOnlyList<ProviderProfile> providers)
    {
        return context.RenderEditor(editor, AgentEditorSection.Identity, providers: providers);
    }

    internal static ProviderProfile CreateImageProvider()
    {
        return new ProviderProfile(
            Id: Guid.NewGuid(),
            Name: "Default image provider",
            Kind: ProviderKind.OpenAi,
            BaseUrl: "https://api.example.test/v1",
            ApiKeyEnvironmentVariable: "TEST_IMAGE_API_KEY",
            DefaultModel: "gpt-image-1",
            Transport: ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: false,
            SupportsTools: false,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Healthy",
            LastCheckedAtUtc: null,
            SuggestedModels: ["gpt-image-1"],
            Purpose: ProviderProfilePurpose.ImageGeneration);
    }

    public class WorkspaceServiceProxy : DispatchProxy
    {
        public IReadOnlyList<ProviderProfile> Providers { get; set; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "add_ExecutionUpdated" or "remove_ExecutionUpdated" => null,
                nameof(IAgentFrameworkWorkspaceService.ListProvidersAsync) => Task.FromResult(Providers),
                _ => throw new InvalidOperationException(
                    $"Workspace service member '{targetMethod?.Name}' was not expected in this component test.")
            };
        }
    }

    private sealed class RecordingImageGenerationService : IAgentImageGenerationService
    {
        public AgentImageGenerationRequest? LastRequest { get; private set; }

        public Task<AgentImageGenerationResult> GenerateAsync(
            AgentImageGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new AgentImageGenerationResult(
                request.Model,
                request.Format,
                [new AgentGeneratedImage("image/jpeg", Convert.FromBase64String(ValidSquareJpegBase64))]));
        }
    }

    internal const string ValidSquareJpegBase64 =
        "/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCAAgACADASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwDEooor9dPyQKKKKACiiigAooooA//Z";
}
