using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Editor.UI;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using ProviderProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentEditorChildBoundaryTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Source_refresh_read_cannot_publish_after_provider_selection_changes(bool returnToOriginal) {
        using var context = AgentDetailsDialogAvatarGenerationTests.CreateContext(new UnavailableAgentImageGenerationService());
        var service = DispatchProxy.Create<ISharedProviderManagementService, SharedProviderRefreshButtonTests.RefreshProxy>();
        var proxy = (SharedProviderRefreshButtonTests.RefreshProxy)(object)service;
        context.Services.AddSingleton(service);
        var model = SharedProviderRoutingModelIdCodec.Create(proxy.Selected.RemotePublicationId, "real-model").Value;
        var first = AgentDetailsDialogAvatarGenerationTests.CreateImageProvider() with {
            Id = proxy.Selected.ProviderProfileId, Name = "Original shared provider", Purpose = ProviderProfilePurpose.Chat,
            DefaultModel = model, SuggestedModels = [model],
            CredentialBinding = new(Guid.NewGuid(), ProviderCredentialPurpose.SourceAccessToken,
                ProviderCredentialConsumerKind.Source, proxy.Selected.SourceId)
        };
        var second = first with { Id = Guid.NewGuid(), Name = "Successor local provider", CredentialBinding = null };
        var cut = context.RenderEditor(new() { Name = "Unrelated draft", ProviderProfileId = first.Id }, AgentEditorSection.Runtime, [first, second]);
        var reads = context.Services.GetRequiredService<AgentEditorReadFixture>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource<IReadOnlyList<ProviderProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        reads.ReadProviders = _ => {
            started.TrySetResult();
            return released.Task;
        };
        var refresh = cut.Find("[data-testid='shared-provider-refresh-capabilities']").ClickAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Select(second.Name);
        if (returnToOriginal) {
            Select(first.Name);
        }
        released.SetResult([first with { Name = "Obsolete catalog response" }]);
        await refresh;
        var current = cut.FindComponent<AgentEditorCoreSurface>().Instance.State;
        Assert.Equal(returnToOriginal ? first.Name : second.Name, current.RuntimeProvider!.Name);
        Assert.Equal("Unrelated draft", current.Draft.Name);
        Assert.Equal(2, current.RuntimeProviders.Count);
        Assert.Equal(1, reads.ProviderReads);
        Assert.DoesNotContain("Obsolete catalog response", cut.Markup);

        void Select(string name) {
            var input = cut.Find("[data-testid='agents-catalog-provider']");
            input.Change(input.QuerySelectorAll("option").Single(option => option.TextContent.Trim() == name).GetAttribute("value"));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Shared_provider_refresh_retains_editor_draft(bool fail) {
        using var context = AgentDetailsDialogAvatarGenerationTests.CreateContext(new UnavailableAgentImageGenerationService());
        var service = DispatchProxy.Create<ISharedProviderManagementService, SharedProviderRefreshButtonTests.RefreshProxy>();
        var proxy = (SharedProviderRefreshButtonTests.RefreshProxy)(object)service;
        proxy.Fail = fail;
        context.Services.AddSingleton(service);
        var model = SharedProviderRoutingModelIdCodec.Create(proxy.Selected.RemotePublicationId, "real-model").Value;
        var provider = AgentDetailsDialogAvatarGenerationTests.CreateImageProvider() with {
            Id = proxy.Selected.ProviderProfileId,
            Purpose = ProviderProfilePurpose.Chat,
            DefaultModel = model,
            SuggestedModels = [model],
            CredentialBinding = new(Guid.NewGuid(), ProviderCredentialPurpose.SourceAccessToken,
                ProviderCredentialConsumerKind.Source, proxy.Selected.SourceId)
        };
        var draft = new AgentEditorModel {
            Name = "Unsaved name",
            Instructions = "Unsaved instructions",
            ProviderProfileId = provider.Id,
            Model = model
        };
        var cut = context.RenderEditor(draft, AgentEditorSection.Runtime, [provider]);
        var editContext = cut.FindComponent<EditForm>().Instance.EditContext;
        await cut.Find("[data-testid='shared-provider-refresh-capabilities']").ClickAsync();
        Assert.Same(editContext, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal("Unsaved name", draft.Name);
        Assert.Equal("Unsaved instructions", draft.Instructions);
        Assert.Equal(provider.Id, draft.ProviderProfileId);
        Assert.Equal(model, draft.Model);
        Assert.Equal(1, context.Services.GetRequiredService<AgentEditorReadFixture>().ProviderReads);
        Assert.Equal([proxy.Selected.RemotePublicationId], proxy.SynchronizedIds);
        Assert.Contains(fail ? "could not be confirmed" : "unsaved selections were preserved",
            cut.Find("[data-testid='shared-provider-refresh-result']").TextContent);
    }

    [Fact]
    public async Task Avatar_generation_failure_retains_unrelated_draft() {
        var generation = new DeferredGeneration();
        var provider = AgentDetailsDialogAvatarGenerationTests.CreateImageProvider();
        using var context = AgentDetailsDialogAvatarGenerationTests.CreateContext(generation, [provider]);
        var draft = new AgentEditorModel { Name = "Retained name", Instructions = "Retained instructions" };
        var cut = context.RenderEditor(draft, AgentEditorSection.Identity, [provider]);
        cut.Find("[data-testid='agents-catalog-avatar-open']").Click();
        var generated = cut.Find("[data-testid='agents-catalog-avatar-ai-generate']").ClickAsync();
        await cut.InvokeAsync(() => generation.Pending.SetException(new InvalidOperationException("Generation failed.")));
        await generated;
        Assert.Empty(draft.AvatarImageUrl);
        Assert.Equal("Retained name", draft.Name);
        Assert.Equal("Retained instructions", draft.Instructions);
        Assert.Contains(context.Services.GetRequiredService<NotificationService>().Messages,
            message => message.Summary == "AI avatar generation failed");
    }

    [Fact]
    public async Task Reset_rejects_pending_avatar_result() {
        var generation = new DeferredGeneration();
        var provider = AgentDetailsDialogAvatarGenerationTests.CreateImageProvider();
        using var context = AgentDetailsDialogAvatarGenerationTests.CreateContext(generation, [provider]);
        var cut = context.RenderEditor(new() { Name = "Old draft" }, AgentEditorSection.Identity, [provider]);
        cut.Find("[data-testid='agents-catalog-avatar-open']").Click();
        var generated = cut.Find("[data-testid='agents-catalog-avatar-ai-generate']").ClickAsync();
        await cut.FindComponent<StickyActionFooter>().FindAll("button").Single(button => button.TextContent.Trim() == "Clear").ClickAsync();
        await cut.InvokeAsync(() => generation.Pending.SetResult(new AgentImageGenerationResult(
            provider.DefaultModel, AgentGeneratedImageFormat.Jpeg,
            [new AgentGeneratedImage("image/jpeg", Convert.FromBase64String(AgentDetailsDialogAvatarGenerationTests.ValidSquareJpegBase64))])));
        await generated;
        Assert.True(cut.Instance.CurrentTarget.IsNew);
        var current = (AgentEditorModel)cut.FindComponent<EditForm>().Instance.EditContext!.Model;
        Assert.Empty(current.AvatarImageUrl);
        Assert.Empty(current.Name);
    }

    private sealed class DeferredGeneration : IAgentImageGenerationService {
        public TaskCompletionSource<AgentImageGenerationResult> Pending { get; } = new();
        public Task<AgentImageGenerationResult> GenerateAsync(AgentImageGenerationRequest request,
            CancellationToken cancellationToken = default) => Pending.Task;
    }
}
