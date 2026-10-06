using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using CanDoItAll.Workbench.Operators.UI.Parties;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectStructurePartySaveTests {
    [Theory]
    [InlineData(ProjectObjectType.Participant)]
    [InlineData(ProjectObjectType.Meeting)]
    public async Task No_change_save_preserves_native_assignment_identity_precision_roles_and_unknown_metadata(ProjectObjectType kind) {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var target = await SeedAsync(harness, kind);
        var owner = harness.Context.Services.GetRequiredService<IProjectPartyIntegrationBridge>();
        var role = harness.Context.Services.GetRequiredService<IProjectNodeAssignmentPolicyBridge>().Resolve(kind, target.Node.ObjectSubtype).PreferredRole!.Value;
        var organization = await harness.Context.Services.GetRequiredService<PartyDirectoryService>().SavePartyAsync(new() {
            PartyType = PartyType.Organization, DisplayName = "Original affiliation organization" });
        Assert.True(organization.IsSuccess);
        var affiliation = await harness.Context.Services.GetRequiredService<IPartyOrganizationAffiliationService>().UpsertAsync(new() {
            PersonPartyId = target.FirstParty, OrganizationPartyId = organization.Value,
            AffiliationKind = PartyOrganizationAffiliationKind.Contractor, IsPrimary = true, JobTitle = "Delivery lead",
            ValidFrom = new DateOnly(2025, 1, 1) }, "owned-wb5-test");
        Assert.True(affiliation.IsSuccess);
        var projectDefault = await owner.SaveAssignmentAsync(new() { ProjectId = target.ProjectId, ExpectedProjectAdmission = target.Admission,
            PartyId = target.SecondParty, Role = ProjectPartyAssignmentRole.Manager, IsPrimary = true, Source = "Original project default" });
        Assert.True(projectDefault.IsSuccess);
        var defaultBefore = await ReadAssignmentAsync(harness, projectDefault.Value);
        var id = (await owner.SaveAssignmentAsync(new() { ProjectId = target.ProjectId, ExpectedProjectAdmission = target.Admission,
            PartyId = target.FirstParty, PartyAffiliationId = affiliation.Value!.Id, NodeKey = target.Node.Id, Role = role, IsPrimary = true, AllocationPercent = 37.125m,
            Source = "owned-source", Notes = "Preserve assignment note" })).Value;
        await using (var context = await harness.Context.Services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync()) {
            var row = await context.Set<ProjectPartyAssignment>().SingleAsync(item => item.Id == id);
            row.StartsAtUtc = new DateTimeOffset(2026, 9, 1, 10, 11, 12, TimeSpan.Zero).AddTicks(1234560);
            row.EndsAtUtc = row.StartsAtUtc.Value.AddDays(12).AddTicks(120);
            row.PhaseName = "Native phase context";
            await context.SaveChangesAsync();
        }
        var beforeAssignment = await ReadAssignmentAsync(harness, id);
        var before = await ReadNodeAsync(harness, target);
        var cut = Render(harness, target.ProjectId);
        await SelectAsync(cut, target.Node.Id);
        await SaveAsync(cut, kind);
        Assert.Equal(PartySavePhase.Observed, cut.FindComponent<PartyEditor>().Instance.State.Receipt!.Phase);
        await SaveAsync(cut, kind);
        Assert.Equal(beforeAssignment, await ReadAssignmentAsync(harness, id));
        Assert.Equal(defaultBefore, await ReadAssignmentAsync(harness, projectDefault.Value));
        Assert.Single(await owner.ListAssignmentsDetailedAsync(target.ProjectId), row => row.Id == id);
        var after = await ReadNodeAsync(harness, target);
        Assert.Equal((before.StartUtc, before.EndUtc, before.DurationSeconds, before.Notes, before.Subtitle, before.ParentId,
            before.StorageObjectReferenceJson, JsonSerializer.Serialize(before.NodeReferences)),
            (after.StartUtc, after.EndUtc, after.DurationSeconds, after.Notes, after.Subtitle, after.ParentId,
            after.StorageObjectReferenceJson, JsonSerializer.Serialize(after.NodeReferences)));
        var beforeMetadata = JsonNode.Parse(before.MetadataJson)!;
        var afterMetadata = JsonNode.Parse(after.MetadataJson)!;
        Assert.True(JsonNode.DeepEquals(beforeMetadata["unknownRoot"], afterMetadata["unknownRoot"]));
        var section = kind == ProjectObjectType.Participant ? "participant" : "meeting";
        Assert.True(JsonNode.DeepEquals(beforeMetadata[section]!["unknownChild"], afterMetadata[section]!["unknownChild"]));
        Assert.Equal(target.Neighbor, JsonSerializer.Serialize((await Workbench(harness).GetStructureAsync(target.ProjectId)).Nodes.Single(node => node.Title == "Untouched neighbor")));
    }

    [Fact]
    public async Task Same_selection_refresh_keeps_raw_fields_and_edit_context() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var target = await SeedAsync(harness);
        var cut = Render(harness, target.ProjectId);
        await SelectAsync(cut, target.Node.Id);
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-participant-local-only']").ChangeAsync(new ChangeEventArgs { Value = false }));
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-participant-quick-name']").InputAsync(new ChangeEventArgs { Value = "Unblurred survives native refresh" }));
        var edit = cut.FindComponent<PartyEditor>().Instance.State.EditContext;
        await SelectAsync(cut, target.Node.Id);
        Assert.Same(edit, cut.FindComponent<PartyEditor>().Instance.State.EditContext);
        Assert.Equal("Unblurred survives native refresh", cut.Find("[data-testid='project-structure-participant-quick-name']").GetAttribute("value"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delayed_A_B_A_option_success_or_error_cannot_replace_a_new_opening(bool failOldRead) {
        var gate = new Gate { HoldFirstOptions = true, FailHeldOptions = failOldRead };
        await using var harness = await HarnessAsync(gate);
        var target = await SeedAsync(harness);
        var other = await Workbench(harness).CreateObjectAsync(target.ProjectId, new(ProjectObjectType.Participant, "Participant B", "", "", $"project:{target.ProjectId:D}"));
        var cut = Render(harness, target.ProjectId);
        var pending = cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnSelectionChanged(target.Node.Id, JsonSerializer.Serialize(new[] { target.Node.Id })));
        PartyEditorState successor;
        try {
            await gate.OptionsEntered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await SelectAsync(cut, other.Id);
            await SelectAsync(cut, target.Node.Id);
            successor = cut.FindComponent<PartyEditor>().Instance.State;
            await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-participant-local-only']").ChangeAsync(new ChangeEventArgs { Value = false }));
            await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-participant-quick-name']").InputAsync(new ChangeEventArgs { Value = "Newest opening's raw draft" }));
        } finally {
            gate.OptionsRelease.TrySetResult();
        }
        await pending;
        Assert.Same(successor, cut.FindComponent<PartyEditor>().Instance.State);
        Assert.False(successor.IsLoading);
        Assert.False(successor.IsUnavailable);
        Assert.Equal("Newest opening's raw draft", successor.Draft.Name);
        Assert.Equal(string.Empty, successor.Message);
        Assert.Equal(0, gate.Replacements);
    }

    [Fact]
    public async Task Actor_retirement_after_assignment_commit_refuses_the_later_metadata_phase() {
        var gate = new Gate();
        await using var harness = await HarnessAsync(gate);
        var target = await SeedAsync(harness);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        gate.AfterAssignment = async () => {
            entered.TrySetResult();
            await release.Task;
        };
        static Task<AuthenticationState> Actor(string name) => Task.FromResult(new AuthenticationState(new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.Name, name)], "Owned test actor"))));
        var host = harness.Context.Render<CascadingValue<Task<AuthenticationState>>>(parameters => parameters.Add(component => component.Value, Actor("Original"))
            .AddChildContent<ProjectStructurePage>(child => child.Add(component => component.ProjectId, target.ProjectId)));
        var cut = host.FindComponent<ProjectStructurePage>();
        cut.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        await SelectAsync(cut, target.Node.Id);
        await PickAsync(cut, target.FirstParty);
        var original = cut.FindComponent<PartyEditor>().Instance.State;
        var pending = SaveAsync(cut);
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await host.InvokeAsync(() => host.Render(parameters => parameters.Add(component => component.Value, Actor("Successor"))
                .AddChildContent<ProjectStructurePage>(child => child.Add(component => component.ProjectId, target.ProjectId))));
        } finally {
            release.TrySetResult();
        }
        await pending;
        Assert.True(original.IsRetired);
        Assert.Equal(PartySavePhase.AssignmentsCommitted, original.Receipt!.Phase);
        Assert.Equal(target.Node.MetadataJson, (await ReadNodeAsync(harness, target)).MetadataJson);
        Assert.Equal(target.FirstParty, Assert.Single(await gate.Inner!.ListAssignmentsDetailedAsync(target.ProjectId)).PartyId);
    }

    [Fact]
    public async Task Two_editors_cannot_overwrite_the_first_native_commit() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var target = await SeedAsync(harness);
        var first = Render(harness, target.ProjectId);
        var second = Render(harness, target.ProjectId);
        await SelectAsync(first, target.Node.Id);
        await SelectAsync(second, target.Node.Id);
        await PickAsync(first, target.FirstParty);
        await PickAsync(second, target.SecondParty);
        await SaveAsync(first);
        var accepted = Assert.Single(await harness.Context.Services.GetRequiredService<IProjectPartyIntegrationBridge>().ListAssignmentsDetailedAsync(target.ProjectId));
        var committed = await ReadNodeAsync(harness, target);
        await SaveAsync(second);
        Assert.Equal(PartySavePhase.Rejected, second.FindComponent<PartyEditor>().Instance.State.Receipt!.Phase);
        Assert.Equal(accepted, Assert.Single(await harness.Context.Services.GetRequiredService<IProjectPartyIntegrationBridge>().ListAssignmentsDetailedAsync(target.ProjectId)));
        Assert.Equal(committed.MetadataJson, (await ReadNodeAsync(harness, target)).MetadataJson);
    }

    [Fact]
    public async Task Assignment_commit_survives_metadata_conflict_and_retry_does_not_replace_again() {
        var gate = new Gate();
        await using var harness = await HarnessAsync(gate);
        var target = await SeedAsync(harness);
        gate.AfterAssignment = async () => {
            var metadata = JsonNode.Parse(target.Node.MetadataJson)!.AsObject();
            metadata["concurrentNativeEdit"] = "Keep exact competitor";
            await Workbench(harness).UpdateContentMetadataAsync(target.Admission, target.Node, metadata.ToJsonString());
        };
        var cut = Render(harness, target.ProjectId);
        await SelectAsync(cut, target.Node.Id);
        await PickAsync(cut, target.FirstParty);
        await SaveAsync(cut);
        var state = cut.FindComponent<PartyEditor>().Instance.State;
        var accepted = Assert.Single(await gate.Inner!.ListAssignmentsDetailedAsync(target.ProjectId));
        Assert.Equal(PartySavePhase.AssignmentsCommitted, state.Receipt!.Phase);
        Assert.Contains(accepted.Id, state.Receipt.AssignmentIds);
        Assert.True(state.RequiresObservation);
        Assert.Equal("Keep exact competitor", JsonNode.Parse((await ReadNodeAsync(harness, target)).MetadataJson)!["concurrentNativeEdit"]!.GetValue<string>());
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-party-retry']").ClickAsync(new MouseEventArgs()));
        Assert.Equal(1, gate.Replacements);
        Assert.Equal(accepted.Id, Assert.Single(await gate.Inner.ListAssignmentsDetailedAsync(target.ProjectId)).Id);
    }

    [Fact]
    public async Task Node_commit_is_known_before_failed_readback_and_read_only_retry_recovers() {
        var gate = new Gate { FailReadAfterCommit = true };
        await using var harness = await HarnessAsync(gate);
        var target = await SeedAsync(harness);
        var cut = Render(harness, target.ProjectId);
        await SelectAsync(cut, target.Node.Id);
        await PickAsync(cut, target.FirstParty);
        await SaveAsync(cut);
        var state = cut.FindComponent<PartyEditor>().Instance.State;
        Assert.Equal(PartySavePhase.NodeCommitted, state.Receipt!.Phase);
        Assert.Equal(target.Node.Id, state.Receipt.NodeId);
        Assert.Equal("First directory person", (await ReadNodeAsync(harness, target)).Title);
        gate.FailReadAfterCommit = false;
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-party-retry']").ClickAsync(new MouseEventArgs()));
        Assert.Equal(PartySavePhase.Observed, state.Receipt.Phase);
        Assert.False(state.RequiresObservation);
        Assert.Equal(1, gate.Replacements);
    }

    [Theory]
    [InlineData(ProjectObjectType.Participant)]
    [InlineData(ProjectObjectType.Meeting)]
    public async Task Missing_picker_option_does_not_silently_remove_a_saved_assignment(ProjectObjectType kind) {
        var gate = new Gate();
        await using var harness = await HarnessAsync(gate);
        var target = await SeedAsync(harness, kind);
        var role = harness.Context.Services.GetRequiredService<IProjectNodeAssignmentPolicyBridge>().Resolve(kind, target.Node.ObjectSubtype).PreferredRole!.Value;
        var saved = await gate.Inner!.SaveAssignmentAsync(new() { ProjectId = target.ProjectId, ExpectedProjectAdmission = target.Admission,
            PartyId = target.FirstParty, NodeKey = target.Node.Id, Role = role, IsPrimary = true });
        Assert.True(saved.IsSuccess);
        gate.HiddenParty = target.FirstParty;
        var cut = Render(harness, target.ProjectId);
        await SelectAsync(cut, target.Node.Id);
        Assert.Single(cut.FindComponent<PartyEditor>().Instance.State.Choices, choice => choice.Id == target.FirstParty && choice.IsMissing);
        await SaveAsync(cut, kind);
        Assert.Equal(saved.Value, Assert.Single(await gate.Inner.ListAssignmentsDetailedAsync(target.ProjectId)).Id);
        Assert.Equal(PartySavePhase.Observed, cut.FindComponent<PartyEditor>().Instance.State.Receipt!.Phase);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Failed_option_read_is_distinct_from_empty_success_and_can_retry_without_mutation(bool fail) {
        var gate = new Gate { FailOptions = fail, EmptyOptions = !fail };
        await using var harness = await HarnessAsync(gate);
        var target = await SeedAsync(harness);
        var cut = Render(harness, target.ProjectId);
        await SelectAsync(cut, target.Node.Id);
        var state = cut.FindComponent<PartyEditor>().Instance.State;
        Assert.Equal(fail, state.IsUnavailable);
        Assert.Empty(state.Choices);
        gate.FailOptions = false;
        if (fail) {
            await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-party-retry']").ClickAsync(new MouseEventArgs()));
            Assert.False(state.IsUnavailable);
            Assert.NotEmpty(state.Choices);
        }
        Assert.Equal(0, gate.Replacements);
    }

    [Fact]
    public async Task Native_sensitive_directory_contacts_are_absent_from_the_entire_projected_state() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var target = await SeedAsync(harness);
        var saved = await harness.Context.Services.GetRequiredService<PartyDirectoryService>().SavePartyAsync(new() {
            PartyType = PartyType.Person, DisplayName = "Sensitive native person", IsSensitive = true,
            ContactPoints = [new() { ContactType = PartyContactType.Email, Value = "native-private@example.invalid", IsPublic = true, IsPrimary = true },
                new() { ContactType = PartyContactType.Phone, Value = "native-private-phone", IsPublic = true }] });
        Assert.True(saved.IsSuccess);
        var cut = Render(harness, target.ProjectId);
        await SelectAsync(cut, target.Node.Id);
        var state = cut.FindComponent<PartyEditor>().Instance.State;
        var choice = Assert.Single(state.Choices, item => item.Id == saved.Value);
        Assert.True(choice.IsSensitive);
        Assert.Equal(string.Empty, choice.PublicContact);
        Assert.Equal(string.Empty, choice.PublicPhone);
        Assert.DoesNotContain("native-private", JsonSerializer.Serialize(state.Choices), StringComparison.Ordinal);
        Assert.DoesNotContain("native-private", cut.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Native_assignment_owner_rechecks_node_occurrence_after_the_form_lookup(bool changeKind) {
        var gate = new Gate { HoldLookup = true };
        await using var harness = await HarnessAsync(gate);
        var target = await SeedAsync(harness);
        var cut = Render(harness, target.ProjectId);
        await SelectAsync(cut, target.Node.Id);
        await PickAsync(cut, target.FirstParty);
        var pending = SaveAsync(cut);
        try {
            await gate.LookupEntered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await using var db = await harness.Context.Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
            var row = await db.Set<ProjectObjectRecord>().SingleAsync(item => item.ProjectId == target.ProjectId && item.NodeKey == target.Node.Id);
            if (changeKind) {
                row.ObjectType = ProjectObjectType.Note;
            } else {
                row.ParentNodeKey = target.NeighborId;
            }
            await db.SaveChangesAsync();
        } finally {
            gate.LookupRelease.TrySetResult();
        }
        await pending;
        Assert.Equal(1, gate.Replacements);
        Assert.Empty(await gate.Inner!.ListAssignmentsDetailedAsync(target.ProjectId));
        Assert.Equal(PartySavePhase.Rejected, cut.FindComponent<PartyEditor>().Instance.State.Receipt!.Phase);
    }

    [Fact]
    public async Task Unavailable_conditional_owner_never_falls_back_to_unconditional_replacement() {
        var gate = new Gate { Unsupported = true };
        await using var harness = await HarnessAsync(gate);
        var target = await SeedAsync(harness);
        var cut = Render(harness, target.ProjectId);
        await SelectAsync(cut, target.Node.Id);
        await PickAsync(cut, target.FirstParty);
        await SaveAsync(cut);
        Assert.Equal(1, gate.Replacements);
        Assert.Equal(0, gate.UnconditionalCalls);
        Assert.Empty(await gate.Inner!.ListAssignmentsDetailedAsync(target.ProjectId));
        Assert.Equal(PartySavePhase.Rejected, cut.FindComponent<PartyEditor>().Instance.State.Receipt!.Phase);
    }

    private sealed record Target(Guid ProjectId, ProjectWriteAdmission Admission, ProjectStructureNode Node, Guid FirstParty,
        Guid SecondParty, string NeighborId, string Neighbor);

    private static async Task<Target> SeedAsync(ComponentTestHarness harness, ProjectObjectType kind = ProjectObjectType.Participant) {
        var project = (await harness.Context.Services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Owned WB5 relationships" })).Value;
        var workbench = Workbench(harness);
        var node = await workbench.CreateObjectAsync(project, new(kind, "Native party node", "Exact subtitle", "Exact notes", $"project:{project:D}",
            StartUtc: new DateTimeOffset(2026, 9, 3, 9, 10, 11, TimeSpan.Zero).AddTicks(6543210), DurationSeconds: 1357,
            MetadataJson: "{\"unknownRoot\":{\"keep\":null,\"precise\":123.456789},\"participant\":{\"unknownChild\":[1,null,\"keep\"]},\"meeting\":{\"unknownChild\":{\"nested\":true}}}"));
        var neighbor = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Untouched neighbor", "neighbor", "Exact neighbor notes", $"project:{project:D}"));
        var directory = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var first = await directory.SavePartyAsync(new() { PartyType = PartyType.Person, DisplayName = "First directory person" });
        var second = await directory.SavePartyAsync(new() { PartyType = PartyType.Person, DisplayName = "Second directory person" });
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        var structure = await workbench.GetStructureAsync(project);
        return new(project, structure.ExpectedProjectAdmission!, structure.Nodes.Single(item => item.Id == node.Id), first.Value, second.Value, neighbor.Id,
            JsonSerializer.Serialize(structure.Nodes.Single(item => item.Id == neighbor.Id)));
    }

    private static ProjectWorkbenchService Workbench(ComponentTestHarness harness) => harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
    private static async Task<ProjectStructureNode> ReadNodeAsync(ComponentTestHarness harness, Target target)
        => (await Workbench(harness).GetStructureAsync(target.ProjectId)).Nodes.Single(node => node.Id == target.Node.Id);
    private static async Task<string> ReadAssignmentAsync(ComponentTestHarness harness, Guid id) {
        await using var context = await harness.Context.Services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        return JsonSerializer.Serialize(await context.Set<ProjectPartyAssignment>().AsNoTracking().SingleAsync(row => row.Id == id));
    }
    private static IRenderedComponent<ProjectStructurePage> Render(ComponentTestHarness harness, Guid id) {
        var cut = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(page => page.ProjectId, id));
        cut.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        return cut;
    }
    private static async Task SelectAsync(IRenderedComponent<ProjectStructurePage> cut, string node) {
        await cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnSelectionChanged(node, JsonSerializer.Serialize(new[] { node })));
        cut.WaitForAssertion(() => Assert.False(cut.FindComponent<PartyEditor>().Instance.State.IsLoading));
    }
    private static async Task PickAsync(IRenderedComponent<ProjectStructurePage> cut, Guid id) {
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-participant-local-only']").ChangeAsync(new ChangeEventArgs { Value = false }));
        await cut.InvokeAsync(() => cut.Find($"[data-testid='project-structure-participant-party-option-{id:N}']").ClickAsync(new MouseEventArgs()));
    }
    private static Task SaveAsync(IRenderedComponent<ProjectStructurePage> cut, ProjectObjectType kind = ProjectObjectType.Participant)
        => cut.InvokeAsync(() => cut.Find(kind == ProjectObjectType.Participant ? "[data-testid='project-structure-participant-save']" : "[data-testid='project-structure-meeting-save']").ClickAsync(new MouseEventArgs()));
    private static Task<ComponentTestHarness> HarnessAsync(Gate gate)
        => ComponentTestHarness.CreateAsync(services => services.AddScoped<IProjectPartyIntegrationBridge>(provider => {
            gate.Inner = provider.GetRequiredService<ProjectPartyIntegrationService>();
            var proxy = DispatchProxy.Create<IProjectPartyIntegrationBridge, BridgeProxy>();
            ((BridgeProxy)(object)proxy).Gate = gate;
            return proxy;
        }));

    private sealed class Gate {
        public IProjectPartyIntegrationBridge? Inner { get; set; }
        public Func<Task>? AfterAssignment { get; set; }
        public int Replacements { get; set; }
        public int UnconditionalCalls { get; set; }
        public bool Committed { get; set; }
        public bool FailReadAfterCommit { get; set; }
        public bool FailOptions { get; set; }
        public bool EmptyOptions { get; set; }
        public bool HoldLookup { get; set; }
        public bool Unsupported { get; set; }
        public Guid? HiddenParty { get; set; }
        public bool HoldFirstOptions { get; set; }
        public bool FailHeldOptions { get; set; }
        public TaskCompletionSource OptionsEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource OptionsRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource LookupEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource LookupRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private class BridgeProxy : DispatchProxy {
        public Gate Gate { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? arguments) {
            ArgumentNullException.ThrowIfNull(method);
            if (method.Name == nameof(IProjectPartyIntegrationBridge.ReplaceNodeAssignmentsIfCurrentAsync) && arguments![5] is ProjectPartyNodeOccurrence) {
                return ReplaceAsync(method, arguments);
            }
            if (method.Name == nameof(IProjectPartyIntegrationBridge.ReplaceNodeAssignmentsAsync)) {
                Gate.UnconditionalCalls++;
            }
            if (method.Name == nameof(IProjectPartyIntegrationBridge.ListAssignmentsDetailedAsync) && Gate.Committed && Gate.FailReadAfterCommit) {
                return Task.FromException<IReadOnlyList<ProjectPartyAssignmentDetail>>(new IOException("Owned assignment read failure."));
            }
            if (method.Name == nameof(IProjectPartyIntegrationBridge.ListPartyOptionsAsync)) {
                return OptionsAsync(method, arguments);
            }
            if (method.Name == nameof(IProjectPartyIntegrationBridge.GetPartyOptionAsync)) {
                return OptionAsync(method, arguments!);
            }
            return method.Invoke(Gate.Inner, arguments);
        }
        private async Task<Result<ProjectNodeAssignmentCommit>> ReplaceAsync(MethodInfo method, object?[] arguments) {
            Gate.Replacements++;
            if (Gate.Unsupported) {
                return Result<ProjectNodeAssignmentCommit>.Failure(Error.Failure("Conditional owner unavailable.", ProjectPartyIntegrationErrorCodes.ConditionalReplacementUnavailable));
            }
            var result = await (Task<Result<ProjectNodeAssignmentCommit>>)method.Invoke(Gate.Inner, arguments)!;
            Gate.Committed = result.IsSuccess;
            if (Gate.Committed && Gate.AfterAssignment is { } after) {
                Gate.AfterAssignment = null;
                await after();
            }
            return result;
        }
        private async Task<IReadOnlyList<ProjectPartyOption>> OptionsAsync(MethodInfo method, object?[]? arguments) {
            if (Gate.FailOptions) {
                throw new IOException("Owned directory read failure.");
            }
            var result = await (Task<IReadOnlyList<ProjectPartyOption>>)method.Invoke(Gate.Inner, arguments)!;
            if (Gate.HoldFirstOptions) {
                Gate.HoldFirstOptions = false;
                Gate.OptionsEntered.TrySetResult();
                await Gate.OptionsRelease.Task;
                if (Gate.FailHeldOptions) {
                    throw new IOException("Owned retired option read failure.");
                }
            }
            return Gate.EmptyOptions ? [] : result.Where(option => option.PartyId != Gate.HiddenParty).ToArray();
        }
        private async Task<ProjectPartyOption?> OptionAsync(MethodInfo method, object?[] arguments) {
            if (Gate.HoldLookup) {
                Gate.LookupEntered.TrySetResult();
                await Gate.LookupRelease.Task;
            }
            return arguments[0] is Guid id && id == Gate.HiddenParty ? null : await (Task<ProjectPartyOption?>)method.Invoke(Gate.Inner, arguments)!;
        }
    }
}
