using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Resources.Pages;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

[Trait("Category", "HostPlatform")]
public sealed class ResourcesExactEditorHostTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Routed_agent_context_waits_for_exact_editor_after_independent_catalog_refresh(bool failFirst) {
        HeldEditorOwner? held = null;
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddScoped<IResourceRegistryOwner>(provider => held = new(ActivatorUtilities.CreateInstance<ResourceRegistryOwner>(provider))));
        var services = harness.Context.Services;
        var project = await OwnerPostcommitTestProbe.CreateProjectAsync(services, "Exact readiness fixture");
        var resources = services.GetRequiredService<ResourcesService>();
        var model = new ResourceEditorModel {
            ProjectId = project.ProjectId,
            ExpectedProjectAdmission = project,
            Name = "Exact stored editor",
            ConnectorPluginKey = ResourceConnectorPluginKeys.WebLink,
            ConfigSchemaVersion = resources.ListConnectorManifests().Single(m => m.PluginKey == ResourceConnectorPluginKeys.WebLink).ConfigurationSchema.Version
        };
        model.Configuration.SetText(ResourceConnectorFieldKeys.WebUrl, "https://fixture.test/");
        var result = await resources.SaveAsync(model);
        Assert.True(result.IsSuccess);
        var navigation = services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"http://localhost/resources?resourceId={result.Value:D}");
        var cut = harness.Context.Render<ResourcesPage>();
        var owner = Assert.IsType<HeldEditorOwner>(held);
        await owner.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        var context = services.GetRequiredService<IAgentChatContextRegistry>();
        if (failFirst) {
            owner.Release.TrySetException(new InvalidOperationException("Synthetic exact-read failure"));
            cut.WaitForAssertion(() => Assert.Equal(AgentChatContextAccessState.Failed, context.Capture().Scope.AccessState));
        }
        await cut.InvokeAsync(() => cut.FindComponents<PageHeaderActionButton>().Single(c => c.Instance.Label == "Refresh").Find("button").ClickAsync());
        cut.WaitForAssertion(() => Assert.Equal(failFirst ? AgentChatContextAccessState.Failed : AgentChatContextAccessState.Loading, context.Capture().Scope.AccessState));
        await Assert.ThrowsAsync<AgentChatContextUnavailableException>(async () => await context.CaptureAsync());
        if (failFirst) {
            await cut.InvokeAsync(() => cut.Find(".cda-selection-list-item__button").ClickAsync());
        } else {
            owner.Release.TrySetResult();
        }
        cut.WaitForAssertion(() => Assert.Equal(AgentChatContextAccessState.Ready, context.Capture().Scope.AccessState));
        Assert.Equal(failFirst ? 2 : 1, owner.ExactReads);
        Assert.Equal("Exact stored editor", cut.Instance.Registry.Draft.Editor.Name);
    }

    private sealed class HeldEditorOwner(IResourceRegistryOwner inner) : IResourceRegistryOwner {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ExactReads { get; private set; }
        public bool IsCurrent => inner.IsCurrent;
        public async Task<ResourceEditorModel> GetAsync(Guid id, CancellationToken cancellationToken = default) {
            if (++ExactReads == 1) {
                Entered.TrySetResult();
                await Release.Task;
            }
            return await inner.GetAsync(id, cancellationToken);
        }
        public IReadOnlyList<ConnectorPluginManifest> ListManifests() => inner.ListManifests();
        public string BuildLocationPreview(ResourceEditorModel editor) => inner.BuildLocationPreview(editor);
        public Task<IReadOnlyList<ResourceSummary>> ListAsync(CancellationToken cancellationToken = default) => inner.ListAsync(cancellationToken);
        public Task<IReadOnlyList<ResourceProjectOption>> ListProjectsAsync(CancellationToken cancellationToken = default) => inner.ListProjectsAsync(cancellationToken);
        public Task<IReadOnlyList<ResourceReferenceOption>> ListSecretsAsync(CancellationToken cancellationToken = default) => inner.ListSecretsAsync(cancellationToken);
        public Task<IReadOnlyList<ResourceReferenceOption>> ListPartiesAsync(Guid? projectId, IReadOnlyList<Guid> retainedIds, CancellationToken cancellationToken = default) => inner.ListPartiesAsync(projectId, retainedIds, cancellationToken);
        public Task<Result<Guid>> SaveAsync(ResourceEditorModel command) => inner.SaveAsync(command);
        public Task DeleteAsync(Guid id, ProjectWriteAdmission? admission) => inner.DeleteAsync(id, admission);
    }
}
