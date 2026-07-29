using Bunit;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Voice;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Processes.AgentChat;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel.Streaming;
using CanDoItAll.Web.Composition;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProcessWorkspaceShellTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 30, 0, TimeSpan.Zero);
    private static readonly Guid ProjectSubprocessRunId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid ProjectSubprocessProjectId = Guid.Parse("12121212-3434-5656-7878-909090909090");

    [Fact]
    public void Global_shell_renders_projection_tabs_and_command_strip()
    {
        using var context = CreateContext(out var client);

        var cut = context.Render<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-shell']")));
        Assert.Equal(ProcessWorkspaceScopeKind.Global, client.LastRequest?.Scope.Kind);
        Assert.Contains("Definition", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Roles", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Steps", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Runs", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Graphs", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Analytics", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Exchange", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Manager chat", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Blazor app delivery", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Store pending", cut.Markup, StringComparison.Ordinal);
        Assert.NotNull(cut.Find("[data-testid='processes-command-strip']"));
        Assert.NotNull(cut.Find("[data-testid='processes-detail-tabs']"));
        Assert.NotNull(cut.Find("[data-testid='processes-definition-tree']"));
    }

    [Fact]
    public void Process_workspace_publishes_selected_definition_and_updates_semantic_view()
    {
        using var context = CreateContext(out _);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var registry = context.Services.GetRequiredService<IAgentChatContextRegistry>();
        navigation.NavigateTo("/processes");

        var cut = context.Render<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() =>
        {
            var position = Assert.IsType<AgentChatSurfacePosition>(registry.Capture()?.Scope.SurfacePosition);
            Assert.Equal("processes", position.Module);
            Assert.Equal("workspace", position.Surface);
            Assert.Equal("definition", position.View);
            Assert.Equal("/processes", position.Route);
            Assert.Equal("process-definition", position.PrimarySelection?.Kind);
            Assert.Equal("blazor-app-delivery", position.PrimarySelection?.Id);
            Assert.Equal("Blazor app delivery", position.PrimarySelection?.DisplayName);
        });
        var initialScopeId = Assert.IsType<AgentChatContextSnapshot>(registry.Capture()).Scope.Id;

        ActivateProcessDetailTab(cut, "processes-detail-tab-runs", "processes-detail-panel-runs");

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            var position = Assert.IsType<AgentChatSurfacePosition>(snapshot.Scope.SurfacePosition);
            Assert.Equal(initialScopeId, snapshot.Scope.Id);
            Assert.Equal("runs.activity", position.View);
            Assert.Contains(position.Facts, fact => fact.Name == "runtime-history");
            Assert.DoesNotContain(position.Facts, fact => fact.Name.Contains("event-ledger", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void Process_workspace_publishes_scope_fragment_and_attachment_atomically_without_extra_query()
    {
        using var context = CreateContext(
            out var client,
            timeProvider: new ManualTimeProvider(Now));
        ConfigureReadyRefresh(client, () => Now);
        var registry = context.Services.GetRequiredService<IAgentChatContextRegistry>();

        var cut = context.Render<ProcessWorkspaceShell>();

        AgentChatContextSnapshot initial = null!;
        cut.WaitForAssertion(() =>
        {
            initial = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(AgentChatContextAccessState.Ready, initial.Scope.AccessState);
            var fragment = Assert.Single(initial.Fragments);
            var attachment = Assert.Single(initial.Attachments);
            Assert.Equal(fragment.ContributorId, attachment.ContributorId);
            Assert.Equal(
                ProcessInvocationSnapshotMapper.AttachmentKindValue,
                attachment.Kind.Value);
            Assert.True(attachment.TryGetAttachment<ProcessInvocationSnapshot>(out var snapshot));
            Assert.NotNull(snapshot);
            Assert.Equal(ProcessInvocationSnapshotSurface.Workspace, snapshot.Surface);
        });
        var requestCount = client.Requests.Count;
        Assert.Equal(requestCount, client.Requests.Count);
    }

    [Fact]
    public void Process_workspace_expires_runtime_context_without_an_implicit_projection_read()
    {
        var timeProvider = new ManualTimeProvider(Now);
        using var context = CreateContext(out var client, timeProvider: timeProvider);
        ConfigureReadyRefresh(client, timeProvider.GetUtcNow);
        var registry = context.Services.GetRequiredService<IAgentChatContextRegistry>();
        var cut = context.Render<ProcessWorkspaceShell>();

        AgentChatContextSnapshot initial = null!;
        AgentChatContextAttachmentEnvelope initialAttachment = null!;
        cut.WaitForAssertion(() =>
        {
            initial = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            initialAttachment = Assert.Single(initial.Attachments);
            Assert.Equal(Now, initialAttachment.CapturedAtUtc);
            Assert.Equal(
                Now.Add(ProcessInvocationSnapshotMapper.FreshnessLifetime),
                initialAttachment.FreshUntilUtc);
        });
        var requestCount = client.Requests.Count;

        timeProvider.Advance(ProcessInvocationSnapshotMapper.FreshnessLifetime);

        cut.WaitForAssertion(() =>
        {
            var expired = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.True(expired.Version > initial.Version);
            Assert.Empty(expired.Attachments);
            Assert.Equal(requestCount, client.Requests.Count);
        });
    }

    [Fact]
    public void Live_processes_expires_runtime_context_without_an_implicit_projection_read()
    {
        var timeProvider = new ManualTimeProvider(Now);
        using var context = CreateContext(out var client, timeProvider: timeProvider);
        ConfigureReadyRefresh(client, timeProvider.GetUtcNow);
        var registry = context.Services.GetRequiredService<IAgentChatContextRegistry>();
        var cut = context.Render<LiveProcessesDashboard>();

        AgentChatContextSnapshot initial = null!;
        AgentChatContextAttachmentEnvelope initialAttachment = null!;
        cut.WaitForAssertion(() =>
        {
            initial = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            initialAttachment = Assert.Single(initial.Attachments);
            Assert.Equal(Now, initialAttachment.CapturedAtUtc);
            Assert.Equal(
                Now.Add(ProcessInvocationSnapshotMapper.FreshnessLifetime),
                initialAttachment.FreshUntilUtc);
        });
        var requestCount = client.Requests.Count;

        timeProvider.Advance(ProcessInvocationSnapshotMapper.FreshnessLifetime);

        cut.WaitForAssertion(() =>
        {
            var expired = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.True(expired.Version > initial.Version);
            Assert.Empty(expired.Attachments);
            Assert.Equal(requestCount, client.Requests.Count);
        });
    }

    [Fact]
    public void Live_processes_publishes_selected_run_and_focused_dialog_without_runtime_ledgers()
    {
        using var context = CreateContext(out _);
        var selectedRunId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var registry = context.Services.GetRequiredService<IAgentChatContextRegistry>();
        navigation.NavigateTo($"/processes/live?runId={selectedRunId:D}");

        var cut = context.Render<LiveProcessesDashboard>(parameters => parameters
            .Add(component => component.RunIdQuery, selectedRunId));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            var position = Assert.IsType<AgentChatSurfacePosition>(snapshot.Scope.SurfacePosition);
            Assert.Equal("live", position.Surface);
            Assert.Equal("activity", position.View);
            Assert.Equal("/processes/live", position.Route);
            Assert.Equal("process-run", position.PrimarySelection?.Kind);
            Assert.Equal(selectedRunId.ToString("D"), position.PrimarySelection?.Id);
            Assert.Equal(WorkspaceScopeKind.Process, snapshot.Scope.WorkspaceScope?.Kind);
            Assert.Contains(position.Facts, fact => fact.Name == "run-process-name" && fact.Value.Contains("customer onboarding", StringComparison.Ordinal));
            Assert.DoesNotContain(position.Facts, fact => fact.Name is "events" or "tools" or "diagnostics");
        });
        var initialScopeId = Assert.IsType<AgentChatContextSnapshot>(registry.Capture()).Scope.Id;

        cut.Find("[data-testid='live-processes-run-open-details']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='live-processes-run-detail-dialog']"));
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            var position = Assert.IsType<AgentChatSurfacePosition>(snapshot.Scope.SurfacePosition);
            Assert.Equal(initialScopeId, snapshot.Scope.Id);
            Assert.Contains(position.Facts, fact => fact.Name == "focused-dialog" && fact.Value == "run-detail");
        });
    }

    [Fact]
    public void Live_processes_fails_closed_when_an_explicit_run_does_not_exist()
    {
        using var context = CreateContext(out _);
        var missingRunId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var registry = context.Services.GetRequiredService<IAgentChatContextRegistry>();

        var cut = context.Render<LiveProcessesDashboard>(parameters => parameters
            .Add(component => component.RunIdQuery, missingRunId));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            var position = Assert.IsType<AgentChatSurfacePosition>(snapshot.Scope.SurfacePosition);
            Assert.Equal(AgentChatContextAccessState.Failed, snapshot.Scope.AccessState);
            Assert.Equal("process-run", position.PrimarySelection?.Kind);
            Assert.Equal(missingRunId.ToString("D"), position.PrimarySelection?.Id);
            Assert.DoesNotContain(
                position.SelectedEntities,
                entity => entity.Kind == "process-run" && entity.Id != missingRunId.ToString("D"));
            Assert.DoesNotContain(position.Facts, fact => fact.Name == "run-process-name");
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Live_processes_fails_closed_for_an_unresolved_non_run_route(bool useProcessId)
    {
        using var context = CreateContext(out _);
        var unresolvedId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var registry = context.Services.GetRequiredService<IAgentChatContextRegistry>();

        var cut = useProcessId
            ? context.Render<LiveProcessesDashboard>(parameters => parameters
                .Add(component => component.ProcessIdQuery, unresolvedId))
            : context.Render<LiveProcessesDashboard>(parameters => parameters
                .Add(component => component.LaunchPlanIdQuery, unresolvedId));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            var position = Assert.IsType<AgentChatSurfacePosition>(snapshot.Scope.SurfacePosition);
            Assert.Equal(AgentChatContextAccessState.Failed, snapshot.Scope.AccessState);
            Assert.Null(position.PrimarySelection);
            Assert.Empty(position.SelectedEntities);
            Assert.DoesNotContain(position.Facts, fact => fact.Name == "run-process-name");
        });
    }

    [Fact]
    public void Process_workspace_fails_closed_when_an_explicit_run_does_not_exist()
    {
        using var context = CreateContext(out _);
        var missingRunId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var registry = context.Services.GetRequiredService<IAgentChatContextRegistry>();

        var cut = context.Render<ProcessWorkspaceShell>(parameters => parameters
            .Add(component => component.RunIdQuery, missingRunId));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            var position = Assert.IsType<AgentChatSurfacePosition>(snapshot.Scope.SurfacePosition);
            Assert.Equal(AgentChatContextAccessState.Failed, snapshot.Scope.AccessState);
            Assert.Equal("process-run", position.PrimarySelection?.Kind);
            Assert.Equal(missingRunId.ToString("D"), position.PrimarySelection?.Id);
            Assert.DoesNotContain(
                position.SelectedEntities,
                entity => entity.Kind == "process-run" && entity.Id != missingRunId.ToString("D"));
            Assert.DoesNotContain(position.Facts, fact => fact.Name == "run-process-name");
        });
    }

    [Fact]
    public void Process_workspace_fails_closed_when_an_explicit_definition_does_not_exist()
    {
        using var context = CreateContext(out var client);
        const string missingDefinitionKey = "missing-route-definition";
        const string fallbackDefinitionKey = "blazor-app-delivery";
        var registry = context.Services.GetRequiredService<IAgentChatContextRegistry>();
        client.ShellResultTransform = (_, projection) =>
        {
            var fallback = Assert.Single(
                projection.DefinitionCatalog.Items,
                item => item.Key.Value == fallbackDefinitionKey);
            return projection with
            {
                DefinitionCatalog = projection.DefinitionCatalog with
                {
                    SelectedDefinitionKey = fallback.Key,
                    SelectedItem = fallback
                }
            };
        };

        var cut = context.Render<ProcessWorkspaceShell>(parameters => parameters
            .Add(component => component.SelectedDefinitionKey, missingDefinitionKey));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            var position = Assert.IsType<AgentChatSurfacePosition>(snapshot.Scope.SurfacePosition);
            Assert.Equal(AgentChatContextAccessState.Failed, snapshot.Scope.AccessState);
            Assert.NotEqual(fallbackDefinitionKey, position.PrimarySelection?.Id);
            Assert.DoesNotContain(
                position.SelectedEntities,
                entity => entity.Kind == "process-definition" && entity.Id == fallbackDefinitionKey);
            Assert.DoesNotContain(
                position.Facts,
                IsSelectedDefinitionFact);
        });
    }

    [Fact]
    public void Process_workspace_publishes_the_exact_definition_selected_by_process_id()
    {
        using var context = CreateContext(out var client);
        const string targetDefinitionKey = "architecture-decision-governance";
        var targetProcessId = ProcessDefinitionCatalogProjectionService.CreateDefinitionId(
            new ProcessDefinitionCatalogItemKey(targetDefinitionKey)).Value;
        var registry = context.Services.GetRequiredService<IAgentChatContextRegistry>();
        client.ShellResultTransform = (_, projection) =>
        {
            var target = Assert.Single(
                projection.DefinitionCatalog.Items,
                item => item.Key.Value == targetDefinitionKey);
            return projection with
            {
                DefinitionCatalog = projection.DefinitionCatalog with
                {
                    SelectedDefinitionKey = target.Key,
                    SelectedItem = target,
                    SelectedEditor = null
                }
            };
        };

        var cut = context.Render<ProcessWorkspaceShell>(parameters => parameters
            .Add(component => component.ProcessIdQuery, targetProcessId));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(AgentChatContextAccessState.Ready, snapshot.Scope.AccessState);
            Assert.Equal(targetDefinitionKey, snapshot.Scope.SurfacePosition?.PrimarySelection?.Id);
        });
    }

    [Fact]
    public void Process_workspace_fails_closed_when_an_explicit_process_id_does_not_exist()
    {
        using var context = CreateContext(out _);
        var missingProcessId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var registry = context.Services.GetRequiredService<IAgentChatContextRegistry>();

        var cut = context.Render<ProcessWorkspaceShell>(parameters => parameters
            .Add(component => component.ProcessIdQuery, missingProcessId));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            var position = Assert.IsType<AgentChatSurfacePosition>(snapshot.Scope.SurfacePosition);
            Assert.Equal(AgentChatContextAccessState.Failed, snapshot.Scope.AccessState);
            Assert.Null(position.PrimarySelection);
            Assert.Empty(position.SelectedEntities);
            Assert.DoesNotContain(
                position.Facts,
                IsSelectedDefinitionFact);
        });
    }

    [Fact]
    public void Process_workspace_fails_closed_when_process_id_conflicts_with_definition_key()
    {
        using var context = CreateContext(out _);
        const string requestedDefinitionKey = "blazor-app-delivery";
        var conflictingProcessId = ProcessDefinitionCatalogProjectionService.CreateDefinitionId(
            new ProcessDefinitionCatalogItemKey("architecture-decision-governance")).Value;
        var registry = context.Services.GetRequiredService<IAgentChatContextRegistry>();

        var cut = context.Render<ProcessWorkspaceShell>(parameters => parameters
            .Add(component => component.ProcessIdQuery, conflictingProcessId)
            .Add(component => component.SelectedDefinitionKey, requestedDefinitionKey));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            var position = Assert.IsType<AgentChatSurfacePosition>(snapshot.Scope.SurfacePosition);
            Assert.Equal(AgentChatContextAccessState.Failed, snapshot.Scope.AccessState);
            Assert.Null(position.PrimarySelection);
            Assert.Empty(position.SelectedEntities);
            Assert.DoesNotContain(
                position.Facts,
                IsSelectedDefinitionFact);
        });
    }

    [Fact]
    public void Process_workspace_fails_closed_for_an_unresolved_launch_plan_route()
    {
        using var context = CreateContext(out _);
        var launchPlanId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var registry = context.Services.GetRequiredService<IAgentChatContextRegistry>();

        var cut = context.Render<ProcessWorkspaceShell>(parameters => parameters
            .Add(component => component.LaunchPlanIdQuery, launchPlanId));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            var position = Assert.IsType<AgentChatSurfacePosition>(snapshot.Scope.SurfacePosition);
            Assert.Equal(AgentChatContextAccessState.Failed, snapshot.Scope.AccessState);
            Assert.Null(position.PrimarySelection);
            Assert.Empty(position.SelectedEntities);
            Assert.DoesNotContain(position.Facts, fact => fact.Name == "run-process-name");
            Assert.DoesNotContain(
                position.Facts,
                IsSelectedDefinitionFact);
        });
    }

    [Fact]
    public async Task Live_processes_blocks_a_run_query_transition_until_the_new_run_context_is_published()
    {
        using var context = CreateContext(out _);
        var firstRunId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var secondRunId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var registry = context.Services.GetRequiredService<IAgentChatContextRegistry>();
        navigation.NavigateTo($"/processes/live?runId={firstRunId:D}");
        using var workspaceLease = registry.RegisterWorkspacePosition(
            new AgentChatWorkspacePosition("live", "Live processes", "/processes/live", "page"),
            AgentChatNavigationIdentity.CreateForLocation(navigation.BaseUri, navigation.Uri));

        var cut = context.Render<LiveProcessesDashboard>(parameters => parameters
            .Add(component => component.RunIdQuery, firstRunId));
        cut.WaitForAssertion(() =>
            Assert.Equal(
                firstRunId.ToString("D"),
                registry.Capture()?.Scope.SurfacePosition?.PrimarySelection?.Id));
        var scopeId = Assert.IsType<AgentChatContextSnapshot>(registry.Capture()).Scope.Id;

        var nextLocation = navigation.ToAbsoluteUri($"/processes/live?runId={secondRunId:D}").AbsoluteUri;
        var nextNavigationIdentity = AgentChatNavigationIdentity.CreateForLocation(
            navigation.BaseUri,
            nextLocation);
        var staleSurfaceIdentity = AgentChatNavigationIdentity.CreateForLocation(
            navigation.BaseUri,
            nextLocation,
            [new("runId", firstRunId.ToString("D"))]);
        Assert.NotEqual(staleSurfaceIdentity, nextNavigationIdentity);
        workspaceLease.Update(
            new AgentChatWorkspacePosition("live", "Live processes", "/processes/live", "page"),
            nextNavigationIdentity);

        Assert.Null(registry.Capture());
        var mismatch = await Assert.ThrowsAsync<AgentChatContextPositionMismatchException>(
            async () => await registry.CaptureAsync());
        Assert.Equal(AgentChatContextPositionMismatchReason.NavigationChanged, mismatch.Reason);

        cut.Render(parameters => parameters
            .Add(component => component.RunIdQuery, secondRunId));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(scopeId, snapshot.Scope.Id);
            Assert.Equal(
                secondRunId.ToString("D"),
                snapshot.Scope.SurfacePosition?.PrimarySelection?.Id);
        });
    }

    [Fact]
    public void Live_processes_ignores_a_late_projection_from_a_superseded_run_selection()
    {
        using var context = CreateContext(out var client);
        var firstRunId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var secondRunId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var registry = context.Services.GetRequiredService<IAgentChatContextRegistry>();
        client.DeferShellRequests = true;

        var cut = context.Render<LiveProcessesDashboard>(parameters => parameters
            .Add(component => component.RunIdQuery, firstRunId));
        cut.WaitForAssertion(() => Assert.Single(client.Requests));

        cut.Render(parameters => parameters
            .Add(component => component.RunIdQuery, secondRunId));
        cut.WaitForAssertion(() => Assert.Equal(2, client.Requests.Count));
        Assert.Equal(
            AgentChatContextAccessState.Loading,
            Assert.IsType<AgentChatContextSnapshot>(registry.Capture()).Scope.AccessState);

        client.CompleteShellRequest(1);
        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(AgentChatContextAccessState.Ready, snapshot.Scope.AccessState);
            Assert.Equal(secondRunId.ToString("D"), snapshot.Scope.SurfacePosition?.PrimarySelection?.Id);
        });

        client.CompleteShellRequest(0);
        cut.WaitForAssertion(() => Assert.Equal(
            secondRunId.ToString("D"),
            registry.Capture()?.Scope.SurfacePosition?.PrimarySelection?.Id));
    }

    [Fact]
    public void Live_processes_ignores_a_late_filtered_projection_from_a_superseded_project()
    {
        using var context = CreateContext(out var client);
        var firstRunId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var secondRunId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var nextProjectId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var registry = context.Services.GetRequiredService<IAgentChatContextRegistry>();
        client.DeferShellRequests = true;
        client.ShellResultTransform = (requestIndex, projection) => requestIndex == 1
            ? projection with
            {
                Runtime = projection.Runtime with
                {
                    Runs = projection.Runtime.Runs
                        .Where(run => run.RunId.Value == secondRunId)
                        .ToArray()
                }
            }
            : projection;

        var cut = context.Render<LiveProcessesDashboard>(parameters => parameters
            .Add(component => component.RunIdQuery, firstRunId));
        cut.WaitForAssertion(() => Assert.Single(client.Requests));
        SetPrivateField<Guid?>(cut.Instance, "requiredRouteRunId", null);
        SetPrivateField(cut.Instance, "statusFilter", ProcessProjectedRunStatus.Active);

        client.CompleteShellRequest(0);
        Assert.True(
            SpinWait.SpinUntil(() => client.Requests.Count == 2, TimeSpan.FromSeconds(10)),
            "The filtered follow-up projection request was not started.");

        SetPrivateField(cut.Instance, "statusFilter", ProcessProjectedRunStatus.NeedsAttention);
        cut.Render(parameters => parameters
            .Add(component => component.ProjectId, nextProjectId)
            .Add(component => component.RunIdQuery, firstRunId));
        cut.WaitForAssertion(() => Assert.Equal(3, client.Requests.Count));

        client.CompleteShellRequest(2);
        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(AgentChatContextAccessState.Ready, snapshot.Scope.AccessState);
            Assert.Equal(firstRunId.ToString("D"), snapshot.Scope.SurfacePosition?.PrimarySelection?.Id);
            Assert.Contains(
                snapshot.Scope.SurfacePosition!.Facts,
                fact => fact.Name == "run-status" && fact.Value == ProcessProjectedRunStatus.NeedsAttention.ToString());
        });

        client.CompleteShellRequest(1);
        Assert.True(
            SpinWait.SpinUntil(() => client.CompletedShellRequestCount == 3, TimeSpan.FromSeconds(10)),
            "The superseded filtered projection did not finish.");
        var finalSnapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        Assert.Equal(firstRunId.ToString("D"), finalSnapshot.Scope.SurfacePosition?.PrimarySelection?.Id);
        Assert.Contains(
            finalSnapshot.Scope.SurfacePosition!.Facts,
            fact => fact.Name == "run-status" && fact.Value == ProcessProjectedRunStatus.NeedsAttention.ToString());
    }

    [Fact]
    public void Process_workspace_ignores_a_late_projection_from_a_superseded_definition_selection()
    {
        using var context = CreateContext(out var client);
        var registry = context.Services.GetRequiredService<IAgentChatContextRegistry>();
        client.DeferShellRequests = true;

        var cut = context.Render<ProcessWorkspaceShell>(parameters => parameters
            .Add(component => component.SelectedDefinitionKey, "blazor-app-delivery"));
        cut.WaitForAssertion(() => Assert.Single(client.Requests));

        cut.Render(parameters => parameters
            .Add(component => component.SelectedDefinitionKey, "architecture-decision-governance"));
        cut.WaitForAssertion(() => Assert.Equal(2, client.Requests.Count));
        Assert.Equal(
            AgentChatContextAccessState.Loading,
            Assert.IsType<AgentChatContextSnapshot>(registry.Capture()).Scope.AccessState);

        client.CompleteShellRequest(1);
        cut.WaitForAssertion(() => Assert.Equal(
            "architecture-decision-governance",
            registry.Capture()?.Scope.SurfacePosition?.PrimarySelection?.Id));

        client.CompleteShellRequest(0);
        cut.WaitForAssertion(() => Assert.Equal(
            "architecture-decision-governance",
            registry.Capture()?.Scope.SurfacePosition?.PrimarySelection?.Id));
    }

    [Fact]
    public void Initial_shell_requests_lean_definition_and_runtime_projection()
    {
        using var context = CreateContext(out var client);

        var cut = context.Render<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(client.LastRequest));
        var request = client.LastRequest!;
        var definitionOptions = request.DefinitionLoadOptions;
        var runtimeOptions = request.RuntimeQuery?.LoadOptions;

        Assert.NotNull(definitionOptions);
        Assert.True(definitionOptions.IncludeSelectedEditor);
        Assert.False(definitionOptions.IncludeRoleEditor);
        Assert.False(definitionOptions.IncludeStepEditor);
        Assert.False(definitionOptions.IncludeCanvas);
        Assert.False(definitionOptions.IncludeTemplateCatalog);
        Assert.NotNull(runtimeOptions);
        Assert.False(runtimeOptions.IncludeSelectedRun);
        Assert.False(runtimeOptions.IncludeHistory);
        Assert.False(runtimeOptions.IncludeMetricHistory);
        Assert.False(runtimeOptions.IncludeActiveAgents);
        Assert.False(runtimeOptions.IncludeUsageTelemetry);
        Assert.False(runtimeOptions.LiveProcesses.IncludeAttentionReconciliation);
        Assert.False(runtimeOptions.LiveProcesses.IncludeOperatorActions);
        Assert.False(runtimeOptions.LiveProcesses.IncludeCurrentSteps);
        Assert.False(runtimeOptions.LiveProcesses.IncludeChildRunWaits);
        Assert.Null(request.RuntimeQuery?.PreviouslyLoadedRuns);
    }

    [Fact]
    public void Detail_tabs_request_heavy_projection_shapes_on_demand()
    {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();

        ActivateProcessDetailTab(cut, "processes-detail-tab-roles", "processes-detail-panel-roles");
        var rolesOptions = client.LastRequest?.DefinitionLoadOptions;
        Assert.NotNull(rolesOptions);
        Assert.True(rolesOptions.IncludeRoleEditor);
        Assert.False(rolesOptions.IncludeStepEditor);
        Assert.False(rolesOptions.IncludeCanvas);
        Assert.False(rolesOptions.IncludeTemplateCatalog);

        ActivateProcessDetailTab(cut, "processes-detail-tab-steps", "processes-detail-panel-steps");
        var stepsOptions = client.LastRequest?.DefinitionLoadOptions;
        Assert.NotNull(stepsOptions);
        Assert.False(stepsOptions.IncludeRoleEditor);
        Assert.True(stepsOptions.IncludeStepEditor);
        Assert.True(stepsOptions.IncludeCanvas);
        Assert.False(stepsOptions.IncludeTemplateCatalog);

        ActivateProcessDetailTab(cut, "processes-detail-tab-exchange", "processes-detail-panel-exchange");
        var exchangeOptions = client.LastRequest?.DefinitionLoadOptions;
        Assert.NotNull(exchangeOptions);
        Assert.False(exchangeOptions.IncludeRoleEditor);
        Assert.False(exchangeOptions.IncludeStepEditor);
        Assert.False(exchangeOptions.IncludeCanvas);
        Assert.True(exchangeOptions.IncludeTemplateCatalog);

        ActivateProcessDetailTab(cut, "processes-detail-tab-runs", "processes-detail-panel-runs");
        var runsRuntimeOptions = client.LastRequest?.RuntimeQuery?.LoadOptions;
        Assert.NotNull(runsRuntimeOptions);
        Assert.True(runsRuntimeOptions.IncludeSelectedRun);
        Assert.True(runsRuntimeOptions.IncludeHistory);
        Assert.False(runsRuntimeOptions.IncludeMetricHistory);
        Assert.True(runsRuntimeOptions.IncludeActiveAgents);
        Assert.False(runsRuntimeOptions.IncludeUsageTelemetry);
        Assert.False(client.LastRequest?.DefinitionLoadOptions?.IncludeSelectedEditor);
        Assert.NotNull(client.LastRequest?.RuntimeQuery?.PreviouslyLoadedRuns);

        var requestCountBeforeGraphGate = client.Requests.Count;
        ActivateProcessDetailTab(cut, "processes-detail-tab-graphs", "processes-detail-panel-graphs");
        Assert.Equal(requestCountBeforeGraphGate, client.Requests.Count);

        cut.Find("[data-testid='processes-process-graphs-load-button']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(requestCountBeforeGraphGate + 1, client.Requests.Count);
            var loadedGraphRuntimeOptions = client.LastRequest?.RuntimeQuery?.LoadOptions;
            Assert.NotNull(loadedGraphRuntimeOptions);
            Assert.False(loadedGraphRuntimeOptions.IncludeSelectedRun);
            Assert.False(loadedGraphRuntimeOptions.IncludeHistory);
            Assert.True(loadedGraphRuntimeOptions.IncludeMetricHistory);
            Assert.False(loadedGraphRuntimeOptions.IncludeActiveAgents);
            Assert.True(loadedGraphRuntimeOptions.IncludeUsageTelemetry);
            Assert.Equal(ProcessRuntimeHistoryWindow.LiveHour, client.LastRequest?.RuntimeQuery?.HistoryWindow);
            Assert.NotNull(client.LastRequest?.RuntimeQuery?.PreviouslyLoadedRuns);
        });

        ActivateProcessDetailTab(cut, "processes-detail-tab-manager-chat", "processes-detail-panel-manager-chat");
        var managerRuntimeOptions = client.LastRequest?.RuntimeQuery?.LoadOptions;
        Assert.NotNull(managerRuntimeOptions);
        Assert.Equal(ProcessRuntimeHistoryWindow.OneDay, client.LastRequest?.RuntimeQuery?.HistoryWindow);
        Assert.True(client.LastRequest?.RuntimeQuery?.AutoSelectRun);
        Assert.True(managerRuntimeOptions.IncludeSelectedRun);
        Assert.False(managerRuntimeOptions.IncludeHistory);
        Assert.False(managerRuntimeOptions.IncludeMetricHistory);
        Assert.False(managerRuntimeOptions.IncludeActiveAgents);
        Assert.True(managerRuntimeOptions.IncludeUsageTelemetry);
        Assert.False(managerRuntimeOptions.LiveProcesses.IncludeAttentionReconciliation);
        Assert.False(managerRuntimeOptions.LiveProcesses.IncludeOperatorActions);
        Assert.False(managerRuntimeOptions.LiveProcesses.IncludeCurrentSteps);
        Assert.False(managerRuntimeOptions.LiveProcesses.IncludeChildRunWaits);
    }

    [Fact]
    public void Project_shell_passes_project_scope_and_selection_to_projection_client()
    {
        using var context = CreateContext(out var client);
        var projectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var processId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var runId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var cut = context.Render<ProcessWorkspaceShell>(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.ProcessIdQuery, processId)
            .Add(component => component.RunIdQuery, runId));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-detail-panel-runs']")));
        Assert.Equal(ProcessWorkspaceScopeKind.Project, client.LastRequest?.Scope.Kind);
        Assert.Equal(projectId, client.LastRequest?.Scope.ProjectId);
        Assert.Equal(processId, client.LastRequest?.Selection.ProcessId);
        Assert.Equal(runId, client.LastRequest?.Selection.RunId);
    }

    [Fact]
    public void Refresh_button_requests_forced_projection_refresh()
    {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-refresh']")));
        cut.Find("[data-testid='processes-refresh']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.True(client.Requests.Last().ForceRefresh);
            Assert.Null(client.Requests.Last().RuntimeQuery?.PreviouslyLoadedRuns);
        });
        Assert.Contains("Refresh requested", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Definition_search_passes_query_to_projection_client()
    {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-definition-search']")));
        cut.Find("[data-testid='processes-definition-search']").Input("architecture");
        cut.Find("[data-testid='processes-definition-search-submit']").Click();

        cut.WaitForAssertion(() => Assert.Equal("architecture", client.Requests.Last().DefinitionCatalogQuery.SearchText));
        Assert.Contains("Architecture decision governance", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Definition_scope_filter_passes_scope_to_projection_client()
    {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-definition-tree']")));
        cut.Find("[data-testid='processes-definition-scope-project']").Click();

        cut.WaitForAssertion(() => Assert.Equal(ProcessDefinitionCatalogScopeKind.Project, client.Requests.Last().DefinitionCatalogQuery.ScopeFilter));
        Assert.Contains("No definitions match the current search", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Feed_defaults_button_uses_application_command_boundary()
    {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-feed-defaults']")));
        cut.Find("[data-testid='processes-feed-defaults']").Click();

        cut.WaitForAssertion(() => Assert.Equal(1, client.FeedDefaultsCommandCount));
        Assert.Contains("default process definition", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Refresh token", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Projection_service_rejects_mismatched_scope_state()
    {
        var clock = new FixedProcessProjectionClock(Now);
        var service = new ProcessWorkspaceShellProjectionService(
            clock,
            new ProcessDefinitionCatalogProjectionService(clock),
            new ProcessDefinitionEditorProjectionService(clock),
            new ProcessDefinitionRoleEditorProjectionService(clock),
            new ProcessDefinitionCanvasEditorProjectionService(clock),
            new ProcessDefinitionStepEditorProjectionService(clock),
            new ProcessTemplateCatalogProjectionService(clock));
        var selection = new ProcessWorkspaceSelectionProjection(
            ProcessId: null,
            RunId: null,
            LaunchPlanId: null);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetShellAsync(new ProcessWorkspaceShellRequest(
            new ProcessWorkspaceShellScope(ProcessWorkspaceScopeKind.Project, ProjectId: null),
            selection,
            new ProcessDefinitionCatalogQueryProjection(SearchText: null, SelectedDefinitionKey: null, ScopeFilter: ProcessDefinitionCatalogScopeKind.All, Take: 50),
            new ProcessTemplateCatalogQueryProjection(SearchText: null, ProcessTemplateCatalogCategoryKind.All, SelectedItemKey: null, ProcessTemplateCatalogPreviewTabKind.Overview, Take: 50),
            ForceRefresh: false)));

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetShellAsync(new ProcessWorkspaceShellRequest(
            new ProcessWorkspaceShellScope(ProcessWorkspaceScopeKind.Global, Guid.Parse("55555555-5555-5555-5555-555555555555")),
            selection,
            new ProcessDefinitionCatalogQueryProjection(SearchText: null, SelectedDefinitionKey: null, ScopeFilter: ProcessDefinitionCatalogScopeKind.All, Take: 50),
            new ProcessTemplateCatalogQueryProjection(SearchText: null, ProcessTemplateCatalogCategoryKind.All, SelectedItemKey: null, ProcessTemplateCatalogPreviewTabKind.Overview, Take: 50),
            ForceRefresh: false)));
    }

    [Fact]
    public void Agent_context_button_uses_projected_context_key()
    {
        using var context = CreateContext(out _);
        var runId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var cut = context.Render<ProcessWorkspaceShell>(parameters => parameters
            .Add(component => component.RunIdQuery, runId));
        var navigation = context.Services.GetRequiredService<NavigationManager>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-agent-context']")));
        cut.Find("[data-testid='processes-agent-context']").Click();

        Assert.Contains("/agents?processContext=", navigation.Uri, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString($"processes:workspace:run:{runId:N}"), navigation.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void Definition_editor_renders_authoring_sections_from_projection()
    {
        using var context = CreateContext(out _);

        var cut = context.Render<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-definition-editor']")));
        Assert.Contains("Identity", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Governance", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Contracts", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Simulation", cut.Markup, StringComparison.Ordinal);
        Assert.Equal("Blazor app delivery", cut.Find("[data-testid='processes-definition-editor-name']").GetAttribute("value"));
        Assert.NotNull(cut.Find("[data-testid='processes-definition-editor-manager-override']"));
    }

    [Fact]
    public void Definition_save_uses_typed_editor_command_boundary()
    {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-definition-save']")));
        cut.Find("[data-testid='processes-definition-editor-owner']").Input("Architecture owner");
        cut.Find("[data-testid='processes-definition-editor-manager-override']").Input("Use the architecture board manager.");
        cut.Find("[data-testid='processes-definition-save']").Click();

        cut.WaitForAssertion(() => Assert.Equal(ProcessDefinitionEditorCommandKind.SaveDraft, client.LastEditorCommand?.CommandKind));
        Assert.Contains("Draft saved", cut.Markup, StringComparison.Ordinal);
        Assert.Equal("Architecture owner", client.LastEditorCommand?.Draft.Identity.OwnerName);
        Assert.Equal("Use the architecture board manager.", client.LastEditorCommand?.Draft.Governance.ManagerOverrideSummary);
        Assert.NotNull(cut.Find("[data-testid='processes-definition-editor']"));
    }

    [Fact]
    public async Task Definition_editor_ignores_a_late_command_result_after_selecting_another_definition()
    {
        using var context = CreateContext(out var client);
        var registry = context.Services.GetRequiredService<IAgentChatContextRegistry>();
        client.DeferEditorCommands = true;
        var cut = context.Render<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-definition-save']")));
        var commandTask = cut.Find("[data-testid='processes-definition-save']")
            .ClickAsync(new MouseEventArgs());
        Assert.True(
            SpinWait.SpinUntil(() => client.EditorCommandCount == 1, TimeSpan.FromSeconds(10)),
            "The definition editor command did not start.");
        Assert.Equal(
            AgentChatContextAccessState.Loading,
            Assert.IsType<AgentChatContextSnapshot>(registry.Capture()).Scope.AccessState);

        await cut.Find("[data-testid='processes-definition-architecture-decision-governance']")
            .ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(
                "Architecture decision governance",
                cut.Find("[data-testid='processes-definition-editor-name']").GetAttribute("value"));
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(AgentChatContextAccessState.Ready, snapshot.Scope.AccessState);
            Assert.Equal(
                "architecture-decision-governance",
                snapshot.Scope.SurfacePosition?.PrimarySelection?.Id);
        });

        client.CompleteEditorCommand(0);
        await commandTask;
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(
                "Architecture decision governance",
                cut.Find("[data-testid='processes-definition-editor-name']").GetAttribute("value"));
            Assert.DoesNotContain("Draft saved.", cut.Markup, StringComparison.Ordinal);
            Assert.Equal(
                "architecture-decision-governance",
                registry.Capture()?.Scope.SurfacePosition?.PrimarySelection?.Id);
        });
    }

    [Fact]
    public void Definition_publish_shows_blocking_lint_errors()
    {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-definition-publish']")));
        cut.Find("[data-testid='processes-definition-editor-name']").Input(string.Empty);
        cut.Find("[data-testid='processes-definition-publish']").Click();

        cut.WaitForAssertion(() => Assert.Equal(ProcessDefinitionEditorCommandKind.Publish, client.LastEditorCommand?.CommandKind));
        Assert.Contains("Definition name is required", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Rejected", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Role_editor_renders_roles_templates_and_step_bindings()
    {
        using var context = CreateContext(out _);

        var cut = context.Render<ProcessWorkspaceShell>();
        ActivateProcessDetailTab(cut, "processes-detail-tab-roles", "processes-detail-panel-roles");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-definition-role-editor']")));
        Assert.NotNull(cut.Find("[data-testid='processes-role-card-grid']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='processes-role-card']"));
        Assert.Empty(cut.FindAll("[data-testid='processes-role-display-name']"));

        cut.Find("[data-testid='processes-role-solution-architect']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-role-details-dialog']")));
        Assert.Equal("Solution architect", cut.Find("[data-testid='processes-role-display-name']").GetAttribute("value"));
        Assert.Equal("process-role-template/solution-architect", cut.Find("[data-testid='processes-role-template-source']").GetAttribute("value"));
        Assert.Contains("Solution architect template", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Architecture decision", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Approver", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Role_save_uses_typed_role_command_boundary()
    {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();
        ActivateProcessDetailTab(cut, "processes-detail-tab-roles", "processes-detail-panel-roles");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-role-solution-architect']")));
        cut.Find("[data-testid='processes-role-solution-architect']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-role-save']")));
        cut.Find("[data-testid='processes-role-display-name']").Input("Principal architecture steward");
        cut.Find("[data-testid='processes-role-executor-kind']").Change(ProcessDefinitionRoleExecutorKind.PersonOrAgent.ToString());
        cut.Find("[data-testid='processes-role-project-assignment']").Change(ProcessDefinitionRoleProjectAssignmentKind.Manager.ToString());
        cut.Find("[data-testid='processes-role-allocation']").Input("75");
        cut.Find("[data-testid='processes-role-save']").Click();

        cut.WaitForAssertion(() => Assert.Equal(ProcessDefinitionRoleCommandKind.SaveRole, client.LastRoleCommand?.CommandKind));
        Assert.Equal("Principal architecture steward", client.LastRoleCommand?.Draft.DisplayName);
        Assert.Equal(ProcessDefinitionRoleExecutorKind.PersonOrAgent, client.LastRoleCommand?.Draft.PreferredExecutorKind);
        Assert.Equal(ProcessDefinitionRoleProjectAssignmentKind.Manager, client.LastRoleCommand?.Draft.PreferredProjectAssignmentRole);
        Assert.Equal(75, client.LastRoleCommand?.Draft.DefaultAllocationPercent);
        Assert.Contains("Role saved", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Workflow_role_binding_round_trips_and_is_not_erased_by_later_role_edits()
    {
        var workflowId = Guid.Parse("62000000-0000-0000-0000-000000000001");
        var workflowVersionId = Guid.Parse("62000000-0000-0000-0000-000000000002");
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();
        ActivateProcessDetailTab(cut, "processes-detail-tab-roles", "processes-detail-panel-roles");

        cut.Find("[data-testid='processes-role-solution-architect']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-role-details-dialog']")));
        cut.Find("[data-testid='processes-role-executor-kind']")
            .Change(ProcessDefinitionRoleExecutorKind.Workflow.ToString());
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-role-workflow-binding']")));
        cut.Find("[data-testid='processes-role-workflow-id']").Change(workflowId.ToString("D"));
        cut.Find("[data-testid='processes-role-workflow-version-id']").Change(workflowVersionId.ToString("D"));
        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid='processes-role-save']").HasAttribute("disabled")));
        cut.Find("[data-testid='processes-role-save']").Click();

        cut.WaitForAssertion(() => Assert.Equal(workflowId, client.LastRoleCommand?.Draft.WorkflowPreference.WorkflowDefinitionId));
        Assert.Equal(workflowVersionId, client.LastRoleCommand?.Draft.WorkflowPreference.WorkflowVersionId);
        cut.WaitForAssertion(() => Assert.Equal(
            workflowId.ToString("D"),
            cut.Find("[data-testid='processes-role-workflow-id']").GetAttribute("value")));
        Assert.Equal(
            workflowVersionId.ToString("D"),
            cut.Find("[data-testid='processes-role-workflow-version-id']").GetAttribute("value"));

        cut.Find("[data-testid='processes-role-display-name']").Input("Workflow architecture steward");
        cut.Find("[data-testid='processes-role-save']").Click();

        cut.WaitForAssertion(() => Assert.Equal("Workflow architecture steward", client.LastRoleCommand?.Draft.DisplayName));
        Assert.Equal(workflowId, client.LastRoleCommand?.Draft.WorkflowPreference.WorkflowDefinitionId);
        Assert.Equal(workflowVersionId, client.LastRoleCommand?.Draft.WorkflowPreference.WorkflowVersionId);
    }

    [Fact]
    public void Workflow_role_binding_rejects_invalid_guid_before_save()
    {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();
        ActivateProcessDetailTab(cut, "processes-detail-tab-roles", "processes-detail-panel-roles");

        cut.Find("[data-testid='processes-role-solution-architect']").Click();
        cut.Find("[data-testid='processes-role-executor-kind']")
            .Change(ProcessDefinitionRoleExecutorKind.Workflow.ToString());
        cut.Find("[data-testid='processes-role-workflow-id']").Change("not-a-guid");

        cut.WaitForAssertion(() => Assert.Contains(
            "must be a non-empty GUID",
            cut.Find("[data-testid='processes-role-workflow-id-error']").TextContent,
            StringComparison.Ordinal));
        Assert.True(cut.Find("[data-testid='processes-role-save']").HasAttribute("disabled"));
        Assert.Null(client.LastRoleCommand);
    }

    [Fact]
    public void Role_apply_template_uses_selected_template_action()
    {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();
        ActivateProcessDetailTab(cut, "processes-detail-tab-roles", "processes-detail-panel-roles");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-role-solution-architect']")));
        cut.Find("[data-testid='processes-role-solution-architect']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-role-apply-template']")));
        cut.Find("[data-testid='processes-role-template-action']").Change("role-template.solution-architect");
        cut.Find("[data-testid='processes-role-apply-template']").Click();

        cut.WaitForAssertion(() => Assert.Equal(ProcessDefinitionRoleCommandKind.ApplyTemplate, client.LastRoleCommand?.CommandKind));
        Assert.Equal(new ProcessDefinitionRoleTemplateActionKey("role-template.solution-architect"), client.LastRoleCommand?.TemplateActionKey);
        Assert.Equal(ProcessDefinitionRoleTemplateOverrideStatus.AppliedFromTemplate, client.LastRoleCommand?.Draft.OverrideStatus);
        Assert.Contains("Role template applied", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Canvas_renders_shared_workbench_nodes_toolbox_selection_and_route_edges()
    {
        using var context = CreateContext(out _);

        var cut = context.Render<ProcessWorkspaceShell>();
        ActivateProcessDetailTab(cut, "processes-detail-tab-steps", "processes-detail-panel-steps");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-definition-canvas']")));
        var workbench = cut.FindComponent<CanvasWorkbench>();
        Assert.Contains(workbench.Instance.Surface.Nodes, node => node.Id == "step:architecture-decision");
        Assert.Contains(workbench.Instance.Surface.Nodes, node => node.Id == "branch:architecture-decision");
        Assert.Contains(workbench.Instance.Surface.Nodes, node => node.Id == "role:solution-architect");
        Assert.Contains(workbench.Instance.Surface.Nodes, node => node.Id == "artifact:architecture-decision:adr");
        Assert.Contains(workbench.Instance.Surface.Links, link =>
            string.Equals(link.Kind, ProcessDefinitionCanvasEdgeKind.BranchRoute.ToString(), StringComparison.Ordinal) &&
            link.SourceId == "step:architecture-decision" &&
            link.TargetId == "branch:architecture-decision");
        var stepNode = workbench.Instance.Surface.Nodes.Single(node => node.Id == "step:architecture-decision");
        var roleNode = workbench.Instance.Surface.Nodes.Single(node => node.Id == "role:solution-architect");
        Assert.Contains(stepNode.InputPorts, port => port.Id == "process:role");
        Assert.Contains(roleNode.OutputPorts, port => port.Id == "process:role");
        Assert.Contains(workbench.Instance.Surface.Links, link =>
            string.Equals(link.Kind, ProcessDefinitionCanvasEdgeKind.RoleBinding.ToString(), StringComparison.Ordinal) &&
            link.SourcePortId == "process:role" &&
            link.TargetPortId == "process:role");
        Assert.NotNull(cut.Find("[data-testid='processes-canvas-toolbox-window']"));
        Assert.NotNull(cut.Find("[data-testid='processes-canvas-toolbox']"));
        Assert.NotNull(cut.Find("[data-testid='processes-canvas-node-step-architecture-decision']"));
        Assert.NotNull(cut.Find("[data-testid='processes-canvas-node-branch-architecture-decision']"));
        Assert.NotNull(cut.Find("[data-testid='processes-canvas-node-role-solution-architect']"));
        Assert.NotNull(cut.Find("[data-testid='processes-canvas-node-artifact-architecture-decision-adr']"));
        Assert.NotNull(cut.Find("[data-testid='processes-canvas-edge-branch-route-architecture-decision-router']"));
        Assert.All(
            cut.FindComponents<CanvasFloatingWindow>(),
            window => Assert.Equal(".cw-toolbar", window.Instance.SafeTopSelector));

        await cut.InvokeAsync(() => workbench.Instance.OnSelectionChanged("artifact:architecture-decision:adr", "[\"artifact:architecture-decision:adr\"]", 1));

        cut.WaitForAssertion(() => Assert.Contains("Architecture decision record", cut.Find("[data-testid='processes-canvas-selection']").TextContent, StringComparison.Ordinal));
        Assert.Contains("Artifact", cut.Find("[data-testid='processes-canvas-selection']").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Canvas_toolbox_action_uses_typed_canvas_command_boundary()
    {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();
        ActivateProcessDetailTab(cut, "processes-detail-tab-steps", "processes-detail-panel-steps");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-canvas-toolbox-process-step-implementation']")));
        cut.Find("[data-testid='processes-canvas-node-step-architecture-decision']").Click();
        cut.Find("[data-testid='processes-canvas-toolbox-process-step-implementation']").Click();

        cut.WaitForAssertion(() => Assert.Equal(ProcessDefinitionCanvasCommandKind.AddStep, client.LastCanvasCommand?.CommandKind));
        Assert.Equal(new ProcessDefinitionCanvasToolboxActionKey("process-step.implementation"), client.LastCanvasCommand?.ToolboxActionKey);
        Assert.Equal(new ProcessDefinitionCanvasNodeKey("step:architecture-decision"), client.LastCanvasCommand?.SelectedNodeKey);
        Assert.Contains("Canvas command accepted", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Canvas_artifact_context_actions_clone_and_highlight_references()
    {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();
        ActivateProcessDetailTab(cut, "processes-detail-tab-steps", "processes-detail-panel-steps");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-definition-canvas']")));
        var workbench = cut.FindComponent<CanvasWorkbench>();
        var artifactNode = workbench.Instance.Surface.Nodes.Single(node => node.Id == "artifact:architecture-decision:adr");
        var cloneAction = artifactNode.ContextActions.Single(action =>
            string.Equals(action.Label, "Clone", StringComparison.Ordinal) &&
            action.ActionId.StartsWith("process-canvas:artifact-reference:clone:", StringComparison.Ordinal));
        var highlightAction = artifactNode.ContextActions.Single(action =>
            string.Equals(action.Label, "Highlight", StringComparison.Ordinal) &&
            action.ActionId.StartsWith("process-canvas:artifact-reference:highlight:", StringComparison.Ordinal));

        await cut.InvokeAsync(() => workbench.Instance.OnContextAction(artifactNode.Id, cloneAction.ActionId, 0, 0));

        cut.WaitForAssertion(() => Assert.Equal(ProcessDefinitionCanvasCommandKind.CloneArtifactReference, client.LastCanvasCommand?.CommandKind));
        Assert.Equal(new ProcessDefinitionCanvasNodeKey("artifact:architecture-decision:adr"), client.LastCanvasCommand?.SelectedNodeKey);
        Assert.Null(client.LastCanvasCommand?.ToolboxActionKey);

        workbench = cut.FindComponent<CanvasWorkbench>();
        artifactNode = workbench.Instance.Surface.Nodes.Single(node => node.Id == "artifact:architecture-decision:adr");
        highlightAction = artifactNode.ContextActions.Single(action =>
            string.Equals(action.Label, "Highlight", StringComparison.Ordinal));
        await cut.InvokeAsync(() => workbench.Instance.OnContextAction(artifactNode.Id, highlightAction.ActionId, 0, 0));

        cut.WaitForAssertion(() =>
        {
            var highlighted = cut.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes.Single(node => node.Id == "artifact:architecture-decision:adr");
            Assert.Equal("Highlighted artifact", highlighted.StatusPill);
            Assert.Contains(highlighted.Chips, chip => string.Equals(chip.Text, "Highlighted", StringComparison.Ordinal));
        });
        Assert.Contains("Highlighted 1 canvas reference(s)", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(ProcessDefinitionCanvasCommandKind.CloneArtifactReference, client.LastCanvasCommand?.CommandKind);
    }

    [Fact]
    public async Task Canvas_role_context_actions_clone_and_highlight_shared_representations()
    {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();
        ActivateProcessDetailTab(cut, "processes-detail-tab-steps", "processes-detail-panel-steps");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-definition-canvas']")));
        var workbench = cut.FindComponent<CanvasWorkbench>();
        var roleNode = workbench.Instance.Surface.Nodes.Single(node => node.Id == "role:solution-architect");
        var cloneAction = roleNode.ContextActions.Single(action =>
            string.Equals(action.Label, "Clone", StringComparison.Ordinal) &&
            action.ActionId.StartsWith("process-canvas:role-reference:clone:", StringComparison.Ordinal));
        var highlightAction = roleNode.ContextActions.Single(action =>
            string.Equals(action.Label, "Highlight", StringComparison.Ordinal) &&
            action.ActionId.StartsWith("process-canvas:role-reference:highlight:", StringComparison.Ordinal));

        await cut.InvokeAsync(() => workbench.Instance.OnContextAction(roleNode.Id, cloneAction.ActionId, 0, 0));

        cut.WaitForAssertion(() => Assert.Equal(ProcessDefinitionCanvasCommandKind.CloneRoleReference, client.LastCanvasCommand?.CommandKind));
        Assert.Equal(new ProcessDefinitionCanvasNodeKey("role:solution-architect"), client.LastCanvasCommand?.SelectedNodeKey);
        Assert.Null(client.LastCanvasCommand?.ToolboxActionKey);

        workbench = cut.FindComponent<CanvasWorkbench>();
        roleNode = workbench.Instance.Surface.Nodes.Single(node => node.Id == "role:solution-architect");
        highlightAction = roleNode.ContextActions.Single(action => string.Equals(action.Label, "Highlight", StringComparison.Ordinal));
        await cut.InvokeAsync(() => workbench.Instance.OnContextAction(roleNode.Id, highlightAction.ActionId, 0, 0));

        cut.WaitForAssertion(() =>
        {
            var highlighted = cut.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes.Single(node => node.Id == "role:solution-architect");
            Assert.Equal("Highlighted role", highlighted.StatusPill);
            Assert.Contains(highlighted.Chips, chip => string.Equals(chip.Text, "Highlighted", StringComparison.Ordinal));
        });
        Assert.Contains("Highlighted 1 canvas representation(s)", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(ProcessDefinitionCanvasCommandKind.CloneRoleReference, client.LastCanvasCommand?.CommandKind);
    }

    [Fact]
    public void Canvas_recompose_uses_typed_canvas_command_boundary()
    {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();
        ActivateProcessDetailTab(cut, "processes-detail-tab-steps", "processes-detail-panel-steps");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-canvas-recompose']")));
        cut.Find("[data-testid='processes-canvas-recompose']").Click();

        cut.WaitForAssertion(() => Assert.Equal(ProcessDefinitionCanvasCommandKind.Recompose, client.LastCanvasCommand?.CommandKind));
        Assert.Equal(ProcessDefinitionCanvasRecompositionMode.BalancedFlow, client.LastCanvasCommand?.RecompositionMode);
        Assert.Contains("Canvas recomposed", cut.Markup, StringComparison.Ordinal);
        Assert.NotNull(cut.Find("[data-testid='processes-definition-canvas']"));
    }

    [Fact]
    public async Task Canvas_preserves_viewport_state_when_accepted_recompose_refreshes_projection()
    {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();
        ActivateProcessDetailTab(cut, "processes-detail-tab-steps", "processes-detail-panel-steps");

        cut.WaitForElement("button[title='Maximize canvas']").Click();
        cut.WaitForAssertion(() =>
            Assert.True(cut.FindComponent<CanvasWorkbench>().Instance.Surface.UiState.IsMaximized));

        var viewportState = cut.FindComponent<CanvasWorkbench>().Instance.Surface.UiState;
        viewportState.Zoom = 0.72;
        viewportState.PanX = 321;
        viewportState.PanY = -45;
        await cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnStateChanged(
            viewportState.ToJson(),
            1));

        cut.Find("[data-testid='processes-canvas-recompose']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(ProcessDefinitionCanvasCommandKind.Recompose, client.LastCanvasCommand?.CommandKind);
            var refreshedState = cut.FindComponent<CanvasWorkbench>().Instance.Surface.UiState;
            Assert.True(refreshedState.IsMaximized);
            Assert.Equal(0.72, refreshedState.Zoom, 2);
            Assert.Equal(321, refreshedState.PanX, 2);
            Assert.Equal(-45, refreshedState.PanY, 2);
        });
    }

    [Fact]
    public async Task Canvas_node_move_uses_typed_canvas_command_boundary()
    {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();
        ActivateProcessDetailTab(cut, "processes-detail-tab-steps", "processes-detail-panel-steps");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-definition-canvas']")));
        var workbench = cut.FindComponent<CanvasWorkbench>();

        await cut.InvokeAsync(() => workbench.Instance.OnNodesMoved(JsonSerializer.Serialize<IReadOnlyList<CanvasWorkbenchNodePositionChange>>(
        [
            new("step:architecture-decision", 420, 260),
            new("artifact:architecture-decision:adr", 500, 390)
        ])));

        cut.WaitForAssertion(() => Assert.Equal(ProcessDefinitionCanvasCommandKind.MoveNodes, client.LastCanvasCommand?.CommandKind));
        var positions = Assert.IsAssignableFrom<IReadOnlyList<ProcessDefinitionCanvasNodePosition>>(client.LastCanvasCommand?.NodePositions);
        Assert.Collection(
            positions,
            position =>
            {
                Assert.Equal(new ProcessDefinitionCanvasNodeKey("step:architecture-decision"), position.NodeKey);
                Assert.Equal(420, position.X);
                Assert.Equal(260, position.Y);
            },
            position =>
            {
                Assert.Equal(new ProcessDefinitionCanvasNodeKey("artifact:architecture-decision:adr"), position.NodeKey);
                Assert.Equal(500, position.X);
                Assert.Equal(390, position.Y);
            });
    }

    [Fact]
    public void Step_editor_renders_operation_routes_artifacts_and_subprocess_mapping()
    {
        using var context = CreateContext(out _);

        var cut = context.Render<ProcessWorkspaceShell>();
        ActivateProcessDetailTab(cut, "processes-detail-tab-steps", "processes-detail-panel-steps");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-definition-step-editor']")));
        Assert.Equal("Architecture decision", cut.Find("[data-testid='processes-step-title']").GetAttribute("value"));
        Assert.Equal(ProcessDefinitionStepTargetScopeKind.ExternalArtifactDestination.ToString(), cut.Find("[data-testid='processes-step-operation-target-scope']").GetAttribute("value"));
        Assert.NotNull(cut.Find("[data-testid='processes-step-operation-writeexternalartifactdestination']"));
        Assert.NotNull(cut.Find("[data-testid='processes-step-branch-approved']"));
        Assert.NotNull(cut.Find("[data-testid='processes-step-artifact-architecture-decision-record']"));
        Assert.Contains("Delivery default", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Step_save_uses_typed_step_command_boundary()
    {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();
        ActivateProcessDetailTab(cut, "processes-detail-tab-steps", "processes-detail-panel-steps");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-step-save']")));
        cut.Find("[data-testid='processes-step-title']").Input("Architecture decision checkpoint");
        cut.Find("[data-testid='processes-step-operation-target-scope']").Change(ProcessDefinitionStepTargetScopeKind.ExternalProductTargetReadOnly.ToString());
        cut.Find("[data-testid='processes-step-operation-readprojectstructure']").Change(true);
        cut.Find("[data-testid='processes-step-save']").Click();

        cut.WaitForAssertion(() => Assert.Equal(ProcessDefinitionStepCommandKind.SaveStep, client.LastStepCommand?.CommandKind));
        var command = Assert.IsType<ProcessDefinitionStepEditorCommand>(client.LastStepCommand);
        Assert.Equal("Architecture decision checkpoint", command.Draft.Basic.Title);
        Assert.Equal(ProcessDefinitionStepTargetScopeKind.ExternalProductTargetReadOnly, command.Draft.OperationContract.TargetScope);
        Assert.Contains(ProcessDefinitionStepOperationKind.ReadProjectStructure, command.Draft.OperationContract.AllowedOperations);
        Assert.Contains("Step saved", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Step_route_artifact_and_subprocess_commands_use_typed_boundary()
    {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();
        ActivateProcessDetailTab(cut, "processes-detail-tab-steps", "processes-detail-panel-steps");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-step-add-branch-outcome']")));
        cut.Find("[data-testid='processes-step-add-branch-outcome']").Click();
        cut.WaitForAssertion(() => Assert.Equal(ProcessDefinitionStepCommandKind.AddBranchOutcome, client.LastStepCommand?.CommandKind));
        Assert.Contains("Route added", cut.Markup, StringComparison.Ordinal);

        cut.Find("[data-testid='processes-step-add-artifact-expectation']").Click();
        cut.WaitForAssertion(() => Assert.Equal(ProcessDefinitionStepCommandKind.AddArtifactExpectation, client.LastStepCommand?.CommandKind));
        Assert.Contains("Artifact added", cut.Markup, StringComparison.Ordinal);

        cut.Find("[data-testid='processes-step-kind']").Change(ProcessDefinitionStepKind.Subprocess.ToString());
        cut.Find("[data-testid='processes-step-subprocess-definition']").Change("delivery-default");
        cut.Find("[data-testid='processes-step-map-subprocess']").Click();
        cut.WaitForAssertion(() => Assert.Equal(ProcessDefinitionStepCommandKind.MapSubprocess, client.LastStepCommand?.CommandKind));
        Assert.Equal(ProcessDefinitionStepKind.Subprocess, client.LastStepCommand?.Draft.Basic.StepKind);
        Assert.Equal("delivery-default", client.LastStepCommand?.Draft.SubprocessMapping.ProcessKey);
    }

    [Fact]
    public void Template_library_renders_search_categories_and_preview_tabs()
    {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();
        ActivateProcessDetailTab(cut, "processes-detail-tab-exchange", "processes-detail-panel-exchange");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-template-library']")));
        Assert.Contains("Template catalog is projected from canonical JSON", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Blazor app delivery", cut.Markup, StringComparison.Ordinal);
        cut.Find("[data-testid='processes-template-library-search']").Input("architect");
        cut.Find("[data-testid='processes-template-library-search-submit']").Click();

        cut.WaitForAssertion(() => Assert.Equal("architect", client.Requests.Last().TemplateCatalogQuery.SearchText));
        cut.Find("[data-testid='processes-template-library-category-roles']").Click();
        cut.WaitForAssertion(() => Assert.Equal(ProcessTemplateCatalogCategoryKind.Roles, client.Requests.Last().TemplateCatalogQuery.Category));
        Assert.Contains("Solution architect", cut.Markup, StringComparison.Ordinal);

        cut.Find("[data-testid='processes-template-library-preview-tab-json']").Click();
        cut.WaitForAssertion(() => Assert.Equal(ProcessTemplateCatalogPreviewTabKind.Json, client.Requests.Last().TemplateCatalogQuery.PreviewTab));
        Assert.NotNull(cut.Find("[data-testid='processes-template-library-json']"));
        cut.Find("[data-testid='processes-template-library-preview-tab-structure']").Click();
        cut.WaitForAssertion(() => Assert.Equal(ProcessTemplateCatalogPreviewTabKind.Structure, client.Requests.Last().TemplateCatalogQuery.PreviewTab));
        Assert.NotNull(cut.Find("[data-testid='processes-template-library-structure']"));
    }

    [Fact]
    public void Template_library_imports_role_and_artifact_components_with_target_step()
    {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();
        ActivateProcessDetailTab(cut, "processes-detail-tab-exchange", "processes-detail-panel-exchange");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-template-library-import-process']")));
        cut.Find("[data-testid='processes-template-library-import-process']").Click();
        cut.WaitForAssertion(() => Assert.Equal(ProcessTemplateImportCommandKind.ImportProcess, client.LastTemplateImportCommand?.CommandKind));
        Assert.Contains("Process template imported", cut.Markup, StringComparison.Ordinal);

        cut.Find("[data-testid='processes-template-library-import-role-role-blazor-app-delivery-solution-architect']").Click();
        cut.WaitForAssertion(() => Assert.Equal(ProcessTemplateImportCommandKind.ImportRole, client.LastTemplateImportCommand?.CommandKind));
        Assert.Equal(new ProcessTemplateCatalogItemKey("role:blazor-app-delivery:solution-architect"), client.LastTemplateImportCommand?.ItemKey);
        Assert.Contains("Role component imported", cut.Markup, StringComparison.Ordinal);

        cut.Find("[data-testid='processes-template-library-artifact-target']").Change("architecture-decision");
        cut.Find("[data-testid='processes-template-library-import-artifact-artifact-blazor-app-delivery-architecture-decision-architecture-decision-record']").Click();
        cut.WaitForAssertion(() => Assert.Equal(ProcessTemplateImportCommandKind.ImportArtifact, client.LastTemplateImportCommand?.CommandKind));
        Assert.Equal(new ProcessDefinitionStepKey("architecture-decision"), client.LastTemplateImportCommand?.TargetStepKey);
        Assert.Contains("Artifact component imported", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Original_process_workspace_tabs_render_runs_graphs_analytics_and_manager_chat()
    {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();

        ActivateProcessDetailTab(cut, "processes-detail-tab-runs", "processes-detail-panel-runs");
        Assert.NotNull(cut.Find("[data-testid='processes-runs-tab-shell']"));
        Assert.NotNull(cut.Find("[data-testid='processes-runs-tabs']"));
        Assert.Contains("Launch", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Activity", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Control", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Execution", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Coordination", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Evidence", cut.Markup, StringComparison.Ordinal);

        ActivateProcessDetailTab(cut, "processes-detail-tab-graphs", "processes-detail-panel-graphs");
        Assert.NotNull(cut.Find("[data-testid='processes-process-graphs-tab']"));
        Assert.NotNull(cut.Find("[data-testid='processes-process-graphs-load-gate']"));
        Assert.Contains("Load data history", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid='processes-graph-metric-grid']"));

        cut.Find("[data-testid='processes-process-graphs-load-button']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-graph-metric-grid']")));
        Assert.Contains("Cost", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("USD 0.00", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Tokens", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Time", cut.Markup, StringComparison.Ordinal);
        var usageOptions = (CdaChartOptions)typeof(ProcessWorkspaceShell)
            .GetField("RuntimeUsageChartOptions", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;
        var usageSeries = (IReadOnlyList<CdaChartSeries>)typeof(ProcessWorkspaceShell)
            .GetProperty("RuntimeUsageSeries", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(cut.Instance)!;
        Assert.Equal("k tokens / min / USD x1k", usageOptions.YAxisTitle);
        Assert.Equal(["Tokens (k)", "Minutes", "Cost (USD x1k)"], usageSeries.Select(series => series.Name));

        ActivateProcessDetailTab(cut, "processes-detail-tab-analytics", "processes-detail-panel-analytics");
        Assert.NotNull(cut.Find("[data-testid='processes-analytics-tab']"));

        ActivateProcessDetailTab(cut, "processes-detail-tab-manager-chat", "processes-detail-panel-manager-chat");
        Assert.NotNull(cut.Find("[data-testid='processes-manager-chat-tab']"));
        Assert.NotNull(cut.Find("[data-testid='processes-manager-chat-history-window']"));
        Assert.Contains("Run 77777777", cut.Find("[data-testid='processes-manager-chat-run-select']").TextContent, StringComparison.Ordinal);
        Assert.Contains("Run 88888888", cut.Find("[data-testid='processes-manager-chat-run-select']").TextContent, StringComparison.Ordinal);
        Assert.Equal(ProcessRuntimeHistoryWindow.OneDay, client.LastRequest?.RuntimeQuery?.HistoryWindow);
        Assert.Contains("processes:workspace", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Manager_chat_uses_distinct_thread_per_selected_process_run()
    {
        var workspaceService = new RecordingManagerChatWorkspaceService();
        using var context = CreateContext(out var client, workspaceService);
        var firstRunId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var secondRunId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var cut = context.Render<ProcessWorkspaceShell>(parameters => parameters
            .Add(component => component.RunIdQuery, firstRunId));

        ActivateProcessDetailTab(cut, "processes-detail-tab-manager-chat", "processes-detail-panel-manager-chat");

        cut.WaitForAssertion(() => Assert.Contains(firstRunId.ToString("D"), workspaceService.LastWorkspaceSessionTitle, StringComparison.Ordinal));
        Assert.Equal(ProcessRuntimeHistoryWindow.OneDay, client.LastRequest?.RuntimeQuery?.HistoryWindow);
        var firstSessionId = workspaceService.LastWorkspaceSessionId;

        cut.Find("[data-testid='processes-manager-chat-history-window']").Change(ProcessRuntimeHistoryWindow.SevenDays.ToString());
        cut.WaitForAssertion(() => Assert.Equal(ProcessRuntimeHistoryWindow.SevenDays, client.LastRequest?.RuntimeQuery?.HistoryWindow));
        cut.WaitForAssertion(() => Assert.Contains(firstRunId.ToString("D"), workspaceService.LastWorkspaceSessionTitle, StringComparison.Ordinal));

        cut.Find("[data-testid='processes-manager-chat-run-select']").Change(secondRunId.ToString("D"));
        cut.WaitForAssertion(() => Assert.Contains(secondRunId.ToString("D"), workspaceService.LastWorkspaceSessionTitle, StringComparison.Ordinal));
        var secondSessionId = workspaceService.LastWorkspaceSessionId;

        Assert.NotEqual(firstSessionId, secondSessionId);

        cut.Find("[data-testid='processes-manager-chat-run-select']").Change(firstRunId.ToString("D"));
        cut.WaitForAssertion(() => Assert.Equal(firstSessionId, workspaceService.LastWorkspaceSessionId));
        Assert.Equal(2, workspaceService.SessionCount);
    }

    [Fact]
    public void Manager_chat_enables_voice_controls_for_voice_allowed_manager_agent()
    {
        var workspaceService = new RecordingManagerChatWorkspaceService(canUseVoiceMode: true);
        using var context = CreateContext(out _, workspaceService);

        var cut = context.Render<ProcessWorkspaceShell>();

        ActivateProcessDetailTab(cut, "processes-detail-tab-manager-chat", "processes-detail-panel-manager-chat");

        cut.WaitForAssertion(() =>
        {
            Assert.False(cut.Find("[data-testid='chat-voice-mode-button']").HasAttribute("disabled"));
            Assert.False(cut.Find("[data-testid='chat-voice-record-button']").HasAttribute("disabled"));
            Assert.False(cut.Find("[data-testid='chat-voice-speak-button']").HasAttribute("disabled"));
        });

        cut.Find("[data-testid='chat-voice-mode-button']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Audio on", cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void Manager_chat_auto_speaks_assistant_response_when_voice_mode_is_enabled()
    {
        var workspaceService = new RecordingManagerChatWorkspaceService(canUseVoiceMode: true)
        {
            AssistantResponseText = "The selected run cost USD 0.12 and used 1,666 tokens."
        };
        var voiceService = new RecordingAgentVoiceService();
        using var context = CreateContext(out _, workspaceService, voiceService);

        var cut = context.Render<ProcessWorkspaceShell>();

        ActivateProcessDetailTab(cut, "processes-detail-tab-manager-chat", "processes-detail-panel-manager-chat");
        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid='chat-voice-mode-button']").HasAttribute("disabled")));
        cut.Find("[data-testid='chat-voice-mode-button']").Click();
        cut.WaitForAssertion(() => Assert.Contains("Audio on", cut.Markup, StringComparison.Ordinal));

        cut.Find("[data-testid='chat-prompt-input']").Input("Tell me about the selected run cost and tokens.");
        cut.Find("[data-testid='chat-send-button']").Click();

        cut.WaitForAssertion(() =>
        {
            var request = Assert.Single(voiceService.SynthesisRequests);
            Assert.Equal(workspaceService.AssistantResponseText, request.Text);
        });
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(
                context.JSInterop.Invocations,
                invocation => invocation.Identifier == "CanDoItAll.agentFramework.voice.clearAudioQueue");
            Assert.Contains(
                context.JSInterop.Invocations,
                invocation => invocation.Identifier == "CanDoItAll.agentFramework.voice.enqueueAudio");
        });
    }

    [Fact]
    public void Manager_chat_sends_only_user_prompt_and_uses_published_runtime_snapshot()
    {
        var workspaceService = new RecordingManagerChatWorkspaceService();
        using var context = CreateContext(
            out var client,
            workspaceService,
            timeProvider: new ManualTimeProvider(Now));
        ConfigureReadyRefresh(client, () => Now);
        var runId = Guid.Parse("77777777-7777-7777-7777-777777777777");

        var cut = context.Render<ProcessWorkspaceShell>(parameters => parameters
            .Add(component => component.RunIdQuery, runId));

        ActivateProcessDetailTab(cut, "processes-detail-tab-manager-chat", "processes-detail-panel-manager-chat");
        cut.WaitForAssertion(() =>
        {
            var runtimeOptions = client.LastRequest?.RuntimeQuery?.LoadOptions;
            Assert.NotNull(runtimeOptions);
            Assert.True(runtimeOptions.IncludeSelectedRun);
            Assert.False(runtimeOptions.IncludeHistory);
            Assert.False(runtimeOptions.IncludeMetricHistory);
            Assert.False(runtimeOptions.IncludeActiveAgents);
            Assert.True(runtimeOptions.IncludeUsageTelemetry);
        });

        const string userPrompt =
            "Tell me please about this last run, how much did it cost and how much tokens did it use?";
        SetPrivateField<IReadOnlyList<string>>(
            cut.Instance,
            "managerChatDraftAttachmentPaths",
            ["artifacts/selected-run-summary.md"]);
        cut.Render();
        cut.Find("[data-testid='chat-prompt-input']").Input(userPrompt);
        cut.Find("[data-testid='chat-send-button']").Click();

        cut.WaitForAssertion(() => Assert.NotNull(workspaceService.LastOptions));
        Assert.Equal(userPrompt, workspaceService.LastPrompt);
        Assert.True(workspaceService.LastOptions!.RuntimeToolProvidersEnabled);
        Assert.True(workspaceService.LastOptions.WorkspaceToolsEnabled);
        var orchestrator = Assert.IsType<RecordingManagerChatExecutionOrchestrator>(
            context.Services.GetRequiredService<IAgentChatExecutionOrchestrator>());
        cut.WaitForAssertion(() =>
        {
            var status = cut.Find("[data-testid='agent-execution-activity-status']");
            Assert.Equal(
                orchestrator.LastStreamId?.OperationId.ToString(),
                status.GetAttribute("data-activity-operation-id"));
            Assert.Equal(
                "Accepted",
                cut.Find("[data-testid='agent-execution-activity-phase']").TextContent.Trim());
        });
        Assert.Equal(userPrompt, orchestrator.LastSendRequest?.Prompt);
        Assert.NotNull(orchestrator.LastSendRequest?.AttachmentPaths);
        Assert.Equal(
            ["artifacts/selected-run-summary.md"],
            orchestrator.LastSendRequest!.AttachmentPaths!.ToArray());
        var contextSnapshot = Assert.IsType<AgentChatContextSnapshot>(
            orchestrator.LastCapturedContext);
        var attachment = Assert.Single(contextSnapshot.Attachments);
        Assert.True(attachment.TryGetAttachment<ProcessInvocationSnapshot>(out var processSnapshot));
        Assert.Equal(runId, processSnapshot!.SelectedRunId);
        Assert.True(processSnapshot.Usage.HasValue);
        Assert.Equal(1_666, processSnapshot.Usage.Value.TotalTokens);
        Assert.Equal(0.123456m, processSnapshot.Usage.Value.ActualCost);
        var currentAttachment = Assert.Single(
            Assert.IsType<AgentChatContextSnapshot>(
                context.Services.GetRequiredService<IAgentChatContextRegistry>().Capture())
                .Attachments);
        Assert.Equal(attachment.ContentFingerprint, currentAttachment.ContentFingerprint);
    }

    [Fact]
    public void Manager_chat_preserves_classifier_behavior_through_typed_orchestrator_request()
    {
        var workspaceService = new RecordingManagerChatWorkspaceService();
        using var context = CreateContext(out _, workspaceService);
        var cut = context.Render<ProcessWorkspaceShell>();

        ActivateProcessDetailTab(cut, "processes-detail-tab-manager-chat", "processes-detail-panel-manager-chat");
        const string userPrompt =
            "Report the selected run total tokens, input tokens, cached input tokens, output tokens, actual cost, completed status, and current operator actions. Use only the runtime telemetry already in this manager chat context.";
        cut.Find("[data-testid='chat-prompt-input']").Input(userPrompt);
        cut.Find("[data-testid='chat-send-button']").Click();

        var orchestrator = Assert.IsType<RecordingManagerChatExecutionOrchestrator>(
            context.Services.GetRequiredService<IAgentChatExecutionOrchestrator>());
        cut.WaitForAssertion(() => Assert.NotNull(orchestrator.LastSendRequest));
        Assert.Equal(userPrompt, orchestrator.LastSendRequest!.Prompt);
        Assert.False(orchestrator.LastSendRequest.Behavior.RuntimeToolProvidersEnabled);
        Assert.False(orchestrator.LastSendRequest.Behavior.WorkspaceToolsEnabled);
        Assert.True(orchestrator.LastSendRequest.Behavior.ToolCapabilitiesEnabled);
    }

    [Fact]
    public async Task Manager_chat_routes_approval_continuation_through_orchestrator()
    {
        var workspaceService = new RecordingManagerChatWorkspaceService();
        using var context = CreateContext(out _, workspaceService);
        var cut = context.Render<ProcessWorkspaceShell>();

        ActivateProcessDetailTab(cut, "processes-detail-tab-manager-chat", "processes-detail-panel-manager-chat");
        cut.WaitForAssertion(() => Assert.NotNull(workspaceService.LastWorkspaceSessionId));
        var method = typeof(ProcessWorkspaceShell).GetMethod(
            "ContinueManagerChatApprovalAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        await cut.InvokeAsync(async () =>
        {
            var continuation = Assert.IsAssignableFrom<Task>(
                method.Invoke(cut.Instance, [true, true]));
            await continuation;
        });

        var orchestrator = Assert.IsType<RecordingManagerChatExecutionOrchestrator>(
            context.Services.GetRequiredService<IAgentChatExecutionOrchestrator>());
        Assert.True(orchestrator.LastApprovalDecision);
        Assert.True(orchestrator.LastAutoApprovePendingToolCalls);
    }

    [Fact]
    public void Manager_chat_keeps_voice_controls_disabled_for_voice_denied_manager_agent()
    {
        var workspaceService = new RecordingManagerChatWorkspaceService(canUseVoiceMode: false);
        using var context = CreateContext(out _, workspaceService);

        var cut = context.Render<ProcessWorkspaceShell>();

        ActivateProcessDetailTab(cut, "processes-detail-tab-manager-chat", "processes-detail-panel-manager-chat");

        cut.WaitForAssertion(() =>
        {
            Assert.True(cut.Find("[data-testid='chat-voice-mode-button']").HasAttribute("disabled"));
            Assert.True(cut.Find("[data-testid='chat-voice-record-button']").HasAttribute("disabled"));
            Assert.True(cut.Find("[data-testid='chat-voice-speak-button']").HasAttribute("disabled"));
        });
    }

    [Fact]
    public void Processes_navigation_contributor_adds_processes_to_shell_navigation()
    {
        var items = ShellNavigation.GetItems(0, [new ProcessesShellNavigationContributor()]);
        var processes = Assert.Single(items, item => item.Route == "/processes");
        var liveProcesses = Assert.Single(items, item => item.Route == "/processes/live");
        var contribution = new ProcessesShellNavigationContributor()
            .GetShellNavigationContributions()
            .Single(item => item.Item.Route == "/processes/live");

        Assert.Equal("Processes", processes.Title);
        Assert.Equal("account_tree", processes.Icon);
        Assert.Equal("Live Processes", liveProcesses.Title);
        Assert.Equal("monitor_heart", liveProcesses.Icon);
        Assert.True(contribution.IsSubItem);
        Assert.Equal("/processes", contribution.ParentRoute);
    }

    [Fact]
    public void Live_processes_dashboard_uses_own_projection_page()
    {
        using var context = CreateContext(out var client);

        var cut = context.Render<LiveProcessesDashboard>(parameters => parameters
            .Add(component => component.LaunchStarted, true));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='live-processes-tabs']")));
        Assert.Equal(ProcessWorkspaceScopeKind.Global, client.LastRequest?.Scope.Kind);
        Assert.False(client.LastRequest?.RuntimeQuery?.AutoSelectRun);
        Assert.False(client.LastRequest?.DefinitionLoadOptions?.IncludeSelectedEditor);
        Assert.False(client.LastRequest?.DefinitionLoadOptions?.IncludeRoleEditor);
        Assert.False(client.LastRequest?.DefinitionLoadOptions?.IncludeStepEditor);
        Assert.False(client.LastRequest?.DefinitionLoadOptions?.IncludeCanvas);
        Assert.False(client.LastRequest?.DefinitionLoadOptions?.IncludeTemplateCatalog);
        var runtimeOptions = client.LastRequest?.RuntimeQuery?.LoadOptions;
        Assert.NotNull(runtimeOptions);
        Assert.False(runtimeOptions.IncludeSelectedRun);
        Assert.True(runtimeOptions.IncludeHistory);
        Assert.False(runtimeOptions.IncludeMetricHistory);
        Assert.True(runtimeOptions.IncludeActiveAgents);
        Assert.False(runtimeOptions.IncludeUsageTelemetry);

        var requestCountBeforeGraphs = client.Requests.Count;
        cut.FindAll("button[role='tab']")
            .Single(tab => tab.TextContent.Contains("Graphs", StringComparison.Ordinal))
            .Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(requestCountBeforeGraphs + 1, client.Requests.Count);
            var graphOptions = client.LastRequest?.RuntimeQuery?.LoadOptions;
            Assert.NotNull(graphOptions);
            Assert.False(graphOptions.IncludeSelectedRun);
            Assert.False(graphOptions.IncludeHistory);
            Assert.True(graphOptions.IncludeMetricHistory);
            Assert.False(graphOptions.IncludeActiveAgents);
            Assert.True(graphOptions.IncludeUsageTelemetry);
            Assert.NotNull(client.LastRequest?.RuntimeQuery?.PreviouslyLoadedRuns);
        });

        Assert.NotNull(cut.Find("[data-testid='live-processes-page']"));
        Assert.NotNull(cut.Find("[data-testid='live-processes-command-strip']"));
        Assert.NotNull(cut.Find("[data-testid='live-processes-dashboard']"));
        Assert.NotNull(cut.Find("[data-testid='live-processes-started-notification']"));
        Assert.NotNull(cut.Find("[data-testid='live-processes-activity-cards']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='live-processes-run-progress']"));
        Assert.NotNull(cut.Find("[data-testid='live-processes-tool-history-chart']"));
        Assert.Contains("Tool history", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("USD 0.00", cut.Markup, StringComparison.Ordinal);
        Assert.NotNull(cut.Find("[data-testid='live-processes-request-rework']"));
        Assert.NotNull(cut.Find("[data-testid='live-processes-attention-decision']"));
        Assert.Contains("Approve rework", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Approve rework to return implement-code-change", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(".NET Developer", cut.Markup, StringComparison.Ordinal);

        cut.Find("[data-testid='live-processes-attention-open-details']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='live-processes-run-detail-hero']")));
        Assert.NotNull(cut.Find("[data-testid='live-processes-run-detail-manager-summary']"));
        Assert.NotNull(cut.Find("[data-testid='live-processes-run-detail-decision']"));
        Assert.NotNull(cut.Find("[data-testid='live-processes-dialog-operator-note']"));
        Assert.NotNull(cut.Find("[data-testid='live-processes-run-files']"));
        Assert.Contains("Manager summary", cut.Markup, StringComparison.Ordinal);

        cut.Find("[data-testid='live-processes-dialog-operator-note']").Change("Reuse the approved architecture and keep the Tetris output folder.");
        cut.Find("[data-testid='live-processes-dialog-request-rework']").Click();

        cut.WaitForAssertion(() => Assert.NotNull(client.LastOperatorActionCommand));
        Assert.Contains("Manager-approved rework for step 'implement-code-change'", client.LastOperatorActionCommand!.Reason, StringComparison.Ordinal);
        Assert.Contains("Operator note:", client.LastOperatorActionCommand.Reason, StringComparison.Ordinal);
        Assert.Contains("Tetris output folder", client.LastOperatorActionCommand.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Live_processes_dashboard_mock_scenario_supplies_dense_operations_projection()
    {
        using var context = CreateContext(out _);

        var cut = context.Render<LiveProcessesDashboard>(parameters => parameters
            .Add(component => component.MockScenarioQuery, "operations"));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='live-processes-mock-notification']")));
        cut.WaitForAssertion(() => Assert.True(cut.FindAll("[data-testid='live-processes-activity-card']").Count >= 5));
        Assert.NotNull(cut.Find("[data-testid='live-processes-tool-history-chart']"));
        Assert.True(cut.FindAll("[data-testid='live-processes-tool-family-card']").Count >= 4);
        Assert.Contains("Mock scenario: multi-team delivery", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("External verification", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Agent Alpha", cut.Markup, StringComparison.Ordinal);

        cut.Find("[data-testid='live-processes-attention-open-details']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='live-processes-run-detail-decision']")));
        cut.Find("[data-testid='live-processes-dialog-request-rework']").Click();

        cut.WaitForAssertion(() => Assert.DoesNotContain("Vendor onboarding is blocked", cut.Markup, StringComparison.Ordinal));
        Assert.Contains("Request rework accepted", context.Services.GetRequiredService<NotificationService>().Messages.Last().Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Live_processes_active_card_prefers_working_agent_summary_over_stale_attention_event()
    {
        using var context = CreateContext(out _);
        var tooltipHost = context.Render<Tooltip>();
        var activeRunId = Guid.Parse("88888888-8888-8888-8888-888888888888");

        var cut = context.Render<LiveProcessesDashboard>(parameters => parameters
            .Add(component => component.RunIdQuery, activeRunId));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='live-processes-activity-cards']")));
        var activeCard = cut
            .FindAll("[data-testid='live-processes-activity-card']")
            .Single(card => card.TextContent.Contains("Run 88888888", StringComparison.Ordinal));
        var processName = activeCard.QuerySelector("[data-testid='live-processes-run-process-name']");

        Assert.NotNull(processName);
        Assert.Contains("Long-running customer onboardin...", processName!.TextContent, StringComparison.Ordinal);
        Assert.Equal(ResolveProcessName(activeRunId), processName.GetAttribute("title"));
        Assert.Contains("Active: .NET Developer is Running on implementation as lead-engineer.", activeCard.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("ManagerIncidentRaised: ManagerIncidentRaised", activeCard.TextContent, StringComparison.Ordinal);

        processName.TriggerEvent("onmouseenter", new MouseEventArgs { ClientX = 120, ClientY = 80 });
        tooltipHost.WaitForAssertion(() =>
        {
            Assert.Contains(ResolveProcessName(activeRunId), tooltipHost.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Live_processes_activity_card_shows_project_subprocess_and_manager_context()
    {
        using var context = CreateContext(out _);

        var cut = context.Render<LiveProcessesDashboard>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='live-processes-activity-cards']")));
        var subprocessCard = cut
            .FindAll("[data-testid='live-processes-activity-card']")
            .Single(card => card.TextContent.Contains("Run 88888888", StringComparison.Ordinal));

        Assert.Equal(
            "Apollo Delivery",
            subprocessCard.QuerySelector("[data-testid='live-processes-run-project-name']")?.TextContent);
        Assert.NotNull(subprocessCard.QuerySelector("[data-testid='live-processes-run-subprocess-badge']"));
        Assert.Equal(
            "Process manager",
            subprocessCard.QuerySelector("[data-testid='live-processes-run-agent-name']")?.TextContent);
        Assert.DoesNotContain("Unassigned", subprocessCard.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Live_processes_attention_card_shows_event_date_and_hide_action()
    {
        using var context = CreateContext(out _);

        var cut = context.Render<LiveProcessesDashboard>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='live-processes-attention-card']")));
        var attentionCard = cut.Find("[data-testid='live-processes-attention-card']");

        Assert.NotNull(attentionCard.QuerySelector("[data-testid='live-processes-attention-run-date']"));
        Assert.NotNull(attentionCard.QuerySelector("[data-testid='live-processes-hide-run-group']"));
    }

    [Fact]
    public void Live_processes_dashboard_hides_and_restores_related_run_cards()
    {
        using var context = CreateContext(out _);

        var cut = context.Render<LiveProcessesDashboard>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='live-processes-activity-cards']")));
        var subprocessCard = cut
            .FindAll("[data-testid='live-processes-activity-card']")
            .Single(card => card.TextContent.Contains("Run 88888888", StringComparison.Ordinal));

        subprocessCard.QuerySelector("[data-testid='live-processes-hide-run-group']")!.Click();

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain(
                cut.FindAll("[data-testid='live-processes-activity-card']"),
                card => card.TextContent.Contains("Run 88888888", StringComparison.Ordinal));
            Assert.DoesNotContain(
                cut.FindAll("[data-testid='live-processes-run-card']"),
                card => card.TextContent.Contains("Run 88888888", StringComparison.Ordinal));
        });

        cut.Find("[data-testid='live-processes-show-hidden-runs']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(
                cut.FindAll("[data-testid='live-processes-activity-card']"),
                card => card.TextContent.Contains("Run 88888888", StringComparison.Ordinal));
            Assert.Contains(
                cut.FindAll("[data-testid='live-processes-run-card']"),
                card => card.TextContent.Contains("Run 88888888", StringComparison.Ordinal));
        });
    }

    private static BunitContext CreateContext(
        out RecordingProcessWorkspaceProjectionClient client,
        IAgentFrameworkWorkspaceService? agentWorkspaceService = null,
        IAgentVoiceService? voiceService = null,
        TimeProvider? timeProvider = null)
    {
        var effectiveTimeProvider = timeProvider ?? TimeProvider.System;
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<ICurrencyFormatter>(new StaticCurrencyFormatter("USD"));
        context.Services.AddSingleton<IProcessProjectionClock>(new FixedProcessProjectionClock(Now));
        context.Services.AddSingleton(effectiveTimeProvider);
        context.Services.AddSingleton<IDatabaseSwitchNotificationService, DatabaseSwitchNotificationService>();
        context.Services.AddSingleton<IDatabaseRuntimeState, DatabaseRuntimeState>();
        context.Services.AddSingleton<ProcessWorkspaceMockProjectionFactory>();
        var contextRegistry = new AgentChatContextRegistry(effectiveTimeProvider);
        context.Services.AddSingleton<IAgentChatContextRegistry>(contextRegistry);
        context.Services.AddSingleton<IAgentChatExecutionOrchestrator>(
            new RecordingManagerChatExecutionOrchestrator(
                agentWorkspaceService,
                contextRegistry));
        context.Services.AddSingleton<IAgentExecutionActivityReader>(
            new AcceptedActivityReader());
        if (agentWorkspaceService is not null)
        {
            context.Services.AddSingleton(agentWorkspaceService);
            context.Services.AddSingleton<AgentReferenceDataCache>();
            context.Services.AddSingleton<IAgentReferenceDataCacheInvalidator>(serviceProvider =>
                serviceProvider.GetRequiredService<AgentReferenceDataCache>());
            context.Services.AddSingleton<IAgentReferenceDataProvider>(serviceProvider =>
                new WorkspaceBackedAgentReferenceDataProvider(
                    agentWorkspaceService,
                    serviceProvider.GetRequiredService<AgentReferenceDataCache>()));
        }
        if (voiceService is not null)
        {
            context.Services.AddSingleton(voiceService);
        }

        client = new RecordingProcessWorkspaceProjectionClient();
        context.Services.AddSingleton<IProcessWorkspaceProjectionClient>(client);
        return context;
    }

    private sealed class ManualTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private readonly object gate = new();
        private readonly List<ManualTimer> timers = [];
        private DateTimeOffset utcNow = initialUtcNow.ToUniversalTime();

        public override DateTimeOffset GetUtcNow()
        {
            lock (gate)
            {
                return utcNow;
            }
        }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            ArgumentNullException.ThrowIfNull(callback);
            ValidateTimerInterval(dueTime, nameof(dueTime));
            ValidateTimerInterval(period, nameof(period));

            lock (gate)
            {
                var timer = new ManualTimer(this, callback, state);
                timer.SetSchedule(utcNow, dueTime, period);
                timers.Add(timer);
                return timer;
            }
        }

        public void Advance(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(duration),
                    duration,
                    "Manual time cannot move backwards.");
            }

            lock (gate)
            {
                utcNow = utcNow.Add(duration);
            }

            while (TryTakeDueInvocation(out var invocation))
            {
                invocation.Callback(invocation.State);
            }
        }

        private bool TryTakeDueInvocation(out TimerInvocation invocation)
        {
            lock (gate)
            {
                var dueTimer = timers
                    .Where(candidate => candidate.IsDue(utcNow))
                    .OrderBy(candidate => candidate.NextDueUtc)
                    .FirstOrDefault();
                if (dueTimer is null)
                {
                    invocation = default;
                    return false;
                }

                invocation = dueTimer.TakeInvocation();
                return true;
            }
        }

        private bool Change(
            ManualTimer timer,
            TimeSpan dueTime,
            TimeSpan period)
        {
            ValidateTimerInterval(dueTime, nameof(dueTime));
            ValidateTimerInterval(period, nameof(period));

            lock (gate)
            {
                if (timer.IsDisposed)
                {
                    return false;
                }

                timer.SetSchedule(utcNow, dueTime, period);
                return true;
            }
        }

        private void Dispose(ManualTimer timer)
        {
            lock (gate)
            {
                if (timer.IsDisposed)
                {
                    return;
                }

                timer.MarkDisposed();
                timers.Remove(timer);
            }
        }

        private static void ValidateTimerInterval(
            TimeSpan interval,
            string parameterName)
        {
            if (interval < TimeSpan.Zero && interval != Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    interval,
                    "A timer interval must be non-negative or infinite.");
            }
        }

        private sealed class ManualTimer(
            ManualTimeProvider owner,
            TimerCallback callback,
            object? state) : ITimer
        {
            private TimeSpan period = Timeout.InfiniteTimeSpan;

            public DateTimeOffset? NextDueUtc { get; private set; }

            public bool IsDisposed { get; private set; }

            public bool Change(TimeSpan dueTime, TimeSpan period)
                => owner.Change(this, dueTime, period);

            public void Dispose()
                => owner.Dispose(this);

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }

            public bool IsDue(DateTimeOffset currentUtc)
                => !IsDisposed &&
                   NextDueUtc is { } nextDueUtc &&
                   nextDueUtc <= currentUtc;

            public void SetSchedule(
                DateTimeOffset currentUtc,
                TimeSpan dueTime,
                TimeSpan nextPeriod)
            {
                period = nextPeriod;
                NextDueUtc = dueTime == Timeout.InfiniteTimeSpan
                    ? null
                    : currentUtc.Add(dueTime);
            }

            public TimerInvocation TakeInvocation()
            {
                if (NextDueUtc is null)
                {
                    throw new InvalidOperationException("The timer is not due.");
                }

                NextDueUtc = period > TimeSpan.Zero
                    ? NextDueUtc.Value.Add(period)
                    : null;
                return new TimerInvocation(callback, state);
            }

            public void MarkDisposed()
            {
                IsDisposed = true;
                NextDueUtc = null;
            }
        }

        private readonly record struct TimerInvocation(
            TimerCallback Callback,
            object? State);
    }

    private static string ResolveProcessName(Guid runId)
        => runId == ProjectSubprocessRunId
            ? "Long-running customer onboarding process with multiple external approvals"
            : "Blazor app delivery";

    private sealed class StaticCurrencyFormatter(string currencyCode) : ICurrencyFormatter
    {
        public string CurrencyCode { get; } = currencyCode;

        public string Format(decimal value)
        {
            return $"{CurrencyCode} {value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}";
        }
    }

    private sealed class RecordingManagerChatExecutionOrchestrator(
        IAgentFrameworkWorkspaceService? workspaceService,
        IAgentChatContextRegistry contextRegistry) : IAgentChatExecutionOrchestrator
    {
        public AgentChatSendRequest? LastSendRequest { get; private set; }

        public AgentChatContextSnapshot? LastCapturedContext { get; private set; }

        public bool? LastApprovalDecision { get; private set; }

        public bool? LastAutoApprovePendingToolCalls { get; private set; }

        public AgentExecutionActivityStreamId? LastStreamId { get; private set; }

        public AgentChatOperationHandle StartSendMessage(
            AgentChatSendRequest request,
            CancellationToken cancellationToken = default)
        {
            var completion = SendMessageAsync(request, cancellationToken);
            return CreateHandle(completion);
        }

        public AgentChatOperationHandle StartSendMessage(
            Guid agentId,
            Guid? chatSessionId,
            string prompt,
            IReadOnlyList<string>? attachmentPaths = null,
            CancellationToken cancellationToken = default)
            => StartSendMessage(
                new AgentChatSendRequest(agentId, chatSessionId, prompt)
                {
                    AttachmentPaths = attachmentPaths
                },
                cancellationToken);

        public AgentChatOperationHandle StartApprovalContinuation(
            Guid agentId,
            Guid chatSessionId,
            bool approved,
            bool autoApprovePendingToolCalls = false,
            CancellationToken cancellationToken = default)
        {
            var completion = RespondToPendingApprovalsAsync(
                agentId,
                chatSessionId,
                approved,
                autoApprovePendingToolCalls,
                cancellationToken);
            return CreateHandle(completion);
        }

        public Task<AgentChatRunResult> SendMessageAsync(
            AgentChatSendRequest request,
            CancellationToken cancellationToken = default)
        {
            LastSendRequest = request;
            LastCapturedContext = contextRegistry.Capture();
            var service = workspaceService
                ?? throw new InvalidOperationException(
                    "The manager chat execution orchestrator was called without a workspace service.");
            return service.SendMessageAsync(
                request.AgentId,
                request.ChatSessionId,
                request.Prompt,
                new AgentChatRunOptions(
                    AgentExecutionOperationId.New(),
                    request.Behavior.RuntimeToolProvidersEnabled,
                    request.Behavior.WorkspaceToolsEnabled)
                {
                    ToolCapabilitiesEnabled = request.Behavior.ToolCapabilitiesEnabled
                },
                cancellationToken,
                request.AttachmentPaths);
        }

        public Task<AgentChatRunResult> SendMessageAsync(
            Guid agentId,
            Guid? chatSessionId,
            string prompt,
            IReadOnlyList<string>? attachmentPaths = null,
            CancellationToken cancellationToken = default)
            => SendMessageAsync(
                new AgentChatSendRequest(agentId, chatSessionId, prompt)
                {
                    AttachmentPaths = attachmentPaths
                },
                cancellationToken);

        public Task<AgentChatRunResult> RespondToPendingApprovalsAsync(
            Guid agentId,
            Guid chatSessionId,
            bool approved,
            bool autoApprovePendingToolCalls = false,
            CancellationToken cancellationToken = default)
        {
            LastApprovalDecision = approved;
            LastAutoApprovePendingToolCalls = autoApprovePendingToolCalls;
            var assistantMessage = new ChatMessageRecord(
                Guid.NewGuid(),
                ChatMessageRole.Assistant,
                "Approval continuation completed.",
                Now,
                TokenEstimate: 0);
            var metric = new AgentRunMetric(
                Guid.NewGuid(),
                agentId,
                chatSessionId,
                Now,
                RunOutcome.Succeeded,
                ProviderName: "TestProvider",
                Model: "test-model",
                DurationMs: 1,
                InputTokens: 0,
                OutputTokens: 0,
                ToolCalls: 0);
            return Task.FromResult(
                new AgentChatRunResult(chatSessionId, assistantMessage, metric));
        }

        private AgentChatOperationHandle CreateHandle(
            Task<AgentChatRunResult> completion)
        {
            var identity = AgentExecutionActivityWorkspaceIdentity.CreateHostLifetime(
                WorkspaceScopeDescriptor.Organization("test"));
            LastStreamId = identity.CreateStreamId(AgentExecutionOperationId.New());
            return new AgentChatOperationHandle(
                LastStreamId,
                completion);
        }
    }

    private sealed class AcceptedActivityReader : IAgentExecutionActivityReader
    {
        public ISequencedStreamReader<AgentExecutionActivity> OpenReader(
            AgentExecutionActivityStreamId streamId,
            StreamSequence fromInclusive)
        {
            Assert.Equal(StreamSequence.Beginning, fromInclusive);
            return new AcceptedSequencedStreamReader();
        }
    }

    private sealed class AcceptedSequencedStreamReader :
        ISequencedStreamReader<AgentExecutionActivity>
    {
        private bool emitted;

        public StreamSequence NextSequence => emitted
            ? new StreamSequence(2)
            : StreamSequence.First;

        public ValueTask<SequencedStreamReadResult<AgentExecutionActivity>> ReadAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (emitted)
            {
                return ValueTask.FromResult<
                    SequencedStreamReadResult<AgentExecutionActivity>>(
                    new SequencedStreamCompleted<AgentExecutionActivity>(
                        StreamSequence.First));
            }

            emitted = true;
            var activity = new AgentExecutionActivity(
                AgentExecutionActivityPhase.Accepted,
                Now,
                agentId: null,
                "Agent request accepted.");
            return ValueTask.FromResult<
                SequencedStreamReadResult<AgentExecutionActivity>>(
                new SequencedStreamEvents<AgentExecutionActivity>(
                [
                    new SequencedStreamEnvelope<AgentExecutionActivity>(
                        StreamSequence.First,
                        activity)
                ]));
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingManagerChatWorkspaceService : IAgentFrameworkWorkspaceService
    {
        private readonly AgentDefinition agent;
        private readonly List<ChatSessionRecord> sessions = [];

        public RecordingManagerChatWorkspaceService(bool canUseVoiceMode = false)
        {
            agent = CreateAgent(canUseVoiceMode);
        }

        public string AssistantResponseText { get; init; } = "Manager response from the fake runtime.";

        public string LastPrompt { get; private set; } = string.Empty;

        public AgentChatRunOptions? LastOptions { get; private set; }

        public event EventHandler<ExecutionLogEntry>? ExecutionUpdated
        {
            add { }
            remove { }
        }

        public Guid? LastWorkspaceSessionId { get; private set; }

        public string LastWorkspaceSessionTitle { get; private set; } = string.Empty;

        public int SessionCount => sessions.Count;

        public Task<IReadOnlyList<AgentDefinition>> ListAgentsAsync(
            bool includeTemplates = true,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AgentDefinition>>([agent]);

        public Task<IReadOnlyList<ChatSessionRecord>> ListChatSessionsAsync(
            Guid agentId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ChatSessionRecord>>(
                sessions
                    .Where(session => session.AgentId == agentId)
                    .OrderByDescending(session => session.UpdatedAtUtc)
                    .ToArray());

        public Task<ChatSessionRecord> GetOrCreateChatSessionAsync(
            Guid agentId,
            Guid? chatSessionId = null,
            CancellationToken cancellationToken = default)
        {
            if (chatSessionId is { } sessionId)
            {
                return Task.FromResult(sessions.Single(session => session.Id == sessionId));
            }

            var session = new ChatSessionRecord(
                Guid.NewGuid(),
                agentId,
                "New exploration thread",
                Now,
                Now,
                Messages: []);
            sessions.Add(session);
            return Task.FromResult(session);
        }

        public Task<ChatSessionRecord> RenameChatSessionAsync(
            Guid agentId,
            Guid chatSessionId,
            string title,
            CancellationToken cancellationToken = default)
        {
            var sessionIndex = sessions.FindIndex(session => session.AgentId == agentId && session.Id == chatSessionId);
            if (sessionIndex < 0)
            {
                throw new InvalidOperationException($"Chat session '{chatSessionId:D}' was not created.");
            }

            sessions[sessionIndex] = sessions[sessionIndex] with
            {
                Title = title,
                UpdatedAtUtc = Now.AddSeconds(sessions.Count)
            };
            return Task.FromResult(sessions[sessionIndex]);
        }

        public Task<ChatAgentWorkspaceSnapshot> GetChatAgentWorkspaceAsync(
            Guid agentId,
            Guid? preferredSessionId = null,
            CancellationToken cancellationToken = default)
        {
            var selectedSession = preferredSessionId is { } sessionId
                ? sessions.FirstOrDefault(session => session.AgentId == agentId && session.Id == sessionId)
                : sessions
                    .Where(session => session.AgentId == agentId)
                    .OrderByDescending(session => session.UpdatedAtUtc)
                    .FirstOrDefault();
            LastWorkspaceSessionId = selectedSession?.Id;
            LastWorkspaceSessionTitle = selectedSession?.Title ?? string.Empty;
            return Task.FromResult(new ChatAgentWorkspaceSnapshot(
                agentId,
                sessions
                    .Where(session => session.AgentId == agentId)
                    .Select(ToSummary)
                    .ToArray(),
                selectedSession,
                selectedSession?.Id,
                LatestRun: null));
        }

        public Task<ChatRuntimeSnapshot> GetChatRuntimeSnapshotAsync(
            Guid agentId,
            Guid? chatSessionId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatRuntimeSnapshot([], []));

        public Task<SandboxDashboardSnapshot> GetDashboardAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentOverviewSnapshot> GetAgentOverviewAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentUsageDetailSnapshot> GetAgentUsageDetailsAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderUsageDetailSnapshot> GetProviderUsageDetailsAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<ModelUsageDetailSnapshot> GetModelUsageDetailsAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentEditorModel> GetAgentEditorAsync(Guid? agentId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveAgentAsync(AgentEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteAgentAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<AgentTeamDefinition>> ListAgentTeamsAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentTeamEditorModel> GetAgentTeamEditorAsync(Guid? teamId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveAgentTeamAsync(AgentTeamEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentTeamDefinition> UpdateAgentTeamMembersAsync(
            Guid teamId,
            IReadOnlyList<Guid> agentIds,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteAgentTeamAsync(Guid teamId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> CloneAgentAsync(Guid agentId, string cloneName, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> ConvertToTemplateAsync(Guid agentId, string templateKey, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentExportResult> ExportAgentAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> ImportAgentAsync(string packagePath, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderProfileEditorModel> GetProviderEditorAsync(Guid? providerId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveProviderAsync(ProviderProfileEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteProviderAsync(Guid providerId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderHealthResult> TestProviderAsync(Guid providerId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(
            Guid providerId,
            ProviderTestChatRequest request,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
            Guid providerId,
            ProviderModelMaintenanceEditorRequest request,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<CapabilityCatalogItem>> ListCapabilitiesAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<CapabilityEditorModel> GetCapabilityEditorAsync(Guid? capabilityId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveCapabilityAsync(CapabilityEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteCapabilityAsync(Guid capabilityId, CancellationToken cancellationToken = default) => throw Unused();

        public Task VerifyCapabilityAsync(Guid agentId, Guid capabilityId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatPageBootstrapSnapshot> GetChatPageBootstrapAsync(
            bool includeTemplates = false,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<ExecutionRunResult> ExecuteRunAsync(
            ExecutionRunRequest request,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<ExecutionRunResult> ContinueExecutionRunAsync(
            Guid executionRunId,
            AgentExecutionOperationId activityOperationId,
            bool approved,
            bool autoApprovePendingToolCalls = false,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentChatRunResult> SendMessageAsync(
            Guid agentId,
            Guid? chatSessionId,
            string prompt,
            AgentChatRunOptions options,
            CancellationToken cancellationToken = default,
            IReadOnlyList<string>? attachmentPaths = null)
        {
            LastPrompt = prompt;
            LastOptions = options;
            var session = chatSessionId is { } sessionId
                ? sessions.Single(item => item.AgentId == agentId && item.Id == sessionId)
                : CreateSession(agentId);
            var userMessage = new ChatMessageRecord(
                Guid.NewGuid(),
                ChatMessageRole.User,
                prompt,
                Now,
                TokenEstimate: 0);
            var assistantMessage = new ChatMessageRecord(
                Guid.NewGuid(),
                ChatMessageRole.Assistant,
                AssistantResponseText,
                Now.AddSeconds(1),
                TokenEstimate: 0);
            var updatedSession = session with
            {
                Messages = session.Messages.Concat([userMessage, assistantMessage]).ToArray(),
                UpdatedAtUtc = Now.AddSeconds(1)
            };
            var sessionIndex = sessions.FindIndex(item => item.AgentId == agentId && item.Id == session.Id);
            sessions[sessionIndex] = updatedSession;
            var metric = new AgentRunMetric(
                Guid.NewGuid(),
                agentId,
                updatedSession.Id,
                Now.AddSeconds(1),
                RunOutcome.Succeeded,
                ProviderName: "TestProvider",
                Model: "test-model",
                DurationMs: 100,
                InputTokens: 12,
                OutputTokens: 34,
                ToolCalls: 0);
            return Task.FromResult(new AgentChatRunResult(updatedSession.Id, assistantMessage, metric));
        }

        public Task<AgentChatRunResult> RespondToPendingApprovalsAsync(
            Guid agentId,
            Guid chatSessionId,
            AgentExecutionOperationId activityOperationId,
            bool approved,
            bool autoApprovePendingToolCalls = false,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionLogEntry>> ListExecutionLogAsync(
            Guid agentId,
            Guid? chatSessionId = null,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<AgentRunMetric>> ListMetricsAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<AgentMemoryRecord>> ListMemoryAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveMemoryAsync(MemoryEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteMemoryAsync(Guid memoryId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(
            ExecutionRunQuery query,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentExecutionReportPage> QueryExecutionReportAsync(
            AgentExecutionReportQuery query,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<ExecutionRunDetail> GetExecutionRunDetailAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionArtifactRecord>> ListExecutionArtifactsAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionWorkflowCheckpointRecord>> ListExecutionWorkflowCheckpointsAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ToolExecutionReceiptRecord>> ListToolExecutionReceiptsAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();

        private static AgentDefinition CreateAgent(bool canUseVoiceMode)
            => new(
                Guid.Parse("99999999-9999-9999-9999-999999999999"),
                "Process Manager",
                "Process manager",
                "Answers process manager questions.",
                "Use the supplied process context.",
                AgentLifecycleStatus.Active,
                null,
                "test-model",
                AgentWorkloadKind.Management,
                AgentChatHistoryMode.FrameworkManaged,
                0,
                false,
                false,
                AgentVoiceAccessMetadata.Write(
                    "{}",
                    new AgentVoiceAccessSettings
                    {
                        CanUseVoiceMode = canUseVoiceMode,
                        PreferredVoiceId = canUseVoiceMode ? "cedar" : string.Empty
                    }),
                false,
                string.Empty,
                AgentPermissionsPolicy.Default,
                [],
                [],
                Now,
                Now);

        private ChatSessionRecord CreateSession(Guid agentId)
        {
            var session = new ChatSessionRecord(
                Guid.NewGuid(),
                agentId,
                "New exploration thread",
                Now,
                Now,
                Messages: []);
            sessions.Add(session);
            return session;
        }

        private static ChatSessionSummaryRecord ToSummary(ChatSessionRecord session)
            => new(
                session.Id,
                session.AgentId,
                session.Title,
                session.CreatedAtUtc,
                session.UpdatedAtUtc,
                session.Messages.Count,
                LastMessagePreview: string.Empty,
                PendingApprovalCount: 0,
                AutoApprovePendingToolCalls: false);

        private static NotSupportedException Unused()
            => new("This fake member is not used by the manager chat component test.");
    }

    private sealed class RecordingAgentVoiceService : IAgentVoiceService
    {
        public List<AgentVoiceSynthesisRequest> SynthesisRequests { get; } = [];

        public Task<AgentVoiceSettings> GetSettingsAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentVoiceSettings> SaveSettingsAsync(
            AgentVoiceSettings settings,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentVoiceTranscriptionResult> TranscribeAsync(
            AgentVoiceTranscriptionRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new AgentVoiceTranscriptionResult("Tell me about the selected run cost and tokens.", "test-stt"));

        public Task<AgentVoiceSynthesisResult> SynthesizeAsync(
            AgentVoiceSynthesisRequest request,
            CancellationToken cancellationToken = default)
        {
            SynthesisRequests.Add(request);
            return Task.FromResult(CreateSynthesisResult(request));
        }

        public IAsyncEnumerable<AgentVoiceSynthesisResult> SynthesizeChunksAsync(
            AgentVoiceSynthesisRequest request,
            CancellationToken cancellationToken = default)
        {
            SynthesisRequests.Add(request);
            return EnumerateSynthesisResult(CreateSynthesisResult(request));
        }

        public Task<AgentVoiceSynthesisResult> SynthesizeSampleAsync(
            string? sampleText = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(CreateSynthesisResult(new AgentVoiceSynthesisRequest(sampleText ?? "sample")));

        private static async IAsyncEnumerable<AgentVoiceSynthesisResult> EnumerateSynthesisResult(
            AgentVoiceSynthesisResult result)
        {
            await Task.Yield();
            yield return result;
        }

        private static AgentVoiceSynthesisResult CreateSynthesisResult(AgentVoiceSynthesisRequest request)
            => new(
                [1, 2, 3],
                ContentType: "audio/mpeg",
                Model: "test-tts",
                VoiceId: request.AgentVoiceAccess?.PreferredVoiceId ?? "cedar",
                ResponseFormat: "mp3")
            {
                SpokenText = request.Text
            };

        private static NotSupportedException Unused()
            => new("This fake member is not used by the manager chat voice component test.");
    }

    private static void ActivateProcessDetailTab(
        IRenderedComponent<ProcessWorkspaceShell> cut,
        string tabTestId,
        string panelTestId)
    {
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find($"[data-testid='{tabTestId}']")));
        cut.Find($"[data-testid='{tabTestId}']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find($"[data-testid='{panelTestId}']")));
    }

    private static void SetPrivateField<T>(object instance, string fieldName, T value)
    {
        var field = instance.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(instance, value);
    }

    private static bool IsSelectedDefinitionFact(AgentChatContextPositionFact fact)
        => fact.Name is
            "definition-status" or
            "definition-scope" or
            "definition-criticality" or
            "definition-operating-mode" or
            "definition-compatibility-issues";

    private static void ConfigureReadyRefresh(
        RecordingProcessWorkspaceProjectionClient client,
        Func<DateTimeOffset> observedAtUtc)
    {
        client.ShellResultTransform = (_, projection) => projection with
        {
            Refresh = new ProcessWorkspaceProjectionRefreshProjection(
                ProcessWorkspaceProjectionStatus.Ready,
                observedAtUtc().ToUniversalTime(),
                SourceGlobalSequence: 0,
                BacklogEventCount: 0,
                "Projection data is ready.")
        };
    }

    private sealed class RecordingProcessWorkspaceProjectionClient : IProcessWorkspaceProjectionClient
    {
        private readonly List<TaskCompletionSource> shellRequestCompletions = [];
        private readonly List<TaskCompletionSource> editorCommandCompletions = [];
        private int completedShellRequestCount;
        private int editorCommandCount;

        public List<ProcessWorkspaceShellRequest> Requests { get; } = [];

        public bool DeferShellRequests { get; set; }

        public bool DeferEditorCommands { get; set; }

        public int EditorCommandCount => Volatile.Read(ref editorCommandCount);

        public int CompletedShellRequestCount => Volatile.Read(ref completedShellRequestCount);

        public Func<int, ProcessWorkspaceShellProjection, ProcessWorkspaceShellProjection>? ShellResultTransform { get; set; }

        public ProcessWorkspaceShellRequest? LastRequest => Requests.LastOrDefault();

        public int FeedDefaultsCommandCount { get; private set; }

        public ProcessDefinitionEditorCommand? LastEditorCommand { get; private set; }

        public ProcessDefinitionRoleEditorCommand? LastRoleCommand { get; private set; }

        public ProcessDefinitionCanvasCommand? LastCanvasCommand { get; private set; }

        public ProcessDefinitionStepEditorCommand? LastStepCommand { get; private set; }

        public ProcessTemplateImportCommand? LastTemplateImportCommand { get; private set; }

        public ProcessRuntimeOperatorActionCommand? LastOperatorActionCommand { get; private set; }

        public async Task<ProcessWorkspaceShellProjection> GetShellAsync(
            ProcessWorkspaceShellRequest request,
            CancellationToken cancellationToken = default)
        {
            var requestIndex = Requests.Count;
            Requests.Add(request);
            if (DeferShellRequests)
            {
                var completion = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                shellRequestCompletions.Add(completion);
                await completion.Task.WaitAsync(cancellationToken);
            }
            else
            {
                await Task.Yield();
            }

            var projection = CreateShell(request, lastReceipt: null);
            Interlocked.Increment(ref completedShellRequestCount);
            return ShellResultTransform?.Invoke(requestIndex, projection) ?? projection;
        }

        public void CompleteShellRequest(int requestIndex)
            => shellRequestCompletions[requestIndex].TrySetResult();

        public void CompleteEditorCommand(int commandIndex)
            => editorCommandCompletions[commandIndex].TrySetResult();

        public Task<ProcessDefinitionCatalogCommandReceipt> FeedDefaultDefinitionsAsync(
            ProcessDefinitionFeedDefaultsCommand command,
            CancellationToken cancellationToken = default)
        {
            FeedDefaultsCommandCount++;
            return Task.FromResult(new ProcessDefinitionCatalogCommandReceipt(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                ProcessDefinitionCatalogCommandKind.FeedDefaults,
                ProcessDefinitionCatalogCommandStatus.Accepted,
                new ProcessDefinitionCatalogRefreshToken("feed-defaults:test"),
                AffectedDefinitionCount: 2,
                Now,
                "2 default process definition(s) are available from template pack test."));
        }

        public async Task<ProcessDefinitionEditorCommandResult> ExecuteDefinitionEditorCommandAsync(
            ProcessDefinitionEditorCommand command,
            CancellationToken cancellationToken = default)
        {
            LastEditorCommand = command;
            if (DeferEditorCommands)
            {
                var completion = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                editorCommandCompletions.Add(completion);
                Interlocked.Increment(ref editorCommandCount);
                await completion.Task.WaitAsync(cancellationToken);
            }

            var lint = CreateEditorLint(command);
            var status = lint.HasBlockingIssues
                ? ProcessDefinitionEditorCommandStatus.Rejected
                : ProcessDefinitionEditorCommandStatus.Accepted;
            var authoringStatus = command.CommandKind == ProcessDefinitionEditorCommandKind.Publish && status == ProcessDefinitionEditorCommandStatus.Accepted
                ? ProcessDefinitionAuthoringStatus.Published
                : ProcessDefinitionAuthoringStatus.Draft;
            var versionToken = new ProcessDefinitionEditorVersionToken($"{command.CommandKind.ToString().ToLowerInvariant()}:test");
            var receipt = new ProcessDefinitionEditorCommandReceipt(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                command.CommandKind,
                status,
                versionToken,
                Now,
                status == ProcessDefinitionEditorCommandStatus.Accepted
                    ? command.CommandKind == ProcessDefinitionEditorCommandKind.Publish
                        ? "Definition published."
                        : "Draft saved."
                    : "Definition was not published because blocking lint issues remain.",
                lint.Issues);
            var projection = CreateEditor(command.Draft.DefinitionKey, command.Draft, authoringStatus, versionToken, lint, receipt);
            return new ProcessDefinitionEditorCommandResult(receipt, projection);
        }

        public Task<ProcessDefinitionRoleEditorCommandResult> ExecuteDefinitionRoleEditorCommandAsync(
            ProcessDefinitionRoleEditorCommand command,
            CancellationToken cancellationToken = default)
        {
            LastRoleCommand = command;
            var lint = CreateRoleLint(command);
            var status = lint.HasBlockingIssues
                ? ProcessDefinitionRoleCommandStatus.Rejected
                : ProcessDefinitionRoleCommandStatus.Accepted;
            var versionToken = new ProcessDefinitionRoleEditorVersionToken($"{command.CommandKind.ToString().ToLowerInvariant()}:test");
            var receipt = new ProcessDefinitionRoleCommandReceipt(
                Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                command.CommandKind,
                status,
                versionToken,
                Now,
                status == ProcessDefinitionRoleCommandStatus.Accepted
                    ? command.CommandKind == ProcessDefinitionRoleCommandKind.ApplyTemplate
                        ? "Role template applied."
                        : "Role saved."
                    : "Role was not saved because blocking role lint issues remain.",
                lint.Issues);
            var projection = CreateRoleEditor(command.DefinitionKey, command.Draft, versionToken, lint, receipt);
            return Task.FromResult(new ProcessDefinitionRoleEditorCommandResult(receipt, projection));
        }

        public Task<ProcessDefinitionCanvasCommandResult> ExecuteDefinitionCanvasCommandAsync(
            ProcessDefinitionCanvasCommand command,
            CancellationToken cancellationToken = default)
        {
            LastCanvasCommand = command;
            var versionToken = new ProcessDefinitionCanvasVersionToken($"{command.CommandKind.ToString().ToLowerInvariant()}:test");
            var receipt = new ProcessDefinitionCanvasCommandReceipt(
                Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                command.CommandKind,
                ProcessDefinitionCanvasCommandStatus.Accepted,
                versionToken,
                Now,
                command.CommandKind == ProcessDefinitionCanvasCommandKind.Recompose
                    ? "Canvas recomposed."
                    : "Canvas command accepted.");
            var projection = CreateCanvas(command.DefinitionKey, versionToken, receipt, command.CommandKind);
            return Task.FromResult(new ProcessDefinitionCanvasCommandResult(receipt, projection));
        }

        public Task<ProcessDefinitionStepEditorCommandResult> ExecuteDefinitionStepEditorCommandAsync(
            ProcessDefinitionStepEditorCommand command,
            CancellationToken cancellationToken = default)
        {
            LastStepCommand = command;
            var lint = CreateStepLint(command);
            var status = lint.HasBlockingIssues
                ? ProcessDefinitionStepCommandStatus.Rejected
                : ProcessDefinitionStepCommandStatus.Accepted;
            var versionToken = new ProcessDefinitionStepEditorVersionToken($"{command.CommandKind.ToString().ToLowerInvariant()}:test");
            var receipt = new ProcessDefinitionStepCommandReceipt(
                Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                command.CommandKind,
                status,
                versionToken,
                Now,
                status == ProcessDefinitionStepCommandStatus.Accepted
                    ? command.CommandKind switch
                    {
                        ProcessDefinitionStepCommandKind.AddBranchOutcome => "Route added.",
                        ProcessDefinitionStepCommandKind.AddArtifactExpectation => "Artifact added.",
                        ProcessDefinitionStepCommandKind.MapSubprocess => "Subprocess mapped.",
                        _ => "Step saved."
                    }
                    : "Step command rejected.",
                lint.Issues);
            var projection = CreateStepEditor(command.DefinitionKey, command.Draft, versionToken, lint, receipt, command.CommandKind);
            return Task.FromResult(new ProcessDefinitionStepEditorCommandResult(receipt, projection));
        }

        public Task<ProcessTemplateImportCommandResult> ExecuteTemplateImportCommandAsync(
            ProcessTemplateImportCommand command,
            CancellationToken cancellationToken = default)
        {
            LastTemplateImportCommand = command;
            var versionToken = new ProcessTemplateCatalogVersionToken("templates:test:1");
            var receipt = new ProcessTemplateImportCommandReceipt(
                Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
                command.CommandKind,
                ProcessTemplateImportCommandStatus.Accepted,
                versionToken,
                Now,
                command.CommandKind switch
                {
                    ProcessTemplateImportCommandKind.ImportRole => "Role component imported.",
                    ProcessTemplateImportCommandKind.ImportArtifact => "Artifact component imported.",
                    _ => "Process template imported."
                });
            var imported = new[]
            {
                new ProcessTemplateImportedComponentProjection(
                    command.ItemKey,
                    command.CommandKind switch
                    {
                        ProcessTemplateImportCommandKind.ImportRole => ProcessTemplateCatalogItemKind.Role,
                        ProcessTemplateImportCommandKind.ImportArtifact => ProcessTemplateCatalogItemKind.Artifact,
                        _ => ProcessTemplateCatalogItemKind.Process
                    },
                    command.ItemKey.Value,
                    "blazor-app-delivery",
                    command.ItemKey.Value,
                    "sha256:component-test",
                    command.TargetStepKey,
                    Now)
            };
            var projection = CreateTemplateCatalog(command.TargetDefinitionKey, command.Query, receipt, imported);
            return Task.FromResult(new ProcessTemplateImportCommandResult(receipt, projection));
        }

        public Task<ProcessRuntimeOperatorActionResult> ExecuteRuntimeOperatorActionAsync(
            ProcessRuntimeOperatorActionCommand command,
            CancellationToken cancellationToken = default)
        {
            LastOperatorActionCommand = command;
            return Task.FromResult(new ProcessRuntimeOperatorActionResult(
                command.RunId,
                command.StepInstanceId,
                command.Kind,
                ProcessRuntimeTransitionOutcome.Applied,
                ProcessRuntimeStatus.Active,
                Diagnostics: []));
        }

        private static ProcessWorkspaceShellProjection CreateShell(
            ProcessWorkspaceShellRequest request,
            ProcessDefinitionCatalogCommandReceipt? lastReceipt)
        {
            var catalog = CreateDefinitionCatalog(
                request.DefinitionCatalogQuery,
                request.TemplateCatalogQuery,
                lastReceipt,
                request.DefinitionLoadOptions ?? ProcessDefinitionWorkspaceLoadOptions.Full);
            var authorization = new ProcessWorkspaceAuthorizationProjection(
                CanReadDefinitions: true,
                CanRefreshProjections: true,
                CanOpenAgentContext: true,
                CanEditDefinitions: false,
                CanLaunchRuns: false);
            var runtime = CreateRuntimeWorkspace(request);
            var provenance = CreateTestProvenance(request, runtime);
            runtime = runtime with
            {
                Provenance = provenance
            };

            return new ProcessWorkspaceShellProjection(
                request.Scope,
                request.Selection,
                request.Scope.Kind == ProcessWorkspaceScopeKind.Project ? "Project processes" : "Processes",
                "Projection-first process workspace.",
                catalog,
                new ProcessLiveRunSummaryProjection(0, 0, 0, null, "Runtime projection snapshots are not available in this workspace shell."),
                new ProcessWorkspaceProjectionRefreshProjection(
                    request.ForceRefresh
                        ? ProcessWorkspaceProjectionStatus.RefreshRequested
                        : ProcessWorkspaceProjectionStatus.ProjectionStoreUnavailable,
                    Now,
                    SourceGlobalSequence: 0,
                    BacklogEventCount: 0,
                    request.ForceRefresh
                        ? "Projection refresh was requested through the application boundary."
                        : "Projection store integration is pending; runtime data is intentionally not read by the UI shell."),
                authorization,
                CreateTabs(),
                CreateCommands(),
                CreateAgentEntry(request))
            {
                Runtime = runtime,
                Provenance = provenance
            };
        }

        private static ProcessWorkspaceProvenanceVector CreateTestProvenance(
            ProcessWorkspaceShellRequest request,
            ProcessRuntimeWorkspaceProjection runtime)
        {
            var options = request.RuntimeQuery?.LoadOptions ?? ProcessRuntimeWorkspaceLoadOptions.Full;

            ProcessProjectionComponentProvenance Present(
                ProcessWorkspaceProvenanceComponent component,
                object content)
                => ProcessProjectionComponentProvenance.Present(
                    ProcessProjectionComponentSource.ShellProjection,
                    ProcessProjectionContentFingerprintFactory.Create(component, content),
                    runtime.Freshness);

            ProcessProjectionComponentProvenance Requested(
                bool include,
                ProcessWorkspaceProvenanceComponent component,
                object content)
                => include
                    ? Present(component, content)
                    : ProcessProjectionComponentProvenance.NotRequested(
                        ProcessProjectionComponentAbsenceReason.LoadOptionDisabled);

            var selectedRunRequested = options.IncludeSelectedRun;
            var selectedRunPresent = selectedRunRequested && runtime.SelectedRun is not null;
            var selectedRunProvenance = !selectedRunRequested
                ? ProcessProjectionComponentProvenance.NotRequested(
                    ProcessProjectionComponentAbsenceReason.LoadOptionDisabled)
                : selectedRunPresent
                    ? Present(
                        ProcessWorkspaceProvenanceComponent.SelectedRunDetail,
                        runtime.SelectedRun!)
                    : ProcessProjectionComponentProvenance.Absent(
                        ProcessProjectionComponentSource.ShellProjection,
                        ProcessProjectionComponentAbsenceReason.NoSelection);
            var selectedRecordProvenance = !selectedRunRequested
                ? ProcessProjectionComponentProvenance.NotRequested(
                    ProcessProjectionComponentAbsenceReason.LoadOptionDisabled)
                : selectedRunPresent
                    ? Present(
                        ProcessWorkspaceProvenanceComponent.SelectedRunRecord,
                        new
                        {
                            runtime.SelectedRunId,
                            runtime.SelectedRun!.Status
                        })
                    : ProcessProjectionComponentProvenance.Absent(
                        ProcessProjectionComponentSource.ShellProjection,
                        ProcessProjectionComponentAbsenceReason.NoSelection);

            return new ProcessWorkspaceProvenanceVector(
                Present(
                    ProcessWorkspaceProvenanceComponent.Selection,
                    new
                    {
                        request.Scope,
                        request.Selection,
                        runtime.SelectedRunId
                    }),
                Present(
                    ProcessWorkspaceProvenanceComponent.ShellRefresh,
                    new
                    {
                        request.ForceRefresh,
                        runtime.Freshness
                    }),
                Present(
                    ProcessWorkspaceProvenanceComponent.DefinitionCatalog,
                    request.DefinitionCatalogQuery),
                Present(
                    ProcessWorkspaceProvenanceComponent.LiveRunSummary,
                    new
                    {
                        runtime.Stats.ActiveRunCount,
                        runtime.Stats.AttentionRunCount,
                        runtime.Stats.FailedRunCount
                    }),
                Present(
                    ProcessWorkspaceProvenanceComponent.LiveRuns,
                    runtime.Runs.Select(run => new
                    {
                        run.RunId,
                        run.Status,
                        run.LastEventAtUtc
                    }).ToArray()),
                selectedRunProvenance,
                selectedRecordProvenance,
                Requested(
                    options.IncludeHistory,
                    ProcessWorkspaceProvenanceComponent.HistoryPage,
                    runtime.Events),
                Requested(
                    options.IncludeMetricHistory,
                    ProcessWorkspaceProvenanceComponent.MetricHistory,
                    runtime.MetricPoints),
                Requested(
                    options.IncludeActiveAgents,
                    ProcessWorkspaceProvenanceComponent.ActiveAgents,
                    runtime.ActiveAgents),
                Requested(
                    options.IncludeUsageTelemetry,
                    ProcessWorkspaceProvenanceComponent.UsageTelemetry,
                    runtime.Stats),
                Present(
                    ProcessWorkspaceProvenanceComponent.DerivedProjection,
                    new
                    {
                        runtime.Stats,
                        runtime.AttentionSummary
                    }));
        }

        private static ProcessRuntimeWorkspaceProjection CreateRuntimeWorkspace(ProcessWorkspaceShellRequest request)
        {
            var loadOptions = request.RuntimeQuery?.LoadOptions ?? ProcessRuntimeWorkspaceLoadOptions.Full;
            var runId = new ProcessRunId(Guid.Parse("77777777-7777-7777-7777-777777777777"));
            var secondRunId = new ProcessRunId(Guid.Parse("88888888-8888-8888-8888-888888888888"));
            var shouldResolveSelectedRun = loadOptions.IncludeSelectedRun ||
                loadOptions.IncludeHistory ||
                loadOptions.IncludeMetricHistory ||
                loadOptions.IncludeActiveAgents;
            ProcessRunId? selectedRunId = shouldResolveSelectedRun
                ? request.RuntimeQuery?.SelectedRunId == secondRunId.Value
                    ? secondRunId
                    : request.RuntimeQuery?.SelectedRunId == runId.Value
                        ? runId
                        : request.RuntimeQuery?.AutoSelectRun != false
                            ? runId
                            : null
                : null;
            var freshness = new ProcessProjectionFreshness(
                Now,
                SourceGlobalSequence: 12,
                new ProcessProjectionLag(12, 12, BacklogEventCount: 0));
            var eventRunId = selectedRunId ?? runId;
            var events = loadOptions.IncludeHistory ? CreateRuntimeEvents(eventRunId) : [];
            var metricEvents = loadOptions.IncludeMetricHistory ? CreateRuntimeEvents(eventRunId) : [];
            var selectedRun = selectedRunId is null || !loadOptions.IncludeSelectedRun
                ? null
                : new ProcessRunDetailProjection(
                    selectedRunId.Value,
                    selectedRunId.Value,
                    selectedRunId == runId ? ProcessProjectedRunStatus.NeedsAttention : ProcessProjectedRunStatus.Active,
                    Now.AddMinutes(-35),
                    Now.AddMinutes(-2),
                    freshness,
                    CreateRuntimeEvents(selectedRunId.Value).Select(ToLiveRunEvent).ToArray());
            var runs = new[]
            {
                CreateLiveRun(runId, ProcessProjectedRunStatus.NeedsAttention, freshness, CreateRuntimeEvents(runId)),
                CreateLiveRun(secondRunId, ProcessProjectedRunStatus.Active, freshness, CreateRuntimeEvents(secondRunId, startSequence: 20))
            };
            var historyWindow = request.RuntimeQuery?.HistoryWindow ?? ProcessRuntimeHistoryWindow.OneDay;
            var page = request.RuntimeQuery?.EventPage ?? 0;
            var pageSize = request.RuntimeQuery?.EventPageSize ?? 25;
            var useManagerChatUsageTelemetry = loadOptions.IncludeUsageTelemetry &&
                loadOptions.IncludeSelectedRun &&
                !loadOptions.IncludeMetricHistory;

            return new ProcessRuntimeWorkspaceProjection(
                historyWindow,
                page,
                pageSize,
                HasMoreEvents: false,
                selectedRunId?.Value,
                selectedRun,
                runs,
                events,
                runs.SelectMany(run => run.Incidents).ToArray(),
                loadOptions.IncludeMetricHistory
                    ?
                    [
                        new ProcessManagerMessageProjection(
                            "manager-message-test",
                            eventRunId,
                            eventRunId,
                            "Manager Incident Raised",
                            "Manager incident raised; operator review is required.",
                            Now.AddMinutes(-2),
                            ProcessProjectedSensitivity.Normal,
                            RestrictedDiagnosticReference: null)
                    ]
                    : [],
                loadOptions.IncludeActiveAgents
                    ?
                    [
                        new ProcessRuntimeActiveAgentProjection(
                            eventRunId.Value,
                            Guid.Parse("99999999-9999-9999-9999-999999999999"),
                            $"Run {eventRunId.Value.ToString("N")[..8]}",
                            "implementation",
                            "lead-engineer",
                            "agent",
                            "agent-dotnet-developer",
                            ".NET Developer",
                            "Running",
                            IsWorking: true,
                            IsLeaseExpired: false,
                            Now.AddMinutes(-1),
                            Now.AddMinutes(-30),
                            Now.AddMinutes(20),
                            ".NET Developer is Running on implementation as lead-engineer.")
                    ]
                    : [],
                new ProcessRuntimeStatsProjection(
                    ObservedRunCount: runs.Length,
                    ActiveRunCount: 2,
                    AttentionRunCount: 1,
                    FailedRunCount: 0,
                    EventCount: loadOptions.IncludeMetricHistory ? metricEvents.Count : events.Count,
                    ManagerEventCount: loadOptions.IncludeMetricHistory ? 1 : 0,
                    ToolCallCount: loadOptions.IncludeMetricHistory ? metricEvents.Count : events.Count,
                    DurationMs: 33 * 60 * 1000,
                    InputTokens: useManagerChatUsageTelemetry ? 1_234 : 0,
                    CachedInputTokens: useManagerChatUsageTelemetry ? 234 : 0,
                    OutputTokens: useManagerChatUsageTelemetry ? 432 : 0,
                    TotalTokens: useManagerChatUsageTelemetry ? 1_666 : 0,
                    EstimatedCost: useManagerChatUsageTelemetry ? 0.130000m : 0m,
                    ActualCost: useManagerChatUsageTelemetry ? 0.123456m : 0m),
                metricEvents.Select(runtimeEvent => new ProcessRuntimeMetricPointProjection(
                    runtimeEvent.OccurredAtUtc,
                    EventCount: 1,
                    ManagerEventCount: runtimeEvent.EventType.StartsWith("Manager", StringComparison.Ordinal) ? 1 : 0,
                    ToolCallCount: 1,
                    DurationMs: 60_000,
                    InputTokens: 0,
                    CachedInputTokens: 0,
                    OutputTokens: 0,
                    TotalTokens: 0,
                    EstimatedCost: 0m,
                    ActualCost: 0m)).ToArray(),
                loadOptions.IncludeMetricHistory
                    ?
                    [
                        new ProcessRuntimeToolUsageProjection("Step Running", 1, Now.AddMinutes(-30), "1 event, latest test."),
                        new ProcessRuntimeToolUsageProjection("Manager Incident Raised", 1, Now.AddMinutes(-2), "1 event, latest test.")
                    ]
                    : [],
                freshness,
                $"2 run(s), 2 active, 1 needing attention, {events.Count.ToString(CultureInfo.InvariantCulture)} event(s) on this page.",
                "Cause: Manager incident raised. Next action: open the selected run and review manager messages.")
            {
                ReusableRuns = runs
            };
        }

        private static ProcessLiveProcessSnapshot CreateLiveRun(
            ProcessRunId runId,
            ProcessProjectedRunStatus status,
            ProcessProjectionFreshness freshness,
            IReadOnlyList<ProcessTimelineEventProjection> events)
        {
            IReadOnlyList<ProcessIncidentProjection> incidents = status == ProcessProjectedRunStatus.NeedsAttention
                ?
                [
                    new ProcessIncidentProjection(
                        "incident-test",
                        runId,
                        runId,
                        "ManagerIncident",
                        "NeedsAttention",
                        "Raised",
                        "Manager incident raised",
                        "runtime-event:test",
                        Now.AddMinutes(-2))
                ]
                : Array.Empty<ProcessIncidentProjection>();

            var snapshot = new ProcessLiveProcessSnapshot(
                runId,
                runId,
                status,
                IsActive: status is ProcessProjectedRunStatus.Active or ProcessProjectedRunStatus.NeedsAttention,
                Now.AddMinutes(-35),
                Now.AddMinutes(-2),
                freshness,
                events.Select(ToLiveRunEvent).ToArray(),
                incidents);
            snapshot = snapshot with { ProcessName = ResolveProcessName(runId.Value) };
            if (runId.Value == ProjectSubprocessRunId)
            {
                snapshot = CreateProjectSubprocessLiveRun(snapshot);
            }

            return status == ProcessProjectedRunStatus.NeedsAttention
                ? snapshot with
                {
                    OperatorActions =
                    [
                        new ProcessRuntimeOperatorActionProjection(
                            runId.Value,
                            Guid.Parse("99999999-9999-9999-9999-999999999998"),
                            "implement-code-change",
                            ProcessRuntimeStepStatus.Blocked.ToString(),
                            "dotnet-developer",
                            ".NET Developer",
                            ".NET Developer",
                            ProcessRuntimeOperatorActionKind.RequestRework,
                            "Approve rework",
                            "Root action: approve manager-guided rework for implement-code-change after Blocked. Last strategy outcome: NeedsManager. Assigned role: .NET Developer. Executor: .NET Developer.",
                            IsEnabled: true,
                            DisabledReason: null)
                        {
                            ProblemSummary = "implement-code-change is Blocked on attempt 1. The last strategy outcome was NeedsManager and the runtime applied Blocked. This is the actionable upstream step for role .NET Developer, currently assigned to .NET Developer.",
                            RequiredOperatorDecision = "Approve rework to return implement-code-change from Blocked to Ready and let the process manager dispatch .NET Developer again. Add an operator note if the agent needs extra context.",
                            RecommendedInstruction = "Manager-approved rework for step 'implement-code-change'. Resolve the previous NeedsManager outcome, preserve accepted upstream artifacts, produce the required evidence for role '.NET Developer', and continue the process. Previous executor: .NET Developer. Step status before rework: Blocked.",
                            PrimaryRootCause = true
                        }
                    ]
                }
                : snapshot;
        }

        private static ProcessLiveProcessSnapshot CreateProjectSubprocessLiveRun(ProcessLiveProcessSnapshot snapshot)
        {
            var currentStepId = Guid.Parse("88888888-8888-8888-8888-aaaaaaaaaaaa");
            var childRunId = Guid.Parse("88888888-8888-8888-8888-bbbbbbbbbbbb");

            return snapshot with
            {
                ProjectId = ProjectSubprocessProjectId,
                ProjectName = "Apollo Delivery",
                IsSubprocess = true,
                CurrentStep = new ProcessRuntimeCurrentStepProjection(
                    snapshot.RunId.Value,
                    currentStepId,
                    "await-child-artifacts",
                    ProcessRuntimeStepStatus.Waiting.ToString(),
                    "process-manager",
                    "Process manager",
                    "Process manager",
                    AttemptNumber: 1,
                    IsWorking: false,
                    IsLeaseExpired: false,
                    Now.AddMinutes(-1),
                    ClaimedAtUtc: null,
                    LeaseExpiresAtUtc: null,
                    "Process manager is waiting for child process evidence."),
                WaitingOnChildRuns =
                [
                    new ProcessRuntimeChildRunWaitProjection(
                        snapshot.RunId.Value,
                        currentStepId,
                        "await-child-artifacts",
                        ProcessRuntimeStepStatus.Waiting.ToString(),
                        childRunId,
                        ProcessRuntimeStatus.Active.ToString(),
                        "collect-child-evidence",
                        ProcessRuntimeStepStatus.Running.ToString(),
                        "Process manager is waiting for child process evidence.")
                ]
            };
        }

        private static IReadOnlyList<ProcessTimelineEventProjection> CreateRuntimeEvents(ProcessRunId runId, int startSequence = 10)
            =>
            [
                new ProcessTimelineEventProjection(
                    RuntimeEventId.New(),
                    startSequence,
                    runId,
                    runId,
                    "ProcessRunActivated",
                    Now.AddMinutes(-35),
                    ProcessProjectedSensitivity.Normal,
                    "ProcessRunActivated",
                    RestrictedDiagnosticReference: null),
                new ProcessTimelineEventProjection(
                    RuntimeEventId.New(),
                    startSequence + 1,
                    runId,
                    runId,
                    "StepRunning",
                    Now.AddMinutes(-30),
                    ProcessProjectedSensitivity.Normal,
                    "StepRunning",
                    RestrictedDiagnosticReference: null),
                new ProcessTimelineEventProjection(
                    RuntimeEventId.New(),
                    startSequence + 2,
                    runId,
                    runId,
                    "ManagerIncidentRaised",
                    Now.AddMinutes(-2),
                    ProcessProjectedSensitivity.Normal,
                    "ManagerIncidentRaised",
                    RestrictedDiagnosticReference: null)
            ];

        private static ProcessLiveRunEventProjection ToLiveRunEvent(ProcessTimelineEventProjection runtimeEvent)
            => new(
                runtimeEvent.EventId,
                runtimeEvent.GlobalSequence,
                runtimeEvent.RootRunId,
                runtimeEvent.RunId,
                runtimeEvent.EventType,
                runtimeEvent.OccurredAtUtc,
                runtimeEvent.Sensitivity,
                runtimeEvent.Summary,
                runtimeEvent.RestrictedDiagnosticReference);

        private static ProcessDefinitionCatalogProjection CreateDefinitionCatalog(
            ProcessDefinitionCatalogQueryProjection query,
            ProcessTemplateCatalogQueryProjection templateQuery,
            ProcessDefinitionCatalogCommandReceipt? lastReceipt,
            ProcessDefinitionWorkspaceLoadOptions loadOptions)
        {
            var items = new[]
            {
                new ProcessDefinitionCatalogItemProjection(
                    new ProcessDefinitionCatalogItemKey("blazor-app-delivery"),
                    ProcessDefinitionCatalogScopeKind.Global,
                    "Blazor app delivery",
                    "Build and prove a Blazor application.",
                    ProcessDefinitionCatalogItemStatus.TemplateDefault,
                    "High",
                    "GovernedLive",
                    Now,
                    CompatibilityIssueCount: 0),
                new ProcessDefinitionCatalogItemProjection(
                    new ProcessDefinitionCatalogItemKey("architecture-decision-governance"),
                    ProcessDefinitionCatalogScopeKind.Global,
                    "Architecture decision governance",
                    "Review and approve architecture decisions.",
                    ProcessDefinitionCatalogItemStatus.TemplateDefault,
                    "Medium",
                    "Assisted",
                    Now,
                    CompatibilityIssueCount: 0)
            };
            ProcessDefinitionCatalogItemProjection[] scopeFiltered = query.ScopeFilter == ProcessDefinitionCatalogScopeKind.Project
                ? []
                : items;
            var filtered = string.IsNullOrWhiteSpace(query.SearchText)
                ? scopeFiltered
                : scopeFiltered
                    .Where(item => item.Name.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase) ||
                                   item.Key.Value.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            var selected = query.SelectedDefinitionKey is { } selectedKey
                ? filtered.FirstOrDefault(item => item.Key == selectedKey)
                : filtered.FirstOrDefault();

            return new ProcessDefinitionCatalogProjection(
                PublishedDefinitionCount: items.Length,
                DraftDefinitionCount: 0,
                TemplateCompatibilityIssueCount: 0,
                string.IsNullOrWhiteSpace(query.SearchText)
                    ? "2 default definition(s) loaded from template pack test."
                    : $"{filtered.Length} definition(s) match '{query.SearchText}'.",
                query.SearchText ?? string.Empty,
                selected?.Key,
                [
                    new(ProcessDefinitionCatalogScopeKind.All, "All definitions", "All visible definitions.", items.Length, query.ScopeFilter == ProcessDefinitionCatalogScopeKind.All),
                    new(ProcessDefinitionCatalogScopeKind.Global, "Global defaults", "Template-backed defaults.", items.Length, query.ScopeFilter == ProcessDefinitionCatalogScopeKind.Global),
                    new(ProcessDefinitionCatalogScopeKind.Project, "Project", "Project-specific definitions.", Count: 0, IsSelected: query.ScopeFilter == ProcessDefinitionCatalogScopeKind.Project)
                ],
                filtered,
                selected,
                selected is null || !loadOptions.IncludeSelectedEditor ? null : CreateEditor(selected.Key, templateQuery, loadOptions),
                lastReceipt);
        }

        private static ProcessDefinitionEditorProjection CreateEditor(
            ProcessDefinitionCatalogItemKey key,
            ProcessTemplateCatalogQueryProjection? templateQuery = null,
            ProcessDefinitionWorkspaceLoadOptions? loadOptions = null)
        {
            loadOptions ??= ProcessDefinitionWorkspaceLoadOptions.Full;
            var draft = new ProcessDefinitionEditorDraftProjection(
                key,
                new ProcessDefinitionEditorIdentityProjection(
                    key.Value == "blazor-app-delivery" ? "Blazor app delivery" : "Architecture decision governance",
                    "Global",
                    "Delivery requester",
                    "Delivery owner",
                    "Build and prove the process.",
                    "Deliver a useful process."),
                new ProcessDefinitionEditorGovernanceProjection(
                    ProcessDefinitionCriticalityLevel.High,
                    ProcessDefinitionAutonomyLevel.Guarded,
                    ProcessDefinitionOperatingModeKind.GovernedLive,
                    ProcessDefinitionAuthoringStatus.TemplateDefault,
                    "Manager override.",
                    "Governance notes.",
                    "Change summary.",
                    "Governance policy."),
                new ProcessDefinitionEditorContractProjection(
                    "Interface contract.",
                    "Constitution rule.",
                    "Operating mode summary."),
                new ProcessDefinitionEditorSimulationProjection(
                    "Safe deterministic simulation.",
                    StepCount: 5,
                    RequiredRoleCount: 2,
                    RequiredArtifactExpectationCount: 3,
                    IsReadyForSimulation: true));

            var editor = CreateEditor(
                key,
                draft,
                ProcessDefinitionAuthoringStatus.TemplateDefault,
                new ProcessDefinitionEditorVersionToken($"template:{key.Value}"),
                new ProcessDefinitionEditorLintProjection([]),
                lastReceipt: null);
            return editor with
            {
                RoleEditor = loadOptions.IncludeRoleEditor ? editor.RoleEditor : null,
                Canvas = loadOptions.IncludeCanvas ? editor.Canvas : null,
                StepEditor = loadOptions.IncludeStepEditor ? editor.StepEditor : null,
                TemplateCatalog = loadOptions.IncludeTemplateCatalog
                    ? CreateTemplateCatalog(
                        key,
                        templateQuery ?? new ProcessTemplateCatalogQueryProjection(
                            SearchText: null,
                            ProcessTemplateCatalogCategoryKind.All,
                            SelectedItemKey: null,
                            ProcessTemplateCatalogPreviewTabKind.Overview,
                            Take: 50),
                        lastReceipt: null,
                        importedComponents: [])
                    : null
            };
        }

        private static ProcessDefinitionEditorProjection CreateEditor(
            ProcessDefinitionCatalogItemKey key,
            ProcessDefinitionEditorDraftProjection draft,
            ProcessDefinitionAuthoringStatus status,
            ProcessDefinitionEditorVersionToken versionToken,
            ProcessDefinitionEditorLintProjection lint,
            ProcessDefinitionEditorCommandReceipt? lastReceipt)
            => new(
                key,
                versionToken,
                status,
                draft.Identity,
                draft.Governance with { WorkingStatus = status },
                draft.Contracts,
                draft.Simulation,
                lint,
                [
                    new(ProcessDefinitionEditorCommandKind.SaveDraft, "Save draft", "save", IsEnabled: true, DisabledReason: null),
                    new(ProcessDefinitionEditorCommandKind.Publish, "Publish", "publish", IsEnabled: true, DisabledReason: null),
                    new(ProcessDefinitionEditorCommandKind.Archive, "Archive", "archive", IsEnabled: true, DisabledReason: null),
                    new(ProcessDefinitionEditorCommandKind.Delete, "Delete", "delete", IsEnabled: true, DisabledReason: null)
                ],
                lastReceipt)
            {
                RoleEditor = CreateRoleEditor(key),
                Canvas = CreateCanvas(key),
                StepEditor = CreateStepEditor(key)
            };

        private static ProcessDefinitionRoleEditorProjection CreateRoleEditor(ProcessDefinitionCatalogItemKey key)
        {
            var draft = CreateRoleDraft();
            return CreateRoleEditor(
                key,
                draft,
                new ProcessDefinitionRoleEditorVersionToken($"template:{key.Value}:roles"),
                new ProcessDefinitionRoleLintProjection([]),
                lastReceipt: null);
        }

        private static ProcessDefinitionRoleEditorProjection CreateRoleEditor(
            ProcessDefinitionCatalogItemKey key,
            ProcessDefinitionRoleDraftProjection draft,
            ProcessDefinitionRoleEditorVersionToken versionToken,
            ProcessDefinitionRoleLintProjection lint,
            ProcessDefinitionRoleCommandReceipt? lastReceipt)
        {
            var role = new ProcessDefinitionRoleProjection(
                draft.RoleKey,
                draft.DisplayName,
                draft.SnapshotSummary,
                draft,
                StepBindingCount: 1);
            return new ProcessDefinitionRoleEditorProjection(
                key,
                versionToken,
                role.RoleKey,
                [role],
                role,
                [
                    new ProcessDefinitionRoleTemplateActionProjection(
                        new ProcessDefinitionRoleTemplateActionKey("role-template.solution-architect"),
                        "Solution architect template",
                        "Owns architecture decisions and technical tradeoffs.",
                        new ProcessDefinitionRoleKey("solution-architect"),
                        "solution-architect",
                        "Solution architect next",
                        ProcessDefinitionRoleExecutorKind.PersonOrAgent,
                        DefaultAllocationPercent: 60)
                ],
                [
                    new ProcessDefinitionStepRoleBindingProjection(
                        new ProcessDefinitionStepKey("architecture-decision"),
                        "Architecture decision",
                        draft.RoleKey,
                        draft.DisplayName,
                        ProcessStepRoleResponsibilityKind.Approver,
                        IsRequired: true,
                        FallbackOrder: 1,
                        "Rebind to the architecture board when the primary owner is unavailable.")
                ],
                lint,
                [
                    new(ProcessDefinitionRoleCommandKind.AddRole, "Add role", "add", IsEnabled: true, DisabledReason: null),
                    new(ProcessDefinitionRoleCommandKind.SaveRole, "Save role", "save", IsEnabled: true, DisabledReason: null),
                    new(ProcessDefinitionRoleCommandKind.ApplyTemplate, "Apply template", "content_copy", IsEnabled: true, DisabledReason: null),
                    new(ProcessDefinitionRoleCommandKind.DeleteRole, "Delete role", "delete", IsEnabled: true, DisabledReason: null)
                ],
                lastReceipt);
        }

        private static ProcessDefinitionRoleDraftProjection CreateRoleDraft()
            => new(
                new ProcessDefinitionRoleKey("solution-architect"),
                "Solution architect",
                "Own architecture decisions and technical tradeoffs.",
                "Assign a senior architecture owner before launch planning.",
                ProcessDefinitionRoleExecutorKind.PersonOrAgent,
                new ProcessDefinitionWorkflowPreferenceProjection(
                    ProcessDefinitionRoleWorkflowPreferenceKind.SpecificWorkflow,
                    WorkflowDefinitionId: null,
                    WorkflowVersionId: null,
                    "No workflow selected"),
                ProcessDefinitionRoleProjectAssignmentKind.Architect,
                IsRequired: true,
                AllowsFallback: true,
                RequiresExplicitApproval: true,
                DefaultAllocationPercent: 60,
                "process-role-template/solution-architect",
                "Solution architect v1",
                "Architecture role template snapshot.",
                ProcessDefinitionRoleTemplateOverrideStatus.AppliedFromTemplate,
                "Resolved from process-role-template/solution-architect.");

        private static ProcessDefinitionStepEditorProjection CreateStepEditor(ProcessDefinitionCatalogItemKey key)
            => CreateStepEditor(
                key,
                CreateStepDraft(),
                new ProcessDefinitionStepEditorVersionToken($"template:{key.Value}:steps"),
                new ProcessDefinitionStepLintProjection([]),
                lastReceipt: null,
                commandKind: null);

        private static ProcessDefinitionStepEditorProjection CreateStepEditor(
            ProcessDefinitionCatalogItemKey key,
            ProcessDefinitionStepDraftProjection draft,
            ProcessDefinitionStepEditorVersionToken versionToken,
            ProcessDefinitionStepLintProjection lint,
            ProcessDefinitionStepCommandReceipt? lastReceipt,
            ProcessDefinitionStepCommandKind? commandKind)
        {
            var projectedDraft = commandKind switch
            {
                ProcessDefinitionStepCommandKind.AddBranchOutcome => draft with
                {
                    BranchOutcomes =
                    [
                        .. draft.BranchOutcomes,
                        new ProcessDefinitionBranchOutcomeProjection(
                            new ProcessDefinitionBranchOutcomeKey("architecture-decision-route-2"),
                            "Route 2",
                            "Second typed route.",
                            new ProcessDefinitionRouteTargetProjection(
                                ProcessDefinitionRouteTargetKind.NextStep,
                                StepKey: null,
                                ArtifactExpectationKey: null,
                                "Next step"),
                            IsBackwardRoute: false,
                            new ProcessDefinitionLoopBudgetProjection(
                                IsRequired: false,
                                MaximumRepeats: 0,
                                FingerprintPolicyKey: string.Empty,
                                ProcessDefinitionRouteTargetKind.Escalate))
                    ]
                },
                ProcessDefinitionStepCommandKind.AddArtifactExpectation => draft with
                {
                    ArtifactExpectations =
                    [
                        .. draft.ArtifactExpectations,
                        new ProcessDefinitionArtifactExpectationProjection(
                            new ProcessDefinitionArtifactExpectationKey("architecture-decision-evidence"),
                            "architecture-decision-evidence",
                            "Architecture decision evidence",
                            ProcessDefinitionArtifactKind.Evidence,
                            IsRequired: true,
                            ProcessDefinitionArtifactTrustRequirement.ReviewRequired,
                            ProcessDefinitionArtifactSensitivityLevel.Internal,
                            RetentionDays: 365,
                            WorkflowOutputId: string.Empty,
                            WorkflowOutputName: string.Empty,
                            ProcessDefinitionWorkflowOutputKind.Unspecified,
                            SubprocessChildArtifactExpectationId: null,
                            SubprocessChildStepKey: string.Empty,
                            SubprocessChildArtifactTitle: string.Empty,
                            AllowedFutureUsageSummary: "Reusable for route replay.",
                            ValidationRequirementSummary: "Must identify evidence source.")
                    ]
                },
                _ => draft
            };

            return new ProcessDefinitionStepEditorProjection(
                key,
                versionToken,
                projectedDraft.Basic.StepKey,
                [
                    new ProcessDefinitionStepListItemProjection(
                        projectedDraft.Basic.StepKey,
                        projectedDraft.Basic.Title,
                        projectedDraft.Basic.Subtitle,
                        projectedDraft.Basic.StepKind,
                        Order: 0,
                        IsSelected: true)
                ],
                [projectedDraft],
                projectedDraft,
                [
                    new ProcessDefinitionSubprocessOptionProjection(
                        new ProcessDefinitionCatalogItemKey("delivery-default"),
                        "Delivery default",
                        "Default delivery subprocess.")
                ],
                [
                    new(ProcessDefinitionStepCommandKind.SaveStep, "Save step", "save", IsEnabled: true, DisabledReason: null),
                    new(ProcessDefinitionStepCommandKind.AddBranchOutcome, "Add route", "alt_route", IsEnabled: true, DisabledReason: null),
                    new(ProcessDefinitionStepCommandKind.AddArtifactExpectation, "Add artifact", "inventory_2", IsEnabled: true, DisabledReason: null),
                    new(ProcessDefinitionStepCommandKind.MapSubprocess, "Map subprocess", "account_tree", IsEnabled: true, DisabledReason: null)
                ],
                lint,
                lastReceipt);
        }

        private static ProcessTemplateCatalogProjection CreateTemplateCatalog(
            ProcessDefinitionCatalogItemKey definitionKey,
            ProcessTemplateCatalogQueryProjection query,
            ProcessTemplateImportCommandReceipt? lastReceipt,
            IReadOnlyList<ProcessTemplateImportedComponentProjection> importedComponents)
        {
            var allItems = new[]
            {
                new ProcessTemplateCatalogItemProjection(
                    new ProcessTemplateCatalogItemKey("process:blazor-app-delivery"),
                    ProcessTemplateCatalogItemKind.Process,
                    "Blazor app delivery",
                    "Build and prove a Blazor application.",
                    "blazor-app-delivery",
                    "blazor-app-delivery",
                    "Process",
                    [new("Source", "blazor-app-delivery")],
                    IsSelected: false),
                new ProcessTemplateCatalogItemProjection(
                    new ProcessTemplateCatalogItemKey("role:blazor-app-delivery:solution-architect"),
                    ProcessTemplateCatalogItemKind.Role,
                    "Solution architect",
                    "Owns architecture decisions and technical tradeoffs.",
                    "blazor-app-delivery",
                    "solution-architect",
                    "Role",
                    [new("Executor", "person-or-agent")],
                    IsSelected: false),
                new ProcessTemplateCatalogItemProjection(
                    new ProcessTemplateCatalogItemKey("artifact:blazor-app-delivery:architecture-decision:architecture-decision-record"),
                    ProcessTemplateCatalogItemKind.Artifact,
                    "Architecture decision record",
                    "Must include selected option and rationale.",
                    "blazor-app-delivery",
                    "architecture-decision-record",
                    "Artifact",
                    [new("Artifact", "Deliverable")],
                    IsSelected: false)
            };
            var categoryFiltered = query.Category switch
            {
                ProcessTemplateCatalogCategoryKind.Processes => allItems.Where(item => item.Kind == ProcessTemplateCatalogItemKind.Process),
                ProcessTemplateCatalogCategoryKind.Roles => allItems.Where(item => item.Kind == ProcessTemplateCatalogItemKind.Role),
                ProcessTemplateCatalogCategoryKind.Artifacts => allItems.Where(item => item.Kind == ProcessTemplateCatalogItemKind.Artifact),
                _ => allItems
            };
            var filtered = string.IsNullOrWhiteSpace(query.SearchText)
                ? categoryFiltered.ToArray()
                : categoryFiltered
                    .Where(item => item.Title.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase) ||
                                   item.SourceComponentKey.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            var selected = query.SelectedItemKey is { } selectedKey
                ? filtered.FirstOrDefault(item => item.Key == selectedKey)
                : filtered.FirstOrDefault();
            var selectedQuery = query with { SelectedItemKey = selected?.Key ?? query.SelectedItemKey };
            var importedKeys = importedComponents.Select(component => component.ItemKey).ToHashSet();
            var projectedItems = filtered
                .Select(item => item with
                {
                    IsSelected = selected?.Key == item.Key,
                    Facts = importedKeys.Contains(item.Key)
                        ? [.. item.Facts, new ProcessTemplateCatalogFactProjection("Import", "Imported")]
                        : item.Facts
                })
                .ToArray();
            var preview = selected is null
                ? null
                : new ProcessTemplateCatalogPreviewProjection(
                    selected.Key,
                    selected.Kind,
                    selected.Title,
                    selected.Summary,
                    "processes/blazor-app-delivery/definition.json",
                    "sha256:test-template-hash",
                    "Generated projections are derived from canonical JSON.",
                    "# Blazor app delivery\n\nGenerated from canonical JSON process template `blazor-app-delivery`.",
                    "flowchart TD\n    process[\"Blazor app delivery\"]\n    step[\"Architecture decision\"]\n    process --> step",
                    "{\"key\":\"blazor-app-delivery\",\"displayName\":\"Blazor app delivery\"}",
                    [
                        new("process:blazor-app-delivery", ParentNodeKey: null, ProcessTemplateStructureNodeKind.Process, "Blazor app delivery", "Build and prove a Blazor application.", Depth: 0),
                        new("process:blazor-app-delivery:steps", "process:blazor-app-delivery", ProcessTemplateStructureNodeKind.Section, "Steps", "1 step", Depth: 1),
                        new("process:blazor-app-delivery:steps:architecture-decision", "process:blazor-app-delivery:steps", ProcessTemplateStructureNodeKind.Step, "Architecture decision", "Governed review step.", Depth: 2)
                    ],
                    [
                        new(
                            new ProcessTemplateCatalogItemKey("role:blazor-app-delivery:solution-architect"),
                            ProcessTemplateCatalogItemKind.Role,
                            "Solution architect",
                            "Owns architecture decisions and technical tradeoffs.",
                            "blazor-app-delivery",
                            "solution-architect",
                            importedKeys.Contains(new ProcessTemplateCatalogItemKey("role:blazor-app-delivery:solution-architect"))),
                        new(
                            new ProcessTemplateCatalogItemKey("artifact:blazor-app-delivery:architecture-decision:architecture-decision-record"),
                            ProcessTemplateCatalogItemKind.Artifact,
                            "Architecture decision record",
                            "Must include selected option and rationale.",
                            "blazor-app-delivery",
                            "architecture-decision-record",
                            importedKeys.Contains(new ProcessTemplateCatalogItemKey("artifact:blazor-app-delivery:architecture-decision:architecture-decision-record")))
                    ]);

            return new ProcessTemplateCatalogProjection(
                definitionKey,
                lastReceipt?.VersionToken ?? new ProcessTemplateCatalogVersionToken("templates:test:0"),
                selectedQuery,
                string.IsNullOrWhiteSpace(query.SearchText)
                    ? "3 template catalog item(s) from pack test-pack."
                    : $"{filtered.Length} template catalog item(s) match '{query.SearchText}'.",
                "test-pack",
                "Template catalog is projected from canonical JSON.",
                [
                    new(ProcessTemplateCatalogCategoryKind.All, "All", "All template items.", allItems.Length, query.Category == ProcessTemplateCatalogCategoryKind.All),
                    new(ProcessTemplateCatalogCategoryKind.Processes, "Processes", "Process templates.", 1, query.Category == ProcessTemplateCatalogCategoryKind.Processes),
                    new(ProcessTemplateCatalogCategoryKind.Roles, "Roles", "Role components.", 1, query.Category == ProcessTemplateCatalogCategoryKind.Roles),
                    new(ProcessTemplateCatalogCategoryKind.Artifacts, "Artifacts", "Artifact components.", 1, query.Category == ProcessTemplateCatalogCategoryKind.Artifacts)
                ],
                projectedItems,
                selected,
                preview,
                [
                    new(
                        new ProcessDefinitionStepKey("architecture-decision"),
                        "Architecture decision",
                        "Governed review step",
                        IsDefaultTarget: true)
                ],
                [
                    new(ProcessTemplateImportCommandKind.ImportProcess, "Import process", "account_tree", selected?.Kind == ProcessTemplateCatalogItemKind.Process, null),
                    new(ProcessTemplateImportCommandKind.ImportRole, "Import role", "badge", selected?.Kind == ProcessTemplateCatalogItemKind.Role, null),
                    new(ProcessTemplateImportCommandKind.ImportArtifact, "Import artifact", "inventory_2", selected?.Kind == ProcessTemplateCatalogItemKind.Artifact, null)
                ],
                importedComponents,
                lastReceipt);
        }

        private static ProcessDefinitionStepDraftProjection CreateStepDraft()
            => new(
                new ProcessDefinitionStepBasicDraftProjection(
                    new ProcessDefinitionStepKey("architecture-decision"),
                    "Architecture decision",
                    "Governed review step",
                    "Choose an architecture route from typed outcomes.",
                    ProcessDefinitionStepKind.Decision,
                    TargetLeadHours: 12,
                    AllowsManualSkip: false,
                    AllowsSafeRefusal: true,
                    RequiresApproval: true,
                    RequiresDecisionRecord: true,
                    new ProcessDefinitionRoleKey("solution-architect")),
                new ProcessDefinitionStepOperationContractProjection(
                    ProcessDefinitionStepTargetScopeKind.ExternalArtifactDestination,
                    [
                        ProcessDefinitionStepOperationKind.ReadProcessContext,
                        ProcessDefinitionStepOperationKind.WriteExternalArtifactDestination
                    ]),
                new ProcessDefinitionStepContractsProjection(
                    "Architecture concern and project context.",
                    "Architecture decision record.",
                    "Decision evidence and route rationale.",
                    "Solution architect decides the route.",
                    "Escalate when evidence is contradictory."),
                [
                    new ProcessDefinitionBranchOutcomeProjection(
                        new ProcessDefinitionBranchOutcomeKey("approved"),
                        "Approved",
                        "Route to the approved implementation lane.",
                        new ProcessDefinitionRouteTargetProjection(
                            ProcessDefinitionRouteTargetKind.NextStep,
                            StepKey: null,
                            ArtifactExpectationKey: null,
                            "Next step"),
                        IsBackwardRoute: false,
                        new ProcessDefinitionLoopBudgetProjection(
                            IsRequired: false,
                            MaximumRepeats: 0,
                            FingerprintPolicyKey: string.Empty,
                            ProcessDefinitionRouteTargetKind.Escalate))
                ],
                [
                    new ProcessDefinitionStepRoleBindingProjection(
                        new ProcessDefinitionStepKey("architecture-decision"),
                        "Architecture decision",
                        new ProcessDefinitionRoleKey("solution-architect"),
                        "Solution architect",
                        ProcessStepRoleResponsibilityKind.Approver,
                        IsRequired: true,
                        FallbackOrder: 1,
                        "Rebind to the architecture board when unavailable.")
                ],
                [
                    new ProcessDefinitionArtifactExpectationProjection(
                        new ProcessDefinitionArtifactExpectationKey("architecture-decision-record"),
                        "architecture-decision-record",
                        "Architecture decision record",
                        ProcessDefinitionArtifactKind.Deliverable,
                        IsRequired: true,
                        ProcessDefinitionArtifactTrustRequirement.ReviewRequired,
                        ProcessDefinitionArtifactSensitivityLevel.Internal,
                        RetentionDays: 365,
                        WorkflowOutputId: "adr-output",
                        WorkflowOutputName: "Architecture decision record",
                        ProcessDefinitionWorkflowOutputKind.Artifact,
                        SubprocessChildArtifactExpectationId: null,
                        SubprocessChildStepKey: string.Empty,
                        SubprocessChildArtifactTitle: string.Empty,
                        AllowedFutureUsageSummary: "Reusable for implementation planning.",
                        ValidationRequirementSummary: "Must include selected option and rationale.")
                ],
                new ProcessDefinitionSubprocessMappingProjection(
                    ProcessKey: string.Empty,
                    DefinitionSnapshotName: string.Empty,
                    ChildArtifactMappings: []));

        private static ProcessDefinitionCanvasEditorProjection CreateCanvas(
            ProcessDefinitionCatalogItemKey key,
            ProcessDefinitionCanvasVersionToken? versionToken = null,
            ProcessDefinitionCanvasCommandReceipt? receipt = null,
            ProcessDefinitionCanvasCommandKind? commandKind = null)
        {
            var stepKey = new ProcessDefinitionCanvasNodeKey("step:architecture-decision");
            var branchKey = new ProcessDefinitionCanvasNodeKey("branch:architecture-decision");
            var roleKey = new ProcessDefinitionCanvasNodeKey("role:solution-architect");
            var artifactKey = new ProcessDefinitionCanvasNodeKey("artifact:architecture-decision:adr");
            var nodes = new[]
            {
                CreateCanvasNode(
                    stepKey,
                    ProcessDefinitionCanvasNodeKind.Step,
                    commandKind == ProcessDefinitionCanvasCommandKind.AddStep ? "Implementation" : "Architecture decision",
                    "Governed review step",
                    "Select the architecture decision step without losing editor context.",
                    160,
                    220,
                    "info",
                    new ProcessDefinitionStepKey("architecture-decision"),
                    RoleKey: null,
                    ArtifactKey: null,
                    ["Step"]),
                CreateCanvasNode(
                    branchKey,
                    ProcessDefinitionCanvasNodeKind.BranchRouter,
                    "Architecture decision routes",
                    "Typed branch router",
                    "Route labels are display text; the route target stays typed.",
                    420,
                    110,
                    "warning",
                    new ProcessDefinitionStepKey("architecture-decision"),
                    RoleKey: null,
                    ArtifactKey: null,
                    ["Branch"]),
                CreateCanvasNode(
                    roleKey,
                    ProcessDefinitionCanvasNodeKind.Role,
                    "Solution architect",
                    "person-or-agent",
                    "Architecture authority for the selected step.",
                    160,
                    40,
                    "success",
                    StepKey: null,
                    RoleKey: new ProcessDefinitionRoleKey("solution-architect"),
                    ArtifactKey: null,
                    ["Required"]),
                CreateCanvasNode(
                    artifactKey,
                    ProcessDefinitionCanvasNodeKind.Artifact,
                    "Architecture decision record",
                    "Deliverable",
                    "Required evidence for the selected step.",
                    160,
                    370,
                    "accent",
                    new ProcessDefinitionStepKey("architecture-decision"),
                    RoleKey: null,
                    ArtifactKey: "architecture-decision-record",
                    ["Artifact"])
            };
            var edges = new[]
            {
                new ProcessDefinitionCanvasEdgeProjection(
                    new ProcessDefinitionCanvasEdgeKey("branch-route:architecture-decision:router"),
                    ProcessDefinitionCanvasEdgeKind.BranchRoute,
                    stepKey,
                    branchKey,
                    "approved",
                    "Typed route from architecture decision to the approved lane.",
                    "warning",
                    IsBackwardRoute: false),
                new ProcessDefinitionCanvasEdgeProjection(
                    new ProcessDefinitionCanvasEdgeKey("role-binding:solution-architect:architecture-decision"),
                    ProcessDefinitionCanvasEdgeKind.RoleBinding,
                    roleKey,
                    stepKey,
                    "Approver",
                    "Solution architect approves the architecture decision.",
                    "success",
                    IsBackwardRoute: false),
                new ProcessDefinitionCanvasEdgeProjection(
                    new ProcessDefinitionCanvasEdgeKey("artifact:architecture-decision:adr"),
                    ProcessDefinitionCanvasEdgeKind.ArtifactExpectation,
                    stepKey,
                    artifactKey,
                    "evidence",
                    "Architecture decision record is required evidence.",
                    "accent",
                    IsBackwardRoute: false)
            };

            return new ProcessDefinitionCanvasEditorProjection(
                key,
                versionToken ?? new ProcessDefinitionCanvasVersionToken($"template:{key.Value}:canvas"),
                new ProcessDefinitionCanvasViewportProjection(960, 560, "Test canvas bounds."),
                nodes,
                edges,
                [
                    new ProcessDefinitionCanvasToolboxActionProjection(
                        new ProcessDefinitionCanvasToolboxActionKey("process-step.implementation"),
                        ProcessDefinitionCanvasToolboxActionKind.Step,
                        "Implementation",
                        "Add an implementation step.",
                        "add",
                        IsEnabled: true,
                        DisabledReason: null),
                    new ProcessDefinitionCanvasToolboxActionProjection(
                        new ProcessDefinitionCanvasToolboxActionKey("process-step.decision"),
                        ProcessDefinitionCanvasToolboxActionKind.BranchRouter,
                        "Decision router",
                        "Add a typed branch router to the selected step.",
                        "alt_route",
                        IsEnabled: true,
                        DisabledReason: null),
                    new ProcessDefinitionCanvasToolboxActionProjection(
                        new ProcessDefinitionCanvasToolboxActionKey("process-canvas.add-role-binding"),
                        ProcessDefinitionCanvasToolboxActionKind.RoleBinding,
                        "Role binding",
                        "Connect the selected step to a role.",
                        "badge",
                        IsEnabled: true,
                        DisabledReason: null),
                    new ProcessDefinitionCanvasToolboxActionProjection(
                        new ProcessDefinitionCanvasToolboxActionKey("process-canvas.add-artifact-expectation"),
                        ProcessDefinitionCanvasToolboxActionKind.ArtifactExpectation,
                        "Artifact expectation",
                        "Attach required evidence to the selected step.",
                        "inventory_2",
                        IsEnabled: true,
                        DisabledReason: null)
                ],
                new ProcessDefinitionCanvasSelectionProjection(
                    ProcessDefinitionCanvasSelectionKind.Step,
                    stepKey,
                    EdgeKey: null,
                    "Architecture decision",
                    "Select the architecture decision step without losing editor context.",
                    "architecture-decision",
                    ["Step"]),
                [
                    new ProcessDefinitionCanvasCommandProjection(
                        ProcessDefinitionCanvasCommandKind.Recompose,
                        "Recompose",
                        "auto_fix_high",
                        IsEnabled: true,
                        DisabledReason: null)
                ],
                receipt);
        }

        private static ProcessDefinitionCanvasEditorNodeProjection CreateCanvasNode(
            ProcessDefinitionCanvasNodeKey nodeKey,
            ProcessDefinitionCanvasNodeKind kind,
            string title,
            string subtitle,
            string summary,
            double x,
            double y,
            string tone,
            ProcessDefinitionStepKey? StepKey,
            ProcessDefinitionRoleKey? RoleKey,
            string? ArtifactKey,
            IReadOnlyList<string> badges)
            => new(
                nodeKey,
                kind,
                title,
                subtitle,
                summary,
                x,
                y,
                Width: kind == ProcessDefinitionCanvasNodeKind.BranchRouter ? 168 : 220,
                Height: kind == ProcessDefinitionCanvasNodeKind.Artifact ? 72 : 92,
                tone,
                StepKey,
                RoleKey,
                ArtifactKey,
                badges,
                CreateCanvasPorts(kind));

        private static IReadOnlyList<ProcessDefinitionCanvasPortProjection> CreateCanvasPorts(
            ProcessDefinitionCanvasNodeKind kind)
            => kind switch
            {
                ProcessDefinitionCanvasNodeKind.Step =>
                [
                    new("in", ProcessDefinitionCanvasPortKind.StructuralInput, "Input", 0, 46),
                    new("out", ProcessDefinitionCanvasPortKind.StructuralOutput, "Output", 220, 46),
                    new("role", ProcessDefinitionCanvasPortKind.RoleBinding, "Role", 110, 0),
                    new("artifact", ProcessDefinitionCanvasPortKind.ArtifactExpectation, "Artifact", 110, 92)
                ],
                ProcessDefinitionCanvasNodeKind.BranchRouter =>
                [
                    new("in", ProcessDefinitionCanvasPortKind.StructuralInput, "Decision input", 0, 46),
                    new("out", ProcessDefinitionCanvasPortKind.BranchOutcome, "Outcome", 168, 46)
                ],
                ProcessDefinitionCanvasNodeKind.Role =>
                [
                    new("role-out", ProcessDefinitionCanvasPortKind.RoleBinding, "Responsibility", 220, 46)
                ],
                ProcessDefinitionCanvasNodeKind.Artifact =>
                [
                    new("artifact-in", ProcessDefinitionCanvasPortKind.ArtifactExpectation, "Expectation", 0, 36)
                ],
                ProcessDefinitionCanvasNodeKind.SubprocessBoundary =>
                [
                    new("subprocess-in", ProcessDefinitionCanvasPortKind.SubprocessBoundary, "Child process", 0, 46)
                ],
                _ => []
            };

        private static ProcessDefinitionEditorLintProjection CreateEditorLint(
            ProcessDefinitionEditorCommand command)
        {
            if (!string.IsNullOrWhiteSpace(command.Draft.Identity.Name))
            {
                return new ProcessDefinitionEditorLintProjection([]);
            }

            return new ProcessDefinitionEditorLintProjection(
            [
                new ProcessDefinitionEditorLintIssueProjection(
                    "processes.definition.identity.name-required",
                    ProcessDefinitionEditorLintSeverity.Error,
                    ProcessDefinitionEditorLintSection.Identity,
                    "Definition name is required.",
                    "Enter a stable, user-facing definition name.")
            ]);
        }

        private static ProcessDefinitionRoleLintProjection CreateRoleLint(
            ProcessDefinitionRoleEditorCommand command)
        {
            if (!string.IsNullOrWhiteSpace(command.Draft.DisplayName) &&
                command.Draft.PreferredExecutorKind != ProcessDefinitionRoleExecutorKind.Unspecified &&
                command.Draft.DefaultAllocationPercent is >= 0 and <= 100)
            {
                return new ProcessDefinitionRoleLintProjection([]);
            }

            return new ProcessDefinitionRoleLintProjection(
            [
                new ProcessDefinitionRoleLintIssueProjection(
                    "processes.definition.role.execution.invalid",
                    ProcessDefinitionRoleLintSeverity.Error,
                    ProcessDefinitionRoleLintSection.Execution,
                "Role execution fields are invalid.",
                "Choose a typed executor kind and bounded allocation.")
            ]);
        }

        private static ProcessDefinitionStepLintProjection CreateStepLint(
            ProcessDefinitionStepEditorCommand command)
        {
            if (!string.IsNullOrWhiteSpace(command.Draft.Basic.Title) &&
                command.Draft.OperationContract.TargetScope != ProcessDefinitionStepTargetScopeKind.Unspecified)
            {
                return new ProcessDefinitionStepLintProjection([]);
            }

            return new ProcessDefinitionStepLintProjection(
            [
                new ProcessDefinitionStepLintIssueProjection(
                    "processes.definition.step.invalid",
                    ProcessDefinitionStepLintSeverity.Error,
                    ProcessDefinitionStepLintSection.Basic,
                    "Step fields are invalid.",
                    "Enter a title and choose an explicit operation target scope.")
            ]);
        }

        private static IReadOnlyList<ProcessWorkspaceTabProjection> CreateTabs()
            =>
            [
                new(ProcessWorkspaceTabKey.Definitions, "Definitions", "account_tree", "Definition catalog.", "2", IsEnabled: true),
                new(ProcessWorkspaceTabKey.LaunchPlans, "Launch plans", "rocket_launch", "Launch plans.", "0", IsEnabled: true),
                new(ProcessWorkspaceTabKey.LiveRuns, "Live runs", "monitor_heart", "Live runs.", "0", IsEnabled: true),
                new(ProcessWorkspaceTabKey.History, "History", "history", "History.", "0", IsEnabled: true)
            ];

        private static IReadOnlyList<ProcessWorkspaceCommandProjection> CreateCommands()
            =>
            [
                new(ProcessWorkspaceCommandKind.RefreshProjections, "Refresh", "refresh", IsEnabled: true, DisabledReason: null),
                new(ProcessWorkspaceCommandKind.OpenAgentContext, "Agent context", "smart_toy", IsEnabled: true, DisabledReason: null),
                new(ProcessWorkspaceCommandKind.CreateDefinition, "New definition", "add", IsEnabled: false, "Definition editing is not available in this workspace shell."),
                new(ProcessWorkspaceCommandKind.FeedDefaults, "Feed defaults", "download", IsEnabled: true, DisabledReason: null),
                new(ProcessWorkspaceCommandKind.LaunchRun, "Launch", "rocket_launch", IsEnabled: false, "Runtime launch commands are not available in this workspace shell."),
                new(ProcessWorkspaceCommandKind.OpenLiveDashboard, "Live dashboard", "open_in_new", IsEnabled: true, DisabledReason: null)
            ];

        private static ProcessWorkspaceAgentEntryProjection CreateAgentEntry(ProcessWorkspaceShellRequest request)
        {
            if (request.Selection.RunId is { } runId)
            {
                return new ProcessWorkspaceAgentEntryProjection(
                    ProcessWorkspaceAgentEntryKind.RunContext,
                    IsAvailable: true,
                    "Open run agent context",
                    $"processes:workspace:run:{runId:N}",
                    DisabledReason: null);
            }

            return new ProcessWorkspaceAgentEntryProjection(
                ProcessWorkspaceAgentEntryKind.WorkspaceContext,
                IsAvailable: true,
                "Open process agent context",
                "processes:workspace",
                DisabledReason: null);
        }
    }

    private sealed class FixedProcessProjectionClock(DateTimeOffset utcNow) : IProcessProjectionClock
    {
        public DateTimeOffset GetUtcNow() => utcNow;
    }
}
