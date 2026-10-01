using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages;
using CanDoItAll.Modules.Projects.Pages.Components;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectsEditorMutationTests {
    [Fact]
    public async Task Portfolio_context_is_ready_but_cannot_certify_a_pending_or_failed_exact_editor() {
        var controls = new Controls();
        await using var harness = await Harness(controls);
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        Guid id = (await projects.SaveAsync(new() { Name = "Acquired context" })).Value;
        var cut = harness.Context.Render<ProjectsPage>();
        cut.WaitForElement("[data-testid='project-card']");
        var provider = cut.FindComponent<ProjectsAgentChatContextProvider>();
        Assert.Equal(AgentChatContextAccessState.Ready, provider.Instance.ContextAccessState);
        controls.HoldNextRead = 1;
        var board = cut.FindComponent<ProjectsBoard>();
        Task pending = cut.InvokeAsync(() => board.Instance.OpenProjectPreview.InvokeAsync(id));
        await using var operation = new PendingOperation(pending, controls);
        await controls.Started.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await cut.InvokeAsync(() => provider.Instance.RefreshRequested.InvokeAsync());
        Assert.Equal(AgentChatContextAccessState.Loading, provider.Instance.ContextAccessState);
        controls.Release.TrySetResult();
        await pending;
        Assert.Equal(AgentChatContextAccessState.Ready, provider.Instance.ContextAccessState);
        await cut.InvokeAsync(() => board.Instance.OpenProjectPreview.InvokeAsync(Guid.NewGuid()));
        await cut.InvokeAsync(() => provider.Instance.RefreshRequested.InvokeAsync());
        Assert.Equal(AgentChatContextAccessState.Failed, provider.Instance.ContextAccessState);
        Assert.Empty(cut.FindAll("[data-testid='projects-editor-modal'], [data-testid='projects-detail-modal']"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Auxiliary_selection_reads_cannot_replace_a_newer_portfolio(bool hierarchy) {
        var controls = new Controls();
        await using var harness = await Harness(controls);
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var cut = harness.Context.Render<ProjectsPage>();
        await cut.InvokeAsync(() => cut.FindComponent<ProjectsAgentChatContextProvider>().Instance.RefreshRequested.InvokeAsync());
        Guid id = (await projects.SaveAsync(new() { Name = "Earlier snapshot" })).Value;
        controls.HeldPortfolioReads = hierarchy ? 2 : 1;
        var board = cut.FindComponent<ProjectsBoard>();
        Task pending = cut.InvokeAsync(() => (hierarchy ? board.Instance.OpenHierarchyModal : board.Instance.OpenProjectFiles).InvokeAsync(id));
        await using var operation = new PendingOperation(pending, controls);
        await controls.Started.Task.WaitAsync(TimeSpan.FromSeconds(30));
        var model = await projects.GetAsync(id);
        model.Name = "Newer confirmed portfolio";
        Assert.True((await projects.SaveAsync(model)).IsSuccess);
        await cut.InvokeAsync(() => cut.FindComponent<ProjectsAgentChatContextProvider>().Instance.RefreshRequested.InvokeAsync());
        Assert.Equal(model.Name, Assert.Single(board.Instance.ProjectSummaries).Name);
        controls.Release.TrySetResult();
        await pending;
        Assert.Equal(model.Name, Assert.Single(board.Instance.ProjectSummaries).Name);
        Assert.Equal(model.Name, Assert.Single(cut.FindComponent<ProjectsAgentChatContextProvider>().Instance.ProjectSummaries).Name);
    }

    [Fact]
    public async Task Retired_acquisition_and_agent_completion_cannot_replace_the_current_draft() {
        var controls = new Controls();
        await using var harness = await Harness(controls);
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        Guid first = (await projects.SaveAsync(new() { Name = "Acquisition A" })).Value;
        Guid second = (await projects.SaveAsync(new() { Name = "Acquisition B" })).Value;
        var cut = harness.Context.Render<ProjectsPage>();
        cut.WaitForElement("[data-testid='project-card']");
        controls.HoldNextRead = 1;
        var board = cut.FindComponent<ProjectsBoard>();
        Task pending = cut.InvokeAsync(() => board.Instance.OpenProjectPreview.InvokeAsync(first));
        await using var operation = new PendingOperation(pending, controls);
        await controls.Started.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await cut.InvokeAsync(() => board.Instance.OpenProjectPreview.InvokeAsync(second));
        await cut.InvokeAsync(() => board.Instance.OpenProjectPreview.InvokeAsync(first));
        var editor = cut.FindComponent<ProjectModalHost>();
        var acquired = editor.Instance.Draft;
        await cut.InvokeAsync(() => editor.Instance.SwitchToEditor.InvokeAsync());
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-name-input']").InputAsync(new() { Value = "Unsubmitted A" }));
        controls.Release.TrySetResult();
        await pending;
        var provider = cut.FindComponent<ProjectsAgentChatContextProvider>();
        await cut.InvokeAsync(() => provider.Instance.RefreshRequested.InvokeAsync());
        Assert.Same(acquired, cut.FindComponent<ProjectModalHost>().Instance.Draft);
        Assert.Equal("Unsubmitted A", acquired.Model.Name);
        Assert.Equal("Acquisition A", (await projects.GetAsync(first)).Name);
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-modal-close-button']").ClickAsync(new()));
        await cut.InvokeAsync(() => provider.Instance.RefreshRequested.InvokeAsync());
        Assert.Empty(cut.FindAll("[data-testid='projects-editor-modal'], [data-testid='projects-detail-modal']"));
    }

    [Fact]
    public async Task Delayed_partial_deletion_preserves_successor_and_exact_retry_even_when_the_list_read_fails() {
        var controls = new Controls();
        var participant = new HeldDeletion(controls);
        await using var harness = await Harness(controls, participant);
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        Guid id = (await projects.SaveAsync(new() { Name = "Deleted A" })).Value;
        var cut = harness.Context.Render<ProjectsPage>();
        cut.WaitForElement("[data-testid='project-card']");
        await cut.InvokeAsync(() => cut.FindComponent<ProjectsBoard>().Instance.OpenProjectPreview.InvokeAsync(id));
        var editor = cut.FindComponent<ProjectModalHost>();
        Task pending = cut.InvokeAsync(() => editor.Instance.Delete.InvokeAsync());
        await using var operation = new PendingOperation(pending, controls);
        await controls.Started.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Null((await projects.GetAsync(id)).Id);
        await cut.InvokeAsync(() => editor.Instance.Close.InvokeAsync());
        await NewAsync(cut, "Successor B");
        var successor = cut.FindComponent<ProjectModalHost>().Instance.Draft;
        controls.Release.TrySetResult();
        await pending;
        Assert.Same(successor, cut.FindComponent<ProjectModalHost>().Instance.Draft);
        Assert.Equal("Successor B", successor.Model.Name);
        Assert.Contains("project was deleted", cut.Find("[data-testid='project-deletion-notice']").TextContent);
        var target = Assert.Single(cut.FindComponent<ProjectDeletionStatusSurface>().Instance.Pending).Target;
        Assert.Equal(id, target.ProjectId);
        Assert.Equal(participant.Id, target.ParticipantId);
        Assert.Equal(participant.RecoveryId, target.RecoveryId);
        await cut.InvokeAsync(() => cut.FindAll("button").Single(button => button.TextContent == "Retry project reads").ClickAsync(new()));
        Assert.Equal(1, participant.Completions);
        await cut.InvokeAsync(() => cut.FindComponent<ProjectDeletionStatusSurface>().Instance.Retry.InvokeAsync(target));
        Assert.Equal(2, participant.Completions);
        Assert.Empty(cut.FindComponent<ProjectDeletionStatusSurface>().Instance.Pending);
        Assert.Same(successor, cut.FindComponent<ProjectModalHost>().Instance.Draft);
    }

    [Fact]
    public async Task Held_committed_save_admits_once_and_reconciles_actual_child_ids_without_losing_later_input() {
        var controls = new Controls { HoldActivity = true };
        await using var harness = await Harness(controls);
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var cut = harness.Context.Render<ProjectsPage>();
        await NewAsync(cut, "Submitted project");
        var draft = cut.FindComponent<ProjectModalHost>().Instance.Draft;
        await cut.InvokeAsync(() => {
            draft.Model.Phases.Add(new() { Name = "First phase" });
            draft.Model.Phases.Add(new() { Name = "Second phase" });
            draft.Model.Options[0].OptionName = "C#";
        });
        var originalPhase = draft.Model.Phases[1];
        Task pending = cut.InvokeAsync(() => cut.Find("[data-testid='project-save-open-button']").ClickAsync(new()));
        await using var operation = new PendingOperation(pending, controls);
        await controls.Started.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-name-input']").InputAsync(new() { Value = "Later unblurred name" }));
        await cut.InvokeAsync(() => {
            draft.Model.Phases.RemoveAt(0);
            draft.Model.Phases.Insert(0, new() { Name = "Replacement phase" });
            originalPhase.Goal = "Later phase goal";
            draft.Model.Options[0].Notes = "Later option note";
        });
        await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        Assert.Single(await projects.ListAsync());
        Assert.Null(draft.Model.Id);
        controls.Release.TrySetResult();
        await pending;
        Guid id = Assert.IsType<Guid>(draft.Model.Id);
        var saved = await projects.GetAsync(id);
        Assert.Equal("Submitted project", saved.Name);
        Assert.Equal("Later unblurred name", draft.Model.Name);
        Assert.Null(draft.Model.Phases[0].Id);
        Assert.Equal(saved.Phases[1].Id, originalPhase.Id);
        Assert.Equal("Later phase goal", originalPhase.Goal);
        Assert.Equal("Later option note", draft.Model.Options[0].Notes);
        Assert.Equal(saved.Options[0].Id, draft.Model.Options[0].Id);
        Assert.Same(draft.EditContext, cut.FindComponent<Microsoft.AspNetCore.Components.Forms.EditForm>().Instance.EditContext);
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-save-button']").ClickAsync(new()));
        var repeated = await projects.GetAsync(id);
        Assert.Equal(originalPhase.Id, repeated.Phases[1].Id);
        Assert.Equal(draft.Model.Options[0].Id, repeated.Options[0].Id);
        Assert.Equal("Later unblurred name", repeated.Name);
        Assert.Single(await projects.ListAsync());
        Assert.Equal(2, controls.ProjectActivities);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Known_project_and_seed_facts_survive_failed_read_or_unknown_seed_acknowledgement(bool unknownSeed) {
        var controls = new Controls { FailReadAfterSeed = !unknownSeed, LoseSeedAcknowledgement = unknownSeed };
        await using var harness = await Harness(controls);
        var cut = harness.Context.Render<ProjectsPage>();
        await NewAsync(cut, "Committed with separate seed");
        var draft = cut.FindComponent<ProjectModalHost>().Instance.Draft;
        draft.StarterObjects.Add(new() { Title = "Native batch note" });
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-save-button']").ClickAsync(new()));
        Guid id = Assert.IsType<Guid>(draft.Model.Id);
        Assert.NotNull(draft.Model.ExpectedProjectAdmission);
        Assert.Single(await ObjectsAsync(harness, id));
        Assert.Equal(1, controls.SeedCalls);
        if (unknownSeed) {
            Assert.Equal(ProjectEditorMutationState.SeedOutcomeUnknown, draft.Mutation);
            Assert.Contains("Project saved", draft.Message);
            await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        } else {
            Assert.Equal(ProjectEditorMutationState.Ready, draft.Mutation);
            Assert.Empty(draft.StarterObjects);
            Assert.NotEmpty(cut.FindAll("[data-testid='projects-read-error']"));
            await cut.InvokeAsync(() => cut.FindAll("button").Single(button => button.TextContent == "Retry project reads").ClickAsync(new()));
        }
        Assert.Equal(1, controls.SeedCalls);
        Assert.Single(await ObjectsAsync(harness, id));
        Assert.Single(await harness.Context.Services.GetRequiredService<ProjectsService>().ListAsync());
    }

    [Fact]
    public async Task Seed_transaction_refuses_the_original_admission_after_same_id_recreation() {
        var controls = new Controls { HoldSeed = true };
        await using var harness = await Harness(controls);
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var cut = harness.Context.Render<ProjectsPage>();
        await NewAsync(cut, "Original seed lifetime");
        var draft = cut.FindComponent<ProjectModalHost>().Instance.Draft;
        draft.StarterObjects.Add(new() { Title = "Must not enter successor" });
        Task pending = cut.InvokeAsync(() => cut.Find("[data-testid='project-save-button']").ClickAsync(new()));
        await using var operation = new PendingOperation(pending, controls);
        await controls.Started.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Guid id = Assert.IsType<Guid>(draft.Model.Id);
        var original = draft.Model.ExpectedProjectAdmission;
        await projects.DeleteAsync(id);
        Assert.True((await projects.CreateAsync(id, new() { Name = "Successor lifetime" })).IsSuccess);
        controls.Release.TrySetResult();
        await pending;
        Assert.IsType<ProjectWriteAdmissionRejectedException>(controls.SeedFailure);
        Assert.Empty(await ObjectsAsync(harness, id));
        Assert.Equal(original, draft.Model.ExpectedProjectAdmission);
        Assert.NotEqual(original!.LifetimeId, (await projects.GetAsync(id)).ExpectedLifetimeId);
        Assert.Equal(1, controls.SeedCalls);
    }

    [Fact]
    public async Task Retired_save_does_not_navigate_close_or_clear_a_successor_draft_and_its_plan() {
        var controls = new Controls { HoldSeed = true };
        await using var harness = await Harness(controls);
        var cut = harness.Context.Render<ProjectsPage>();
        await NewAsync(cut, "Retiring project");
        var oldDraft = cut.FindComponent<ProjectModalHost>().Instance.Draft;
        oldDraft.StarterObjects.Add(new() { Title = "Original plan" });
        Task pending = cut.InvokeAsync(() => cut.Find("[data-testid='project-save-open-button']").ClickAsync(new()));
        await using var operation = new PendingOperation(pending, controls);
        await controls.Started.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-modal-close-button']").ClickAsync(new()));
        await NewAsync(cut, "Successor unsaved draft");
        var successor = cut.FindComponent<ProjectModalHost>().Instance.Draft;
        var newPlan = new StarterObjectDraft { Title = "Successor plan" };
        successor.StarterObjects.Add(newPlan);
        string navigation = harness.Context.Services.GetRequiredService<NavigationManager>().Uri;
        controls.Release.TrySetResult();
        await pending;
        Assert.Same(successor, cut.FindComponent<ProjectModalHost>().Instance.Draft);
        Assert.Same(newPlan, Assert.Single(successor.StarterObjects));
        Assert.Equal("Successor unsaved draft", successor.Model.Name);
        Assert.Null(successor.Model.Id);
        Assert.Equal(navigation, harness.Context.Services.GetRequiredService<NavigationManager>().Uri);
        Assert.Single(await ObjectsAsync(harness, oldDraft.Model.Id!.Value));
        Assert.Empty(oldDraft.StarterObjects);
    }

    [Fact]
    public async Task Reopened_same_target_cannot_mutate_while_the_original_seed_outcome_is_pending() {
        var controls = new Controls { HoldSeed = true };
        await using var harness = await Harness(controls);
        var cut = harness.Context.Render<ProjectsPage>();
        await NewAsync(cut, "Pending same target");
        var draft = cut.FindComponent<ProjectModalHost>().Instance.Draft;
        draft.StarterObjects.Add(new() { Title = "One seed" });
        Task pending = cut.InvokeAsync(() => cut.Find("[data-testid='project-save-button']").ClickAsync(new()));
        await using var operation = new PendingOperation(pending, controls);
        await controls.Started.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Guid id = draft.Model.Id!.Value;
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-modal-close-button']").ClickAsync(new()));
        await cut.InvokeAsync(() => harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/projects?projectId={id:D}"));
        cut.WaitForElement("[data-testid='projects-detail-modal']");
        var reopened = cut.FindComponent<ProjectModalHost>();
        await cut.InvokeAsync(() => reopened.Instance.Save.InvokeAsync());
        Assert.Contains("previous operation", reopened.Instance.Draft.Message);
        Assert.Equal(1, controls.SeedCalls);
        controls.Release.TrySetResult();
        await pending;
        Assert.Equal(id, reopened.Instance.Editor.Id);
        Assert.Single(await ObjectsAsync(harness, id));
    }

    private static Task<ComponentTestHarness> Harness(Controls controls, IProjectDeletionParticipant? participant = null) => ComponentTestHarness.CreateAsync(services => {
        if (participant is not null) {
            services.AddSingleton(participant);
        }
        Decorate<IActivityStream>(services, inner => new HeldActivity(inner, controls));
        Decorate<IDbContextFactory<ProjectsDbContext>>(services, inner => new ControlledFactory(inner, controls));
        Decorate<IProjectPartyIntegrationBridge>(services, inner => new HeldPortfolioContext(inner, controls));
        services.RemoveAll<IAdmittedProjectWorkbenchSeedService>();
        services.AddScoped<IAdmittedProjectWorkbenchSeedService>(provider => new ControlledSeed(provider.GetRequiredService<ProjectWorkbenchService>(), controls));
    });

    private static void Decorate<T>(IServiceCollection services, Func<T, T> decorate) where T : class {
        var descriptor = services.Last(item => item.ServiceType == typeof(T));
        services.RemoveAll<T>();
        services.Add(ServiceDescriptor.Describe(typeof(T), provider => decorate((T)(descriptor.ImplementationInstance
            ?? descriptor.ImplementationFactory?.Invoke(provider)
            ?? ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!))), descriptor.Lifetime));
    }

    private static async Task NewAsync(IRenderedComponent<ProjectsPage> cut, string name) {
        await cut.InvokeAsync(() => cut.Find("[data-testid='projects-new-button']").ClickAsync(new()));
        cut.WaitForElement("[data-testid='project-name-input']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-name-input']").InputAsync(new() { Value = name }));
    }

    private static async Task<ProjectObjectRecord[]> ObjectsAsync(ComponentTestHarness harness, Guid id) {
        await using var context = await harness.Context.Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        return await context.Set<ProjectObjectRecord>().AsNoTracking().Where(row => row.ProjectId == id && !row.IsSystemManaged).ToArrayAsync();
    }

    private sealed class Controls {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool HoldActivity { get; set; }
        public bool HoldSeed { get; init; }
        public bool FailReadAfterSeed { get; init; }
        public bool LoseSeedAcknowledgement { get; init; }
        public int FailNextRead;
        public int HoldNextRead;
        public int HeldPortfolioReads;
        public int SeedCalls { get; set; }
        public int ProjectActivities { get; set; }
        public Exception? SeedFailure { get; set; }
    }

    private sealed class PendingOperation(Task pending, Controls controls) : IAsyncDisposable {
        public async ValueTask DisposeAsync() {
            controls.Release.TrySetResult();
            await pending;
        }
    }

    private sealed class HeldActivity(IActivityStream inner, Controls controls) : IActivityStream {
        public async Task RecordAsync(ActivityWriteRequest request, CancellationToken cancellationToken = default) {
            await inner.RecordAsync(request, cancellationToken);
            if (request.Category != "projects" || request.Action is not ("create" or "update")) {
                return;
            }
            controls.ProjectActivities++;
            if (controls.HoldActivity) {
                controls.HoldActivity = false;
                controls.Started.TrySetResult();
                await controls.Release.Task;
            }
        }
    }

    private sealed class ControlledSeed(ProjectWorkbenchService inner, Controls controls) : IAdmittedProjectWorkbenchSeedService {
        public async Task SeedProjectObjectsAsync(ProjectWriteAdmission project, IReadOnlyCollection<ProjectObjectSeedDraft> seeds, CancellationToken cancellationToken = default) {
            controls.SeedCalls++;
            if (controls.HoldSeed) {
                controls.Started.TrySetResult();
                await controls.Release.Task;
            }
            try {
                await inner.SeedProjectObjectsAsync(project, seeds, cancellationToken);
            } catch (Exception exception) {
                controls.SeedFailure = exception;
                throw;
            }
            if (controls.LoseSeedAcknowledgement) {
                throw new IOException("Injected lost seed acknowledgement after the native transaction committed.");
            }
            if (controls.FailReadAfterSeed) {
                Interlocked.Exchange(ref controls.FailNextRead, 1);
            }
        }
    }

    private sealed class ControlledFactory(IDbContextFactory<ProjectsDbContext> inner, Controls controls) : IDbContextFactory<ProjectsDbContext> {
        public ProjectsDbContext CreateDbContext() => inner.CreateDbContext();
        public async Task<ProjectsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            if (Interlocked.Exchange(ref controls.HoldNextRead, 0) == 1) {
                controls.Started.TrySetResult();
                await controls.Release.Task;
            }
            if (Interlocked.Exchange(ref controls.FailNextRead, 0) == 1) {
                throw new IOException("Injected optional Projects read failure after native effects committed.");
            }
            return await inner.CreateDbContextAsync(cancellationToken);
        }
    }

    private sealed class HeldPortfolioContext(IProjectPartyIntegrationBridge inner, Controls controls) : IProjectPartyIntegrationBridge {
        public async Task<IReadOnlyDictionary<Guid, ProjectPortfolioPartyContext>> GetPortfolioContextsAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken = default) {
            var result = await inner.GetPortfolioContextsAsync(projectIds, cancellationToken);
            if (controls.HeldPortfolioReads > 0) {
                if (Interlocked.Decrement(ref controls.HeldPortfolioReads) == 0) {
                    controls.Started.TrySetResult();
                }
                await controls.Release.Task;
            }
            return result;
        }

        public Task<IReadOnlyList<ProjectPartyOption>> ListPartyOptionsAsync(Guid id, CancellationToken cancellationToken = default)
            => inner.ListPartyOptionsAsync(id, cancellationToken);
        public Task<ProjectPartyOption?> GetPartyOptionAsync(Guid id, CancellationToken cancellationToken = default)
            => inner.GetPartyOptionAsync(id, cancellationToken);
        public Task<IReadOnlyList<ProjectPartyAssignmentDetail>> ListAssignmentsDetailedAsync(Guid id, CancellationToken cancellationToken = default)
            => inner.ListAssignmentsDetailedAsync(id, cancellationToken);
        public Task<IReadOnlyList<ProjectPartyAssignmentDetail>> ListAssignmentsDetailedAsync(Guid id, IReadOnlyCollection<ProjectPartyAssignmentRole> roles, CancellationToken cancellationToken = default)
            => inner.ListAssignmentsDetailedAsync(id, roles, cancellationToken);
        public Task<IReadOnlyList<ProjectWorkItemAssigneeBinding>> ListWorkItemAssigneeBindingsAsync(Guid id, CancellationToken cancellationToken = default)
            => inner.ListWorkItemAssigneeBindingsAsync(id, cancellationToken);
        public Task<Result<Guid>> SaveAssignmentAsync(ProjectPartyAssignmentUpsertRequest request, CancellationToken cancellationToken = default)
            => inner.SaveAssignmentAsync(request, cancellationToken);
        public Task<Result> ReplaceNodeAssignmentsAsync(Guid id, ProjectNodeReference node, IReadOnlyList<ProjectPartyAssignmentUpsertRequest> assignments, IReadOnlyList<ProjectPartyAssignmentRole> roles, CancellationToken cancellationToken = default, ProjectWriteAdmission? expectedProjectAdmission = null)
            => inner.ReplaceNodeAssignmentsAsync(id, node, assignments, roles, cancellationToken, expectedProjectAdmission);
        public Task DeleteAssignmentAsync(Guid id, CancellationToken cancellationToken = default, ProjectAssignmentReference? expectedReference = null)
            => inner.DeleteAssignmentAsync(id, cancellationToken, expectedReference);
        public Task DeleteAssignmentsForNodesAsync(Guid id, IReadOnlyCollection<ProjectNodeReference> nodes, CancellationToken cancellationToken = default, ProjectAssignmentReference? expectedReference = null)
            => inner.DeleteAssignmentsForNodesAsync(id, nodes, cancellationToken, expectedReference);
        public Task DeleteAssignmentsForProjectAsync(Guid id, CancellationToken cancellationToken = default, ProjectAssignmentReference? expectedReference = null)
            => inner.DeleteAssignmentsForProjectAsync(id, cancellationToken, expectedReference);
        public Task MoveAssignmentsToProjectAsync(ProjectPartyAssignmentMoveOperationId operation, Guid source, IReadOnlyCollection<ProjectNodeReference> nodes, Guid target, CancellationToken cancellationToken = default, ProjectAssignmentReference? sourceReference = null, ProjectWriteAdmission? expectedTargetAdmission = null)
            => inner.MoveAssignmentsToProjectAsync(operation, source, nodes, target, cancellationToken, sourceReference, expectedTargetAdmission);
        public Task<Result<ProjectPartyQuickCreateResult>> CreatePartyAsync(ProjectPartyQuickCreateRequest request, CancellationToken cancellationToken = default)
            => inner.CreatePartyAsync(request, cancellationToken);
    }

    private sealed class HeldDeletion(Controls controls) : IProjectDeletionParticipant {
        public ProjectDeletionParticipantId Id { get; } = new("held-projects-cleanup");
        public Guid RecoveryId { get; } = Guid.NewGuid();
        public IReadOnlyCollection<ProjectDeletionPreparationScopeKey> PreparationScopeKeys { get; } = [];
        public int Completions { get; private set; }
        private Guid projectId;
        private bool pending;

        public Task<ProjectDeletionParticipantPreparation?> PrepareAsync(Guid id, CancellationToken cancellationToken = default) {
            projectId = id;
            pending = true;
            return Task.FromResult<ProjectDeletionParticipantPreparation?>(new(id, RecoveryId));
        }

        public async Task<ProjectDeletionParticipantCompletion> CompleteAsync(ProjectDeletionParticipantPreparation preparation, CancellationToken cancellationToken = default) {
            Assert.Equal(projectId, preparation.ProjectId);
            Assert.Equal(RecoveryId, preparation.RecoveryId);
            Completions++;
            if (Completions == 1) {
                controls.Started.TrySetResult();
                await controls.Release.Task;
                controls.FailNextRead = 1;
                throw new IOException("Injected participant failure after native project deletion.");
            }
            pending = false;
            return ProjectDeletionParticipantCompletion.Empty(RecoveryId);
        }

        public Task<IReadOnlyList<ProjectDeletionParticipantRecovery>> ListPendingRecoveriesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectDeletionParticipantRecovery>>(pending
                ? [new(projectId, RecoveryId, ProjectDeletionRecoveryStatus.Failed, true, null, "Retry this exact participant operation.")] : []);

        public Task<IReadOnlyList<ProjectDeletionParticipantCompletionNotice>> ListCompletionNoticesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectDeletionParticipantCompletionNotice>>([]);
    }
}
