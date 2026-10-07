using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectStructurePartyQuickCreateTests(ITestOutputHelper output) {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Created_party_cannot_select_or_erase_a_successor_participant(bool returnToOriginal) {
        var gate = new PartyGate();
        await using var harness = await CreateHarnessAsync(gate);
        var (projectId, first, second) = await CreateNodesAsync(harness);
        var cut = Render(harness, projectId);
        await SelectAsync(cut, first.Id);
        await DraftAsync(cut, "Original native party");
        gate.HoldCreate = true;
        var pending = cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-participant-quick-create']").ClickAsync(new MouseEventArgs()));
        try {
            await gate.Created.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await SelectAsync(cut, second.Id);
            if (returnToOriginal) {
                await SelectAsync(cut, first.Id);
            }
            await DraftAsync(cut, "Successor draft survives");
        } finally {
            gate.Release.TrySetResult();
        }
        await pending;

        var directory = await harness.Context.Services.GetRequiredService<PartyDirectoryService>().ListPartiesAsync();
        var accepted = Assert.Single(directory, item => item.DisplayName == "Original native party");
        Assert.Equal(gate.AcceptedPartyId, accepted.Id);
        Assert.Equal(1, gate.CreateCalls);
        Assert.Empty(await gate.Inner!.ListAssignmentsDetailedAsync(projectId));
        output.WriteLine($"WB5-P1 original native PartyId={accepted.Id:D}; ProjectId={projectId:D}; Node={first.Id}; create calls={gate.CreateCalls}");
        cut.WaitForAssertion(() => Assert.Equal("Successor draft survives", cut.Find("[data-testid='project-structure-participant-quick-name']").GetAttribute("value")));
        Assert.DoesNotContain("Directory party created", cut.Find("[data-testid='project-structure-party-editor']").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain(cut.FindAll("[aria-pressed='true']"), element => element.GetAttribute("data-testid")?.Contains(accepted.Id.ToString("N"), StringComparison.Ordinal) == true);
        var structure = await harness.Context.Services.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(projectId);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(Assert.Single(structure.Nodes, item => item.Id == first.Id)));
        Assert.Equal(JsonSerializer.Serialize(second), JsonSerializer.Serialize(Assert.Single(structure.Nodes, item => item.Id == second.Id)));
    }

    [Fact]
    public async Task Accepted_identity_survives_failed_options_and_retry_only_reads() {
        var gate = new PartyGate { FailOptionsAfterCreate = true };
        await using var harness = await CreateHarnessAsync(gate);
        var (projectId, first, _) = await CreateNodesAsync(harness);
        var cut = Render(harness, projectId);
        await SelectAsync(cut, first.Id);
        await DraftAsync(cut, "Accepted before lookup failure");
        var error = await Record.ExceptionAsync(() => cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-participant-quick-create']").ClickAsync(new MouseEventArgs())));
        var accepted = Assert.Single(await harness.Context.Services.GetRequiredService<PartyDirectoryService>().ListPartiesAsync(), item => item.DisplayName == "Accepted before lookup failure");
        output.WriteLine($"WB5-P1 read-back failure after PartyId={accepted.Id:D}; create calls={gate.CreateCalls}; escaped={error?.GetType().Name ?? "none"}");
        Assert.Null(error);
        cut.WaitForAssertion(() => Assert.Contains(accepted.Id.ToString("D"), cut.Find("[data-testid='project-structure-party-editor']").TextContent, StringComparison.Ordinal));
        gate.FailOptionsAfterCreate = false;
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-party-retry']").ClickAsync(new MouseEventArgs()));
        Assert.Equal(1, gate.CreateCalls);
        Assert.Single(await harness.Context.Services.GetRequiredService<PartyDirectoryService>().ListPartiesAsync(), item => item.Id == accepted.Id);
        Assert.Empty(await gate.Inner!.ListAssignmentsDetailedAsync(projectId));
    }

    [Fact]
    public async Task Duplicate_handler_and_edit_during_dispatch_keep_one_immutable_submission() {
        var gate = new PartyGate { HoldBeforeCreate = true };
        await using var harness = await CreateHarnessAsync(gate);
        var (projectId, first, _) = await CreateNodesAsync(harness);
        var cut = Render(harness, projectId);
        await SelectAsync(cut, first.Id);
        await DraftAsync(cut, "Immutable original input");
        var callback = cut.FindComponents<Button>().Single(button => button.Instance.Text == "Create party").Instance.Click;
        var pending = cut.InvokeAsync(() => callback.InvokeAsync());
        try {
            await gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await DraftAsync(cut, "Changed after admission");
            await cut.InvokeAsync(() => callback.InvokeAsync());
            Assert.Equal(1, gate.CreateCalls);
        } finally {
            gate.ReleaseBefore.TrySetResult();
        }
        await pending;
        var parties = await harness.Context.Services.GetRequiredService<PartyDirectoryService>().ListPartiesAsync();
        Assert.Single(parties, party => party.DisplayName == "Immutable original input");
        Assert.DoesNotContain(parties, party => party.DisplayName == "Changed after admission");
        Assert.Empty(await gate.Inner!.ListAssignmentsDetailedAsync(projectId));
    }

    [Fact]
    public async Task Independent_editors_keep_the_second_submission_busy_until_its_own_completion() {
        var gate = new PartyGate { HoldCreate = true };
        await using var harness = await CreateHarnessAsync(gate);
        var (projectId, first, second) = await CreateNodesAsync(harness);
        var left = Render(harness, projectId);
        var right = Render(harness, projectId);
        await SelectAsync(left, first.Id);
        await SelectAsync(right, second.Id);
        await DraftAsync(left, "Left accepted party");
        await DraftAsync(right, "Right accepted party");
        var firstPending = left.InvokeAsync(() => left.FindComponents<Button>().Single(button => button.Instance.Text == "Create party").Instance.Click.InvokeAsync());
        Task? secondPending = null;
        try {
            await gate.Created.Task.WaitAsync(TimeSpan.FromSeconds(20));
            secondPending = right.InvokeAsync(() => right.FindComponents<Button>().Single(button => button.Instance.Text == "Create party").Instance.Click.InvokeAsync());
            await gate.SecondCreated.Task.WaitAsync(TimeSpan.FromSeconds(20));
            gate.Release.TrySetResult();
            await firstPending;
            Assert.True(right.Find("[data-testid='project-structure-participant-save']").HasAttribute("disabled"));
            Assert.Equal("Right accepted party", right.Find("[data-testid='project-structure-participant-quick-name']").GetAttribute("value"));
        } finally {
            gate.Release.TrySetResult();
            gate.SecondRelease.TrySetResult();
        }
        await firstPending;
        if (secondPending is not null) {
            await secondPending;
        }
        Assert.Equal(2, gate.CreateCalls);
        var parties = await harness.Context.Services.GetRequiredService<PartyDirectoryService>().ListPartiesAsync();
        Assert.Single(parties, party => party.DisplayName == "Left accepted party");
        Assert.Single(parties, party => party.DisplayName == "Right accepted party");
        Assert.Empty(await gate.Inner!.ListAssignmentsDetailedAsync(projectId));
    }

    [Theory]
    [InlineData(ProjectPartyQuickCreateKind.Person)]
    [InlineData(ProjectPartyQuickCreateKind.Organization)]
    [InlineData(ProjectPartyQuickCreateKind.OrganizationUnit)]
    [InlineData(ProjectPartyQuickCreateKind.AiAgent)]
    public async Task Native_commit_identity_survives_search_failure_for_each_supported_kind(ProjectPartyQuickCreateKind kind) {
        SearchFault? fault = null;
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddScoped<ISearchIndexService>(provider => {
            var proxy = DispatchProxy.Create<ISearchIndexService, SearchFault>();
            fault = (SearchFault)(object)proxy;
            fault.Inner = ActivatorUtilities.CreateInstance<SearchIndexService>(provider);
            return proxy;
        }));
        var (projectId, _, _) = await CreateNodesAsync(harness);
        var admission = (await harness.Context.Services.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(projectId)).ExpectedProjectAdmission;
        var owner = harness.Context.Services.GetRequiredService<ProjectPartyIntegrationService>();
        fault!.Armed = true;
        var result = await owner.CreatePartyAsync(new() { ProjectId = projectId, ExpectedProjectAdmission = admission,
            PartyKind = kind, DisplayName = $"Native {kind} receipt", Summary = "Retained native summary" });
        Assert.True(result.IsSuccess);
        var receipt = Assert.IsType<ProjectPartyQuickCreateResult>(result.Value);
        Assert.NotNull(receipt.ObservationWarning);
        Assert.True(fault.Fired);
        var accepted = Assert.Single(await harness.Context.Services.GetRequiredService<PartyDirectoryService>().ListPartiesAsync(), party => party.Id == receipt.PartyId);
        Assert.Equal($"Native {kind} receipt", accepted.DisplayName);
        Assert.Empty(await owner.ListAssignmentsDetailedAsync(projectId));
        output.WriteLine($"Native {kind}: PartyId={receipt.PartyId:D}, known committed before search failure; no assignment.");
    }

    [Theory]
    [InlineData(OriginalChange.Metadata)]
    [InlineData(OriginalChange.Kind)]
    [InlineData(OriginalChange.Parent)]
    [InlineData(OriginalChange.ProjectLifetime)]
    public async Task Changed_original_target_is_refused_before_directory_dispatch(OriginalChange change) {
        var gate = new PartyGate();
        await using var harness = await CreateHarnessAsync(gate);
        var (projectId, first, second) = await CreateNodesAsync(harness);
        var cut = Render(harness, projectId);
        await SelectAsync(cut, first.Id);
        await DraftAsync(cut, "Refused stale create");
        if (change == OriginalChange.ProjectLifetime) {
            var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
            var admission = (await harness.Context.Services.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(projectId)).ExpectedProjectAdmission;
            await projects.DeleteAsync(projectId, expectedProjectAdmission: admission);
            Assert.True((await projects.CreateAsync(projectId, new() { Name = "Replacement project" })).IsSuccess);
        } else {
            await using var db = await harness.Context.Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
            var record = await db.Set<ProjectObjectRecord>().SingleAsync(node => node.ProjectId == projectId && node.NodeKey == first.Id);
            if (change == OriginalChange.Metadata) {
                record.MetadataJson = "{\"preservedConcurrentField\":true}";
            } else if (change == OriginalChange.Kind) {
                record.ObjectType = ProjectObjectType.Note;
            } else {
                record.ParentNodeKey = second.Id;
            }
            await db.SaveChangesAsync();
        }
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-participant-quick-create']").ClickAsync(new MouseEventArgs()));
        Assert.Equal(0, gate.CreateCalls);
        Assert.DoesNotContain(await harness.Context.Services.GetRequiredService<PartyDirectoryService>().ListPartiesAsync(), party => party.DisplayName == "Refused stale create");
        Assert.Contains("no longer available", cut.Find("[data-testid='project-structure-party-editor']").TextContent, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Accepted_work_cannot_publish_after_actor_or_runtime_generation_retirement(bool changeActor) {
        var gate = new PartyGate { HoldCreate = true };
        RuntimeGeneration? runtime = null;
        await using var harness = await CreateHarnessAsync(gate, services => {
            services.AddSingleton<CanonicalRuntimeDatabase>();
            services.AddSingleton<ICanonicalRuntimeDatabase>(provider => runtime = new(provider.GetRequiredService<CanonicalRuntimeDatabase>()));
        });
        var (projectId, first, second) = await CreateNodesAsync(harness);
        static Task<AuthenticationState> Actor(string name) => Task.FromResult(new AuthenticationState(new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.Name, name)], "Owned test actor"))));
        var actor = Actor("Original");
        var host = harness.Context.Render<CascadingValue<Task<AuthenticationState>>>(parameters => parameters.Add(component => component.Value, actor)
            .AddChildContent<ProjectStructurePage>(child => child.Add(component => component.ProjectId, projectId)));
        var cut = host.FindComponent<ProjectStructurePage>();
        cut.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        await SelectAsync(cut, first.Id);
        await DraftAsync(cut, "Original retired owner");
        var pending = cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-participant-quick-create']").ClickAsync(new MouseEventArgs()));
        try {
            await gate.Created.Task.WaitAsync(TimeSpan.FromSeconds(20));
            if (changeActor) {
                await host.InvokeAsync(() => host.Render(parameters => parameters.Add(component => component.Value, Actor("Successor"))
                    .AddChildContent<ProjectStructurePage>(child => child.Add(component => component.ProjectId, projectId))));
            } else {
                runtime!.Generation++;
            }
            await SelectAsync(cut, second.Id);
            await DraftAsync(cut, "New authority draft");
        } finally {
            gate.Release.TrySetResult();
        }
        await pending;
        Assert.Equal("New authority draft", cut.Find("[data-testid='project-structure-participant-quick-name']").GetAttribute("value"));
        Assert.DoesNotContain("Directory party", cut.Find("[data-testid='project-structure-party-editor']").TextContent, StringComparison.Ordinal);
        Assert.Single(await harness.Context.Services.GetRequiredService<PartyDirectoryService>().ListPartiesAsync(), party => party.Id == gate.AcceptedPartyId);
        Assert.Empty(await gate.Inner!.ListAssignmentsDetailedAsync(projectId));
    }

    public enum OriginalChange { Metadata, Kind, Parent, ProjectLifetime }

    private static Task<ComponentTestHarness> CreateHarnessAsync(PartyGate gate, Action<IServiceCollection>? configure = null)
        => ComponentTestHarness.CreateAsync(services => {
            configure?.Invoke(services);
            services.AddScoped<IProjectPartyIntegrationBridge>(provider => {
            gate.Inner = provider.GetRequiredService<ProjectPartyIntegrationService>();
            var bridge = DispatchProxy.Create<IProjectPartyIntegrationBridge, PartyProxy>();
            ((PartyProxy)(object)bridge).Gate = gate;
            return bridge;
            });
        });

    private static async Task<(Guid ProjectId, ProjectStructureNode First, ProjectStructureNode Second)> CreateNodesAsync(ComponentTestHarness harness) {
        var saved = await harness.Context.Services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "WB5 owned participant project" });
        Assert.True(saved.IsSuccess);
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var first = await workbench.CreateObjectAsync(saved.Value, new(ProjectObjectType.Participant, "Participant A", "A", "A notes", $"project:{saved.Value}"));
        var second = await workbench.CreateObjectAsync(saved.Value, new(ProjectObjectType.Participant, "Participant B", "B", "B notes", $"project:{saved.Value}"));
        return (saved.Value, first, second);
    }

    private static IRenderedComponent<ProjectStructurePage> Render(ComponentTestHarness harness, Guid projectId) {
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{projectId:D}/structure");
        var cut = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(page => page.ProjectId, projectId));
        cut.WaitForAssertion(() => Assert.Contains(cut.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == $"project:{projectId}"));
        return cut;
    }

    private static async Task SelectAsync(IRenderedComponent<ProjectStructurePage> cut, string nodeId) {
        await cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnSelectionChanged(nodeId, JsonSerializer.Serialize(new[] { nodeId })));
        cut.WaitForElement("[data-testid='project-structure-participant-local-only']");
    }

    private static Task DraftAsync(IRenderedComponent<ProjectStructurePage> cut, string name)
        => cut.InvokeAsync(async () => {
            await cut.Find("[data-testid='project-structure-participant-local-only']").ChangeAsync(new ChangeEventArgs { Value = false });
            await cut.Find("[data-testid='project-structure-participant-quick-name']").InputAsync(new ChangeEventArgs { Value = name });
        });

    private sealed class PartyGate {
        public IProjectPartyIntegrationBridge? Inner { get; set; }
        public bool HoldCreate { get; set; }
        public bool HoldBeforeCreate { get; set; }
        public bool FailOptionsAfterCreate { get; set; }
        public int CreateCalls { get; set; }
        public Guid? AcceptedPartyId { get; set; }
        public TaskCompletionSource Created { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondCreated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseBefore { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<Result<ProjectPartyQuickCreateResult>> CreateAsync(ProjectPartyQuickCreateRequest request, CancellationToken cancellationToken) {
            var call = ++CreateCalls;
            Started.TrySetResult();
            if (HoldBeforeCreate) {
                await ReleaseBefore.Task;
            }
            var result = await Inner!.CreatePartyAsync(request, cancellationToken);
            AcceptedPartyId = result.Value?.PartyId;
            (call == 1 ? Created : SecondCreated).TrySetResult();
            if (HoldCreate) {
                await (call == 1 ? Release : SecondRelease).Task;
            }
            return result;
        }
    }

    private sealed class RuntimeGeneration(ICanonicalRuntimeDatabase inner) : ICanonicalRuntimeDatabase {
        public ResolvedDatabaseProfile Profile => inner.Profile;
        public long Generation { get; set; } = inner.Generation;
    }

    private class SearchFault : DispatchProxy {
        public ISearchIndexService Inner { get; set; } = null!;
        public bool Armed { get; set; }
        public bool Fired { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? arguments) {
            ArgumentNullException.ThrowIfNull(method);
            if (Armed && method.Name is nameof(ISearchIndexService.UpsertAsync) or nameof(ISearchIndexService.UpsertForMutationAsync)) {
                Armed = false;
                Fired = true;
                return Task.FromException(new IOException("Owned search publication failure after native commit."));
            }
            return method.Invoke(Inner, arguments);
        }
    }

    private class PartyProxy : DispatchProxy {
        public PartyGate Gate { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? arguments) {
            ArgumentNullException.ThrowIfNull(method);
            if (method.Name == nameof(IProjectPartyIntegrationBridge.CreatePartyAsync)) {
                return Gate.CreateAsync((ProjectPartyQuickCreateRequest)arguments![0]!, (CancellationToken)arguments[1]!);
            }
            if (method.Name == nameof(IProjectPartyIntegrationBridge.ListPartyOptionsAsync) && Gate.AcceptedPartyId.HasValue && Gate.FailOptionsAfterCreate) {
                return Task.FromException<IReadOnlyList<ProjectPartyOption>>(new IOException("Owned option read failure after directory commit."));
            }
            return method.Invoke(Gate.Inner, arguments);
        }
    }
}
