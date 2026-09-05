using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentEditorSessionTests {
    [Fact]
    public async Task Section_changes_retain_edit_context_and_unsaved_draft() {
        var reads = new AgentEditorReadFixture();
        var memory = new EmptyMemoryProfiles();
        await using var harness = await CreateHarnessAsync(reads, memory);
        var cut = harness.Context.Render<AgentDetailsDialog>();
        var context = cut.FindComponent<EditForm>().Instance.EditContext;
        cut.Find("[data-testid='agents-catalog-name']").Change("Retained draft");
        Assert.Equal(new[] { "Identity", "Runtime", "Memory", "Images", "Project Structure Access", "Workspace Tools", "Secrets", "Process Access", "Capabilities", "Voice" },
            cut.FindAll("[data-testid='agents-details-tabs'] button[role='tab']").Select(tab => tab.TextContent.Trim()));
        foreach (var section in Enum.GetValues<AgentEditorSection>()) {
            cut.Render(parameters => parameters.Add(component => component.Section, section));
            Assert.Same(context, cut.FindComponent<EditForm>().Instance.EditContext);
            Assert.Equal("Retained draft", ((AgentEditorModel)context!.Model).Name);
        }
        Assert.Equal(1, memory.Reads);
        Assert.Equal(0, reads.ProjectReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Target_change_ignores_old_load_completion(bool failOldLoad) {
        var oldId = Guid.NewGuid();
        var currentId = Guid.NewGuid();
        var pending = new TaskCompletionSource<AgentEditorLoadResult>();
        var reads = new AgentEditorReadFixture();
        reads.Load = (target, _) => target.AgentId == oldId ? pending.Task : Task.FromResult(reads.Result(new() { Id = target.AgentId, Name = "Current target" }));
        await using var harness = await CreateHarnessAsync(reads);
        var cut = harness.Context.Render<AgentDetailsDialog>(parameters => parameters.Add(component => component.AgentId, oldId));
        cut.WaitForElement("[data-testid='agents-details-loading']");
        cut.Render(parameters => parameters.Add(component => component.AgentId, currentId));
        cut.WaitForElement("[data-testid='agents-catalog-name']");
        var context = cut.FindComponent<EditForm>().Instance.EditContext;
        var renders = cut.RenderCount;
        await cut.InvokeAsync(() => {
            if (failOldLoad) {
                pending.SetException(new InvalidOperationException("Stale load failure."));
            } else {
                pending.SetResult(reads.Result(new() { Id = oldId, Name = "Old target" }));
            }
        });
        cut.WaitForState(() => cut.RenderCount > renders);
        Assert.Equal(currentId, cut.Instance.CurrentTarget.AgentId);
        Assert.Same(context, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal("Current target", ((AgentEditorModel)context!.Model).Name);
        Assert.DoesNotContain(harness.Context.Services.GetRequiredService<NotificationService>().Messages,
            message => message.Detail?.Contains("Stale load failure", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Reset_uses_create_target_without_changing_catalog_selection() {
        var reads = new AgentEditorReadFixture();
        await using var harness = await CreateHarnessAsync(reads);
        var agent = (await harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>().ListAgentsAsync(false)).First();
        reads.Draft = AgentEditorModel.FromDefinition(agent);
        var dialogHost = harness.Context.Render<DialogHost>();
        var catalog = harness.Context.Render<AgentCatalogHost>(parameters => parameters
            .Add(component => component.InitialAgents, new[] { agent })
            .Add(component => component.InitialProviders, Array.Empty<ProviderProfile>())
            .Add(component => component.InitialTeams, Array.Empty<AgentTeamDefinition>())
            .Add(component => component.SkipCatalogRepair, true));
        var opened = catalog.WaitForElement("[data-testid='agents-catalog-card']").DoubleClickAsync();
        var editor = dialogHost.WaitForComponent<AgentDetailsDialog>();
        editor.WaitForElement("[data-testid='agents-catalog-name']");
        var oldContext = editor.FindComponent<EditForm>().Instance.EditContext;
        editor.FindAll("button").Single(button => button.TextContent.Trim() == "Clear").Click();
        Assert.True(editor.Instance.CurrentTarget.IsNew);
        Assert.True(Assert.Single(catalog.Instance.OpenEditorTargets).IsNew);
        Assert.NotSame(oldContext, editor.FindComponent<EditForm>().Instance.EditContext);
        Assert.Null(((AgentEditorModel)editor.FindComponent<EditForm>().Instance.EditContext!.Model).Id);
        Assert.Equal(agent.Id, catalog.FindComponent<AgentCatalogPanel>().Instance.Selection.AgentId);
        await harness.Context.Services.GetRequiredService<DialogService>().CloseAsync(new AgentDetailsDialogResult(agent.Id, Deleted: true));
        await opened;
        await catalog.InvokeAsync(async () => {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (catalog.Instance.OpenEditorTargets.Count > 0) {
                await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
            }
        });
        Assert.Empty(catalog.Instance.OpenEditorTargets);
        Assert.Equal(agent.Id, catalog.FindComponent<AgentCatalogPanel>().Instance.Selection.AgentId);
    }

    [Fact]
    public async Task Reset_invalidates_pending_project_result_and_allows_new_request() {
        var pending = new TaskCompletionSource<IReadOnlyList<AgentEditorProject>>();
        var reads = new AgentEditorReadFixture { Draft = new() { ProjectStructureAccess = new() { CanRead = true } } };
        reads.ReadProjects = _ => reads.ProjectReads == 1 ? pending.Task : Task.FromResult<IReadOnlyList<AgentEditorProject>>([]);
        await using var harness = await CreateHarnessAsync(reads);
        var cut = harness.Context.Render<AgentDetailsDialog>(parameters => parameters.Add(component => component.Section, AgentEditorSection.ProjectStructureAccess));
        cut.Find("[data-testid='agents-catalog-project-structure-load']").Click();
        cut.FindComponent<StickyActionFooter>().FindAll("button").Single(button => button.TextContent.Trim() == "Clear").Click();
        await cut.InvokeAsync(() => pending.SetResult([new(Guid.NewGuid(), "Stale project")]));
        cut.Render(parameters => parameters.Add(component => component.Section, AgentEditorSection.ProjectStructureAccess));
        Assert.Equal(1, reads.ProjectReads);
        Assert.DoesNotContain("Stale project", cut.Markup);
        cut.Find("[data-testid='agents-catalog-project-structure-read']").Change(true);
        cut.WaitForAssertion(() => Assert.Equal(2, reads.ProjectReads));
        Assert.DoesNotContain("Stale project", cut.Markup);
    }

    [Fact]
    public async Task Dispose_ignores_delayed_load() {
        var pending = new TaskCompletionSource<AgentEditorLoadResult>();
        var reads = new AgentEditorReadFixture();
        CancellationToken capturedToken = default;
        reads.Load = (_, token) => {
            capturedToken = token;
            return pending.Task;
        };
        await using var harness = await CreateHarnessAsync(reads);
        var cut = harness.Context.Render<AgentDetailsDialog>();
        var renders = cut.RenderCount;
        await cut.InvokeAsync(() => {
            cut.Instance.Dispose();
            pending.SetResult(reads.Result(new() { Id = Guid.NewGuid(), Name = "Disposed result" }));
        });
        cut.WaitForState(() => cut.RenderCount > renders);
        Assert.True(capturedToken.IsCancellationRequested);
        Assert.True(cut.Instance.CurrentTarget.IsNew);
        Assert.Empty(cut.FindAll("[data-testid='agents-catalog-name']"));
    }

    [Fact]
    public async Task Separate_editors_keep_independent_drafts() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var agent = (await harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>().ListAgentsAsync(false)).First();
        var first = harness.Context.Render<AgentDetailsDialog>(parameters => parameters.Add(component => component.AgentId, agent.Id));
        var second = harness.Context.Render<AgentDetailsDialog>(parameters => parameters.Add(component => component.AgentId, agent.Id));
        first.WaitForElement("[data-testid='agents-catalog-name']");
        second.WaitForElement("[data-testid='agents-catalog-name']");
        var firstContext = first.FindComponent<EditForm>().Instance.EditContext!;
        var secondContext = second.FindComponent<EditForm>().Instance.EditContext!;
        Assert.NotSame(firstContext.Model, secondContext.Model);
        first.Find("[data-testid='agents-catalog-name']").Change("Only first draft");
        Assert.Equal(agent.Name, ((AgentEditorModel)secondContext.Model).Name);
        Assert.Equal(agent.UpdatedAtUtc, ((AgentEditorModel)firstContext.Model).ExpectedUpdatedAtUtc);
        Assert.Equal(agent.UpdatedAtUtc, ((AgentEditorModel)secondContext.Model).ExpectedUpdatedAtUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reference_failures_are_independent(bool failSecrets) {
        var reads = new AgentEditorReadFixture {
            Draft = new() { Name = "Keep my draft" },
            ProviderError = failSecrets ? null : "Provider probe failure.",
            SecretError = failSecrets ? "Secret probe failure." : null
        };
        await using var harness = await CreateHarnessAsync(reads);
        var cut = harness.Context.Render<AgentDetailsDialog>();
        Assert.Equal("Keep my draft", ((AgentEditorModel)cut.FindComponent<EditForm>().Instance.EditContext!.Model).Name);
        var messages = harness.Context.Services.GetRequiredService<NotificationService>().Messages;
        Assert.Contains(messages, message => message.Summary == (failSecrets ? "Secrets failed to load" : "Providers failed to load"));
        Assert.DoesNotContain(messages, message => message.Summary == "Agent editor failed to load");
        Assert.DoesNotContain(messages, message => message.Summary == (failSecrets ? "Providers failed to load" : "Secrets failed to load"));
    }

    [Fact]
    public async Task Projects_remain_lazy_and_retry_does_not_replace_draft() {
        var project = new AgentEditorProject(Guid.NewGuid(), "Available project");
        var reads = new AgentEditorReadFixture { Draft = new() { ProjectStructureAccess = new() { CanRead = true } } };
        reads.ReadProjects = _ => reads.ProjectReads == 1
            ? Task.FromException<IReadOnlyList<AgentEditorProject>>(new InvalidOperationException("Project probe failure."))
            : Task.FromResult<IReadOnlyList<AgentEditorProject>>([project]);
        await using var harness = await CreateHarnessAsync(reads);
        var cut = harness.Context.Render<AgentDetailsDialog>();
        cut.Find("[data-testid='agents-catalog-name']").Change("Draft survives retry");
        var context = cut.FindComponent<EditForm>().Instance.EditContext;
        cut.Render(parameters => parameters.Add(component => component.Section, AgentEditorSection.ProjectStructureAccess));
        Assert.Equal(0, reads.ProjectReads);
        cut.Find("[data-testid='agents-catalog-project-structure-load']").Click();
        cut.WaitForAssertion(() => Assert.Contains("Project probe failure", cut.Markup));
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Retry project list").Click();
        cut.WaitForElement("[data-testid='agents-catalog-project-structure-projects']");
        Assert.Equal(2, reads.ProjectReads);
        Assert.Same(context, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal("Draft survives retry", ((AgentEditorModel)context!.Model).Name);
        Assert.Contains(project.Name, cut.Markup);
    }

    private static Task<ComponentTestHarness> CreateHarnessAsync(AgentEditorReadFixture reads, EmptyMemoryProfiles? memory = null)
        => ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IAgentEditorReads>(reads);
            services.AddSingleton<IMemoryProviderProfileStore>(memory ?? new());
            services.RemoveAll<IMemoryProviderDriver>();
        });

    private sealed class EmptyMemoryProfiles : IMemoryProviderProfileStore {
        public int Reads { get; private set; }
        public Task UpsertAsync(MemoryProviderProfile profile, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Editor rendering must not write a memory profile.");
        public Task<MemoryProviderProfile?> GetAsync(MemoryProviderInstanceId providerId, CancellationToken cancellationToken = default)
            => Task.FromResult<MemoryProviderProfile?>(null);
        public Task<IReadOnlyList<MemoryProviderProfile>> ListAsync(CancellationToken cancellationToken = default) {
            Reads++;
            return Task.FromResult<IReadOnlyList<MemoryProviderProfile>>([]);
        }
    }
}
