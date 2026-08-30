using Bunit;
using AngleSharp.Dom;
using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.AgentFramework.Workflows.Templates;
using CanDoItAll.AppComponents;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Tools.Documents;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WorkflowFailureDiagnosticEnvelope = CanDoItAll.AgentFramework.Workflows.Abstractions.WorkflowFailureDiagnosticEnvelope;
using WorkflowFailureKind = CanDoItAll.AgentFramework.Workflows.Abstractions.WorkflowFailureKind;
using WorkflowFailureRetryability = CanDoItAll.AgentFramework.Workflows.Abstractions.WorkflowFailureRetryability;
using WorkflowFailureSourceContext = CanDoItAll.AgentFramework.Workflows.Abstractions.WorkflowFailureSourceContext;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class WorkflowsPageTests
{
    [Fact]
    public async Task Workflows_page_opens_exact_workflow_curator_only_after_context_is_ready()
    {
        var launcher = new RecordingWorkflowCuratorChatLauncher();
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-curator-launch-tests");
        await using var harness = await CreateInMemoryWorkflowHarnessAsync(environment, services =>
        {
            services.RemoveAll<IWorkflowCatalogService>();
            services.AddScoped<BlockingInitialWorkflowCatalogService>();
            services.AddScoped<IWorkflowCatalogService>(serviceProvider =>
                serviceProvider.GetRequiredService<BlockingInitialWorkflowCatalogService>());
            services.RemoveAll<IAgentChatLauncher>();
            services.AddSingleton<IAgentChatLauncher>(launcher);
        });
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var catalog = harness.Context.Services.GetRequiredService<BlockingInitialWorkflowCatalogService>();
        await CreateHistoryDefinitionAsync(catalog);

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.Render<WorkflowsPage>();
        await catalog.WaitForInitialListRequestAsync();

        var openButton = cut.Find("[data-testid='workflows-curator-open']");
        Assert.True(openButton.HasAttribute("disabled"));
        Assert.Equal("Open Workflow Curator Agent", openButton.GetAttribute("aria-label"));
        Assert.True(string.IsNullOrWhiteSpace(openButton.TextContent));
        Assert.EndsWith(
            "/avatar-04.jpg",
            Assert.IsAssignableFrom<IElement>(openButton.QuerySelector("img")).GetAttribute("src"),
            StringComparison.Ordinal);

        Assert.Equal(AgentChatContextAccessState.Loading, ReadAgentChatAccessState(cut.Instance));
        openButton.Click();
        Assert.Empty(launcher.StartedAgentIds);

        catalog.CompleteInitialListRequest();
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(AgentChatContextAccessState.Ready, ReadAgentChatAccessState(cut.Instance));
            Assert.False(cut.Find("[data-testid='workflows-curator-open']").HasAttribute("disabled"));
        });

        var avatarAction = Assert.Single(cut.FindComponents<AgentAvatarActionButton>());
        Assert.Equal("Open Workflow Curator Agent", avatarAction.Instance.Label);
        var tooltipTarget = Assert.Single(avatarAction.FindComponents<TooltipTarget>());
        Assert.Equal(TooltipPosition.Bottom, tooltipTarget.Instance.Position);
        Assert.Equal("workflows-curator-tooltip", tooltipTarget.Instance.TestId);

        var surface = ReadAgentChatSurface(cut.Instance);
        var curatorAccess = Assert.Single(surface.AgentAccess);
        Assert.Equal(WorkflowCuratorAgentIdentity.AgentId, curatorAccess.AgentId);
        Assert.Equal(
            AgentChatContextPermission.Read | AgentChatContextPermission.Mutate,
            curatorAccess.Permissions);
        Assert.Equal(
            AgentChatContextCompletionRefreshMode.OnSuccessfulRun,
            surface.CompletionRefreshMode);

        launcher.HoldStartOpen = true;
        var firstOpenTask = cut.Find("[data-testid='workflows-curator-open']")
            .TriggerEventAsync("onclick", new MouseEventArgs());
        await launcher.WaitForStartAsync();
        var secondOpenTask = cut.Find("[data-testid='workflows-curator-open']")
            .TriggerEventAsync("onclick", new MouseEventArgs());

        await secondOpenTask.WaitAsync(TimeSpan.FromSeconds(1));
        var startedAgentId = Assert.Single(launcher.StartedAgentIds);
        Assert.Equal(WorkflowCuratorAgentIdentity.AgentId, startedAgentId);

        launcher.ReleaseStart();
        await firstOpenTask;
        Assert.Single(launcher.StartedAgentIds);

        cut.Find("[data-testid='workflows-tab-workflows']").Click();
        Assert.Single(cut.FindAll("[data-testid='workflows-curator-open']"));
    }

    [Fact]
    public async Task Workflows_page_remains_available_and_retries_when_curator_is_missing()
    {
        var workspaceService = DispatchProxy.Create<
            IAgentFrameworkWorkspaceService,
            MissingWorkflowCuratorWorkspaceProxy>();
        var workspace = (MissingWorkflowCuratorWorkspaceProxy)(object)workspaceService;
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-curator-missing-tests");
        await using var harness = await CreateInMemoryWorkflowHarnessAsync(environment, services =>
        {
            services.RemoveAll<IAgentFrameworkWorkspaceService>();
            services.AddSingleton(workspaceService);
        });
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.Render<WorkflowsPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[data-testid='workflows-loading']"));
            Assert.Empty(cut.FindAll("[data-testid='workflows-error']"));
            Assert.True(cut.Find("[data-testid='workflows-curator-open']").HasAttribute("disabled"));
            Assert.Contains(
                harness.Context.Services.GetRequiredService<NotificationService>().Messages,
                notification => notification.Summary == "Workflow Curator unavailable");
        }, TimeSpan.FromSeconds(10));

        Assert.Equal(1, workspace.ListAgentsCallCount);
        cut.Find("[data-testid='workflows-refresh']").Click();
        cut.WaitForAssertion(
            () => Assert.Equal(2, workspace.ListAgentsCallCount),
            TimeSpan.FromSeconds(10));
        Assert.Empty(cut.FindAll("[data-testid='workflows-error']"));
    }

    [Fact]
    public async Task Workflows_page_creates_starter_workflow_and_runs_preview()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(RegisterDeterministicWorkflowLlmInvoker);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var notificationService = harness.Context.Services.GetRequiredService<NotificationService>();
        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var componentLibrary = harness.Context.Services.GetRequiredService<IWorkflowComponentLibraryService>();
        var runStore = harness.Context.Services.GetRequiredService<IWorkflowRunStore>();

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.Render<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-create-starter']");
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("disabled", cut.Find("[data-testid='workflows-create-starter']").OuterHtml, StringComparison.OrdinalIgnoreCase);
        });

        await cut.InvokeAsync(() => cut.Find("[data-testid='workflows-create-starter']").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(notificationService.Messages, message => message.Summary == "Workflow created");
        });
        Assert.Single(await catalogService.ListDefinitionsAsync());
        var component = Assert.Single(await componentLibrary.ListComponentsAsync());
        var defaultProvider = (await componentLibrary.ListProviderOptionsAsync()).FirstOrDefault(option => option.IsEnabled);
        if (defaultProvider is not null)
        {
            Assert.Equal(defaultProvider.ProviderProfileId, component.ProviderProfileId);
            var expectedModel = string.IsNullOrWhiteSpace(defaultProvider.DefaultModel)
                ? defaultProvider.ModelOptions.FirstOrDefault() ?? "gpt-5.4"
                : defaultProvider.DefaultModel;
            Assert.Equal(expectedModel, component.Model);
        }

        var workflowsTab = cut.Find("[data-testid='workflows-tab-workflows']");
        Assert.Contains("Workflows", workflowsTab.TextContent);
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflows-tab-workflows']").Click());
        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll("[data-testid='workflows-catalog-item']"));
        });

        await cut.InvokeAsync(() => cut.Find("[data-testid='workflows-tab-history']").Click());
        cut.WaitForElement("[data-testid='workflows-run-test']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflows-run-test']").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(notificationService.Messages, message => message.Summary == "Workflow test completed");
            Assert.Contains("Succeeded", cut.Find("[data-testid='workflows-test-result']").TextContent);
            Assert.NotEmpty(cut.FindAll("[data-testid='workflows-run-event']"));
        });
        Assert.Single(await runStore.ListRunsAsync());
    }

    [Fact]
    public async Task Workflows_page_publishes_a_valid_draft_and_refreshes_its_status()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var notificationService = harness.Context.Services.GetRequiredService<NotificationService>();
        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.Render<WorkflowsPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.False(cut.Find("[data-testid='workflows-create-starter']").HasAttribute("disabled"));
        });
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflows-create-starter']").Click());
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(notificationService.Messages, message => message.Summary == "Workflow created");
        });

        await cut.InvokeAsync(() => cut.Find("[data-testid='workflows-tab-workflows']").Click());
        cut.WaitForElement("[data-testid='workflows-publish']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(notificationService.Messages, message => message.Summary == "Workflow published");
            Assert.Contains("Status Active", cut.Find("[data-testid='workflows-detail']").TextContent, StringComparison.Ordinal);
            Assert.Empty(cut.FindAll("[data-testid='workflows-publish']"));
        }, TimeSpan.FromSeconds(30));

        var published = Assert.Single(await catalogService.ListDefinitionsAsync());
        Assert.Equal(WorkflowLifecycleStatus.Active, published.Status);
    }

    [Fact]
    public async Task Workflows_page_defers_component_library_until_component_sections_need_it()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(RegisterCountingWorkflowComponentLibrary);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var counter = harness.Context.Services.GetRequiredService<WorkflowComponentLibraryCallCounter>();

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.Render<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-create-starter']");
        cut.WaitForElement("[data-testid='workflows-tabs']");

        Assert.Empty(cut.FindAll("[data-testid='workflows-tab-templates']"));
        Assert.Equal(0, counter.ListComponentsCount);
        Assert.Equal(0, counter.ListProviderOptionsCount);

        cut.Find("[data-testid='workflows-tab-workflows']").Click();

        cut.WaitForElement("[data-testid='workflows-catalog']");
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(0, counter.ListComponentsCount);
            Assert.Equal(0, counter.ListProviderOptionsCount);
        });

        cut.Find("[data-testid='workflows-open-template-catalogue']").Click();

        cut.WaitForElement("[data-testid='workflows-template-catalogue-dialog']");
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(0, counter.ListComponentsCount);
            Assert.Equal(0, counter.ListProviderOptionsCount);
        });
        cut.Find("[data-testid='workflows-template-catalogue-close']").Click();

        cut.Find("[data-testid='workflows-tab-editor']").Click();

        cut.WaitForElement("[data-testid='workflow-canvas-editor']");
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, counter.ListComponentsCount);
            Assert.Equal(1, counter.ListProviderOptionsCount);
        });
    }

    [Fact]
    public async Task Workflows_page_loads_full_selected_definition_before_rendering_editor_canvas()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var definition = await CreateCanvasLoadDefinitionAsync(catalogService);
        await CreateHistoryDefinitionAsync(catalogService);

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.Render<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-tab-editor']");
        cut.Find("[data-testid='workflows-tab-editor']").Click();
        cut.WaitForElement("[data-testid='workflow-canvas-editor']");
        cut.WaitForAssertion(() =>
        {
            var surface = cut.FindComponent<CanvasWorkbench>().Instance.Surface;
            Assert.DoesNotContain(surface.Nodes, node => node.Id == "work");
        });

        cut.Find("[data-testid='workflows-tab-workflows']").Click();
        cut.WaitForElement("[data-testid='workflows-catalog']");
        cut.WaitForAssertion(() => Assert.Contains(
            cut.FindAll("[data-testid='workflows-catalog-item']"),
            item => item.TextContent.Contains(definition.Name, StringComparison.Ordinal)));
        cut.FindAll("[data-testid='workflows-catalog-item']")
            .First(item => item.TextContent.Contains(definition.Name, StringComparison.Ordinal))
            .Click();
        cut.Find("[data-testid='workflows-tab-editor']").Click();
        cut.WaitForElement("[data-testid='workflow-canvas-editor']");

        cut.WaitForAssertion(() =>
        {
            var surface = cut.FindComponent<CanvasWorkbench>().Instance.Surface;
            Assert.Equal(definition.Graph.Nodes.Count, surface.Nodes.Count);
            Assert.Contains(surface.Nodes, node => node.Id == "work");
            Assert.Contains(surface.Links, link => link.SourceId == "start" && link.TargetId == "work");
            Assert.Contains(surface.Links, link => link.SourceId == "work" && link.TargetId == "end");
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Workflows_page_ignores_late_definition_result_after_newer_selection(bool staleRequestFails)
    {
        await using var harness = await ComponentTestHarness.CreateAsync(RegisterRacingWorkflowCatalog);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var racingCatalog = harness.Context.Services.GetRequiredService<RacingWorkflowCatalogService>();
        var firstDefinition = await CreateCanvasLoadDefinitionAsync(catalogService);
        var secondDefinition = await CreateHistoryDefinitionAsync(catalogService);

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.Render<WorkflowsPage>();
        cut.WaitForAssertion(
            () => Assert.Empty(cut.FindAll("[data-testid='workflows-loading']")),
            TimeSpan.FromSeconds(10));
        racingCatalog.Delay(firstDefinition.Id, secondDefinition.Id);

        var firstSelection = cut.InvokeAsync(() => InvokeSelectDefinitionAsync(cut.Instance, firstDefinition.Id));
        await racingCatalog.WaitForRequestAsync(firstDefinition.Id);
        cut.WaitForAssertion(() => Assert.Equal(
            AgentChatContextAccessState.Loading,
            ReadAgentChatAccessState(cut.Instance)));

        var secondSelection = cut.InvokeAsync(() => InvokeSelectDefinitionAsync(cut.Instance, secondDefinition.Id));
        await racingCatalog.WaitForRequestAsync(secondDefinition.Id);

        racingCatalog.Complete(secondDefinition.Id);
        await secondSelection;
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(secondDefinition.Id, ReadSelectedDefinition(cut.Instance)?.Id);
            Assert.Equal(AgentChatContextAccessState.Ready, ReadAgentChatAccessState(cut.Instance));
        });

        if (staleRequestFails)
        {
            racingCatalog.Fail(firstDefinition.Id);
        }
        else
        {
            racingCatalog.Complete(firstDefinition.Id);
        }

        await firstSelection;
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(secondDefinition.Id, ReadSelectedDefinition(cut.Instance)?.Id);
            Assert.Equal(AgentChatContextAccessState.Ready, ReadAgentChatAccessState(cut.Instance));
        });
    }

    [Fact]
    public async Task Workflows_page_ignores_late_history_page_and_run_selection_results()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var firstDefinition = await CreateCanvasLoadDefinitionAsync(catalogService);
        var secondDefinition = await CreateHistoryDefinitionAsync(catalogService);
        var now = DateTimeOffset.UtcNow;
        var firstRun = CreateRun(firstDefinition, "first-definition-run", now.AddMinutes(-2));
        var secondRun = CreateRun(secondDefinition, "second-definition-run", now.AddMinutes(-1));
        var newestSecondRun = CreateRun(secondDefinition, "newest-second-run", now);
        var runStore = new CountingWorkflowRunStore(new WorkflowRunStoreCallCounter());
        await runStore.SaveRunAsync(firstRun);
        await runStore.SaveRunAsync(secondRun);
        await runStore.SaveRunAsync(newestSecondRun);
        var runtimeManager = new RacingWorkflowRuntimeManager(firstRun, secondRun, newestSecondRun);

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.Render<WorkflowsPage>();
        cut.Instance.RunStore = runStore;
        cut.Instance.RuntimeManager = runtimeManager;
        await cut.InvokeAsync(() => InvokeSelectDefinitionAsync(cut.Instance, firstDefinition.Id));

        runStore.DelayRunPages(firstDefinition.Id, secondDefinition.Id);
        var staleHistory = cut.InvokeAsync(() => InvokeLoadRunsPageAsync(cut.Instance, firstDefinition.Id));
        await runStore.WaitForRunPageRequestAsync(firstDefinition.Id);
        cut.WaitForAssertion(() => Assert.Equal(
            AgentChatContextAccessState.Loading,
            ReadAgentChatAccessState(cut.Instance)));

        await cut.InvokeAsync(() => InvokeSelectDefinitionAsync(cut.Instance, secondDefinition.Id));
        var currentHistory = cut.InvokeAsync(() => InvokeLoadRunsPageAsync(cut.Instance, secondDefinition.Id));
        await runStore.WaitForRunPageRequestAsync(secondDefinition.Id);
        runStore.CompleteRunPage(secondDefinition.Id);
        await currentHistory;

        runStore.CompleteRunPage(firstDefinition.Id);
        await staleHistory;
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(secondDefinition.Id, ReadSelectedDefinition(cut.Instance)?.Id);
            Assert.All(ReadRuns(cut.Instance), run => Assert.Equal(secondDefinition.Id, run.WorkflowId));
            Assert.Equal(secondDefinition.Id, ReadSelectedRun(cut.Instance)?.WorkflowId);
            Assert.Equal(AgentChatContextAccessState.Ready, ReadAgentChatAccessState(cut.Instance));
        });

        runtimeManager.DelayRuns(secondRun.RunId, newestSecondRun.RunId);
        var staleRunSelection = cut.InvokeAsync(() => InvokeSelectRunAsync(cut.Instance, secondRun.RunId));
        await runtimeManager.WaitForRunRequestAsync(secondRun.RunId);
        var currentRunSelection = cut.InvokeAsync(() => InvokeSelectRunAsync(cut.Instance, newestSecondRun.RunId));
        await runtimeManager.WaitForRunRequestAsync(newestSecondRun.RunId);
        runtimeManager.CompleteRun(newestSecondRun.RunId);
        await currentRunSelection;
        runtimeManager.CompleteRun(secondRun.RunId);
        await staleRunSelection;

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(newestSecondRun.RunId, ReadSelectedRun(cut.Instance)?.RunId);
            Assert.Equal(secondDefinition.Id, ReadSelectedRun(cut.Instance)?.WorkflowId);
            Assert.Equal(AgentChatContextAccessState.Ready, ReadAgentChatAccessState(cut.Instance));
        });
    }

    [Fact]
    public async Task Workflows_page_resolves_exact_non_first_project_workflow_and_run_route()
    {
        var projectGateway = new WorkflowRouteProjectGateway();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<IProjectStructureRuntimeGateway>();
            services.AddSingleton<IProjectStructureRuntimeGateway>(projectGateway);
        });
        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var runStore = harness.Context.Services.GetRequiredService<IWorkflowRunStore>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var targetDefinition = await CreateCanvasLoadDefinitionAsync(catalogService);
        await CreateHistoryDefinitionAsync(catalogService);
        Assert.NotEqual(targetDefinition.Id, (await catalogService.ListDefinitionsAsync())[0].Id);

        var now = DateTimeOffset.UtcNow;
        var requestedRun = CreateRun(targetDefinition, "requested-non-first-run", now.AddMinutes(-2));
        var newerRun = CreateRun(targetDefinition, "newer-run", now);
        await runStore.SaveRunAsync(requestedRun);
        await runStore.SaveRunAsync(newerRun);
        var projectId = Guid.NewGuid();
        projectGateway.SetProjectWorkflow(projectId, "Exact workflow project", targetDefinition.Id);
        navigation.NavigateTo(
            $"/agents/workflows?projectId={projectId:D}&workflowId={targetDefinition.Id.Value:D}&runId={requestedRun.RunId.Value:D}");

        var cut = harness.Context.Render<WorkflowsPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(AgentChatContextAccessState.Ready, ReadAgentChatAccessState(cut.Instance));
            Assert.Equal(targetDefinition.Id, ReadSelectedDefinition(cut.Instance)?.Id);
            Assert.Equal(requestedRun.RunId, ReadSelectedRun(cut.Instance)?.RunId);
            var surface = ReadAgentChatSurface(cut.Instance);
            Assert.Equal(targetDefinition.Id.Value.ToString("D"), surface.Position.PrimarySelection?.Id);
            Assert.Contains(surface.Position.SelectedEntities, entity =>
                entity.Kind == "project" && entity.Id == projectId.ToString("D"));
            Assert.Contains(surface.Position.SelectedEntities, entity =>
                entity.Kind == "workflow-run" && entity.Id == requestedRun.RunId.Value.ToString("D"));
            Assert.Contains($"projectId={projectId:D}", surface.Position.Route, StringComparison.Ordinal);
            Assert.Contains($"workflowId={targetDefinition.Id.Value:D}", surface.Position.Route, StringComparison.Ordinal);
            Assert.Contains($"runId={requestedRun.RunId.Value:D}", surface.Position.Route, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Workflows_page_fails_closed_when_requested_workflow_is_missing()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var availableDefinition = await CreateCanvasLoadDefinitionAsync(catalogService);
        var missingWorkflowId = WorkflowId.New();
        navigation.NavigateTo($"/agents/workflows?workflowId={missingWorkflowId.Value:D}");

        var cut = harness.Context.Render<WorkflowsPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(AgentChatContextAccessState.Failed, ReadAgentChatAccessState(cut.Instance));
            Assert.Null(ReadSelectedDefinitionOrNull(cut.Instance));
            Assert.Null(ReadSelectedRunOrNull(cut.Instance));
            Assert.Contains(missingWorkflowId.ToString(), cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain(
                availableDefinition.Name,
                ReadAgentChatSurface(cut.Instance).Position.PrimarySelection?.DisplayName ?? string.Empty,
                StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Workflows_page_fails_closed_when_requested_run_belongs_to_another_workflow()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var runStore = harness.Context.Services.GetRequiredService<IWorkflowRunStore>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var requestedDefinition = await CreateCanvasLoadDefinitionAsync(catalogService);
        var otherDefinition = await CreateHistoryDefinitionAsync(catalogService);
        var conflictingRun = CreateRun(otherDefinition, "wrong-workflow-run", DateTimeOffset.UtcNow);
        await runStore.SaveRunAsync(conflictingRun);
        navigation.NavigateTo(
            $"/agents/workflows?workflowId={requestedDefinition.Id.Value:D}&runId={conflictingRun.RunId.Value:D}");

        var cut = harness.Context.Render<WorkflowsPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(AgentChatContextAccessState.Failed, ReadAgentChatAccessState(cut.Instance));
            Assert.Null(ReadSelectedDefinitionOrNull(cut.Instance));
            Assert.Null(ReadSelectedRunOrNull(cut.Instance));
            Assert.Contains(conflictingRun.RunId.ToString(), cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Workflows_page_fails_closed_when_workflow_is_not_attached_to_requested_project()
    {
        var projectGateway = new WorkflowRouteProjectGateway();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<IProjectStructureRuntimeGateway>();
            services.AddSingleton<IProjectStructureRuntimeGateway>(projectGateway);
        });
        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var requestedDefinition = await CreateCanvasLoadDefinitionAsync(catalogService);
        var attachedDefinition = await CreateHistoryDefinitionAsync(catalogService);
        var projectId = Guid.NewGuid();
        projectGateway.SetProjectWorkflow(projectId, "Different workflow project", attachedDefinition.Id);
        navigation.NavigateTo(
            $"/agents/workflows?projectId={projectId:D}&workflowId={requestedDefinition.Id.Value:D}");

        var cut = harness.Context.Render<WorkflowsPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(AgentChatContextAccessState.Failed, ReadAgentChatAccessState(cut.Instance));
            Assert.Null(ReadSelectedDefinitionOrNull(cut.Instance));
            Assert.Contains("is not attached", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Workflows_page_ignores_late_route_result_after_newer_route_identity()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(RegisterRacingWorkflowCatalog);
        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var racingCatalog = harness.Context.Services.GetRequiredService<RacingWorkflowCatalogService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var staleDefinition = await CreateCanvasLoadDefinitionAsync(catalogService);
        var currentDefinition = await CreateHistoryDefinitionAsync(catalogService);
        racingCatalog.Delay(staleDefinition.Id, currentDefinition.Id);
        navigation.NavigateTo($"/agents/workflows?workflowId={staleDefinition.Id.Value:D}");

        var cut = harness.Context.Render<WorkflowsPage>();
        await racingCatalog.WaitForRequestAsync(staleDefinition.Id);

        navigation.NavigateTo($"/agents/workflows?workflowId={currentDefinition.Id.Value:D}");
        await racingCatalog.WaitForRequestAsync(currentDefinition.Id);
        racingCatalog.Complete(currentDefinition.Id);
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(currentDefinition.Id, ReadSelectedDefinition(cut.Instance)?.Id);
            Assert.Equal(AgentChatContextAccessState.Ready, ReadAgentChatAccessState(cut.Instance));
        });

        racingCatalog.Complete(staleDefinition.Id);
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(currentDefinition.Id, ReadSelectedDefinition(cut.Instance)?.Id);
            Assert.Equal(AgentChatContextAccessState.Ready, ReadAgentChatAccessState(cut.Instance));
        });
    }

    [Fact]
    public async Task Persistent_workflow_catalog_uses_same_latest_version_for_summary_and_detail()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var dbContextFactory = harness.Context.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var workflowId = WorkflowId.New();
        var timestamp = DateTimeOffset.UtcNow;
        var oldVersion = new WorkflowVersionId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var latestVersion = new WorkflowVersionId(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));
        var oldDefinition = CreateCanvasLoadDefinition(
            workflowId,
            oldVersion,
            "Older tied workflow version",
            includeWorkNode: false,
            timestamp);
        var latestDefinition = CreateCanvasLoadDefinition(
            workflowId,
            latestVersion,
            "Latest tied workflow version",
            includeWorkNode: true,
            timestamp);

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Set<WorkflowDefinitionRecord>().AddRange(
                WorkflowDefinitionRecord.FromDefinition(oldDefinition, revision: 1),
                WorkflowDefinitionRecord.FromDefinition(latestDefinition, revision: 2));
            dbContext.Set<WorkflowDefinitionHeadRecord>().Add(new WorkflowDefinitionHeadRecord
            {
                WorkflowId = workflowId.Value,
                VersionId = latestVersion.Value
            });
            await dbContext.SaveChangesAsync();
        }

        var summary = Assert.Single(await catalogService.ListDefinitionsAsync(), item => item.Id == workflowId);
        var detail = await catalogService.GetDefinitionAsync(workflowId);

        Assert.NotNull(detail);
        Assert.Equal(summary.VersionId, detail.Definition.VersionId);
        Assert.Equal(summary.Name, detail.Definition.Name);
        Assert.Contains(detail.Definition.Graph.Nodes, node => node.Id.Value == "work");
    }

    [Fact]
    public async Task Workflows_page_defers_runtime_history_until_history_needs_it()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(RegisterCountingWorkflowRunStore);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var runStore = harness.Context.Services.GetRequiredService<IWorkflowRunStore>();
        var counter = harness.Context.Services.GetRequiredService<WorkflowRunStoreCallCounter>();
        var definition = await CreateHistoryDefinitionAsync(catalogService);
        var runId = WorkflowRunId.New();
        var now = DateTimeOffset.UtcNow;
        await runStore.SaveRunAsync(new WorkflowRunSnapshot(
            runId,
            definition.Id,
            definition.VersionId,
            WorkflowRunState.Completed,
            WorkflowRuntimeBackendKind.InProcess,
            "lazy-history-run",
            "Lazy history run should load only after History is selected.",
            now,
            now));
        await runStore.SaveEventAsync(new WorkflowEventRecord(
            Guid.NewGuid(),
            runId,
            WorkflowEventKind.Completed,
            new WorkflowNodeId("lazy-node"),
            "Lazy history event loaded on demand.",
            "{\"loaded\":true}",
            now));
        await runStore.SaveArtifactAsync(new WorkflowArtifactRecord(
            WorkflowArtifactId.New(),
            runId,
            WorkflowArtifactKind.Text,
            new WorkflowNodeId("lazy-node"),
            "lazy-history.txt",
            "text/plain",
            "workflow-runs/lazy-history/lazy-history.txt",
            "Lazy history artifact.",
            now));
        await runStore.SaveExternalRequestAsync(new WorkflowExternalRequestRecord(
            WorkflowExternalRequestId.New(),
            runId,
            WorkflowExternalRequestKind.Approval,
            new WorkflowNodeId("lazy-node"),
            "approval:lazy-history",
            "{}",
            string.Empty,
            now,
            RespondedAtUtc: null));

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.Render<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-tabs']");

        Assert.Equal(0, counter.ListRunPageCount);
        Assert.Equal(0, counter.GetRunCount);
        Assert.Equal(0, counter.ListEventPageCount);
        Assert.Equal(0, counter.ListArtifactsCount);
        Assert.Equal(0, counter.ListPendingExternalRequestsCount);
        Assert.DoesNotContain("Lazy history run", cut.Markup, StringComparison.Ordinal);

        cut.Find("[data-testid='workflows-tab-history']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, counter.ListRunPageCount);
            Assert.Equal(1, counter.GetRunCount);
            Assert.Equal(1, counter.ListEventPageCount);
            Assert.Equal(1, counter.ListArtifactsCount);
            Assert.Equal(1, counter.ListPendingExternalRequestsCount);
            Assert.Single(cut.FindAll("[data-testid='workflows-run-item']"));
            Assert.Single(cut.FindAll("[data-testid='workflows-run-event']"));
            Assert.Contains("Lazy history run", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Workflows_template_catalogue_dialog_loads_examples_from_workflows_tab()
    {
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-template-page-tests");
        await using var harness = await CreateInMemoryWorkflowHarnessAsync(environment);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.Render<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-tab-workflows']");
        Assert.Empty(cut.FindAll("[data-testid='workflows-tab-templates']"));
        AssertNoWorkflowPageError(cut);
        cut.Find("[data-testid='workflows-tab-workflows']").Click();
        cut.WaitForElement("[data-testid='workflows-open-template-catalogue']");
        cut.Find("[data-testid='workflows-open-template-catalogue']").Click();
        cut.WaitForElement("[data-testid='workflows-template-catalogue-dialog']");

        cut.WaitForAssertion(() =>
        {
            var templates = cut.FindAll("[data-testid='workflows-template-catalogue-item']");
            Assert.Contains(templates, item => item.TextContent.Contains("Local Folder Summary Markdown Report", StringComparison.Ordinal));
            Assert.Contains(templates, item => item.TextContent.Contains("File Diff Markdown Report", StringComparison.Ordinal));
            Assert.Contains(templates, item => item.TextContent.Contains("HTTP Download Document Extraction Report", StringComparison.Ordinal));
            Assert.Contains(templates, item => item.TextContent.Contains("JSON Transform Project Task Creation", StringComparison.Ordinal));
            Assert.Contains(templates, item => item.TextContent.Contains("Approval Gated HTTP Action", StringComparison.Ordinal));
            Assert.NotEmpty(cut.FindAll("[data-testid='workflows-template-preview']"));
            Assert.Contains("Seed", cut.Find("[data-testid='workflows-template-catalogue-dialog']").TextContent, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Workflows_template_catalogue_loads_pack_only_when_dialog_opens()
    {
        var invalidPackRoot = Path.Combine(Path.GetTempPath(), $"invalid-workflow-template-pack-{Guid.NewGuid():N}");
        Directory.CreateDirectory(invalidPackRoot);
        File.WriteAllText(Path.Combine(invalidPackRoot, "manifest.yaml"), "packKey: [");
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<WorkflowTemplatePackLoader>();
            services.AddScoped(_ => new WorkflowTemplatePackLoader(invalidPackRoot));
        });
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.Render<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-tab-workflows']");
        Assert.Empty(cut.FindAll("[data-testid='workflows-error']"));

        cut.Find("[data-testid='workflows-tab-workflows']").Click();
        cut.WaitForElement("[data-testid='workflows-catalog']");
        Assert.Empty(cut.FindAll("[data-testid='workflows-error']"));

        cut.Find("[data-testid='workflows-open-template-catalogue']").Click();
        cut.WaitForElement("[data-testid='workflows-template-catalogue-dialog']");

        cut.WaitForAssertion(() =>
        {
            var error = cut.Find("[data-testid='workflows-template-catalogue-error']");
            Assert.False(string.IsNullOrWhiteSpace(error.TextContent));
        });
    }

    [Fact]
    public async Task Workflows_template_preview_dialog_renders_canvas_without_saving()
    {
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-template-preview-tests");
        await using var harness = await CreateInMemoryWorkflowHarnessAsync(environment);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var initialDefinitionCount = (await catalogService.ListDefinitionsAsync()).Count;

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.Render<WorkflowsPage>();

        OpenTemplatePreview(cut, "Local Folder Summary Markdown Report");

        cut.WaitForElement("[data-testid='workflows-template-preview-dialog']");
        cut.WaitForElement("[data-testid='workflows-template-preview-canvas']");
        cut.WaitForAssertion(() =>
        {
            var canvas = Assert.Single(cut.FindComponents<CanvasWorkbench>());
            Assert.NotEmpty(canvas.Instance.Surface.Nodes);
            Assert.Equal(0.48, canvas.Instance.Surface.UiState.Zoom, 2);
            Assert.Equal(144, canvas.Instance.Surface.UiState.PanX, 2);
            Assert.Equal(88, canvas.Instance.Surface.UiState.PanY, 2);
            Assert.Empty(cut.FindAll("[data-testid='workflow-canvas-save']"));
        });
        Assert.Equal(initialDefinitionCount, (await catalogService.ListDefinitionsAsync()).Count);
    }

    [Fact]
    public async Task Workflows_template_add_to_drafts_uses_next_prefix_when_name_exists()
    {
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-template-draft-name-tests");
        await using var harness = await CreateInMemoryWorkflowHarnessAsync(environment);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var componentLibrary = harness.Context.Services.GetRequiredService<IWorkflowComponentLibraryService>();
        var notificationService = harness.Context.Services.GetRequiredService<NotificationService>();
        var templatePack = new WorkflowTemplatePackLoader().Load();
        var template = templatePack.Workflows.Single(item =>
            string.Equals(item.Name, "Local Folder Summary Markdown Report", StringComparison.Ordinal));
        await SaveTemplateDraftAsync(catalogService, componentLibrary, templatePack, template, template.Name);
        await SaveTemplateDraftAsync(catalogService, componentLibrary, templatePack, template, $"01 {template.Name}");

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.Render<WorkflowsPage>();

        OpenTemplatePreview(cut, template.Name);
        cut.WaitForElement("[data-testid='workflows-template-add-draft']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(notificationService.Messages, message => message.Summary == "Template added to drafts");
        });

        var definitions = await catalogService.ListDefinitionsAsync();
        var created = Assert.Single(definitions, definition =>
            string.Equals(definition.Name, $"02 {template.Name}", StringComparison.Ordinal));
        Assert.Equal(WorkflowLifecycleStatus.Draft, created.Status);
    }

    [Fact]
    public async Task Workflow_canvas_toolbox_exposes_executor_catalog_metadata()
    {
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-canvas-catalog-tests");
        await using var harness = await CreateInMemoryWorkflowHarnessAsync(environment);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.Render<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-tab-editor']");
        ClickTabButton(cut, "Editor");
        cut.WaitForElement("[data-testid='workflow-canvas-editor']");
        var toolboxSearch = EnsureWorkflowToolboxVisible(cut);

        toolboxSearch.Input("json");
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("workflow-toolbox-executor-json-transform", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Deterministic preview", cut.Markup, StringComparison.Ordinal);
        });

        toolboxSearch.Input("markdown");
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("workflow-toolbox-executor-markdown-render", cut.Markup, StringComparison.Ordinal);
        });

        toolboxSearch.Input("delay");
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("workflow-toolbox-executor-utility-delay", cut.Markup, StringComparison.Ordinal);
        });

        toolboxSearch.Input("http");
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Approval required", cut.Markup, StringComparison.Ordinal);
        });

        toolboxSearch.Input("command");
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("workflow-toolbox-executor-command-process", cut.Markup, StringComparison.Ordinal);
            Assert.True(cut.Find("[data-testid='workflow-toolbox-executor-command-process']").HasAttribute("disabled"));
        });
    }

    [Fact]
    public async Task Workflow_canvas_toolbox_opens_custom_executor_settings_in_the_node_inspector()
    {
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-canvas-custom-settings-tests");
        await using var harness = await CreateInMemoryWorkflowHarnessAsync(environment);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.Render<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-tab-editor']");
        ClickTabButton(cut, "Editor");
        cut.WaitForElement("[data-testid='workflow-canvas-editor']");
        var toolboxSearch = EnsureWorkflowToolboxVisible(cut);

        toolboxSearch.Input("image generation");
        cut.WaitForElement("[data-testid='workflow-toolbox-executor-image-generate']").Click();

        cut.WaitForAssertion(() =>
        {
            var providerSelector = cut.Find("[data-testid='workflow-canvas-executor-settings-providerProfileId']");
            Assert.Equal("select", providerSelector.TagName, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("Create Image generation", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Workflow_canvas_places_llm_component_validates_runs_and_saves_definition()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(RegisterDeterministicWorkflowLlmInvoker);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var notificationService = harness.Context.Services.GetRequiredService<NotificationService>();
        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var componentLibrary = harness.Context.Services.GetRequiredService<IWorkflowComponentLibraryService>();
        var runStore = harness.Context.Services.GetRequiredService<IWorkflowRunStore>();
        const string promptTitle = "Workflow canvas prompt";
        var dialogHost = await PrepareFinalWorkflowPromptAsync(
            harness,
            promptTitle,
            "Return a concise workflow canvas test summary.");

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.Render<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-tab-editor']");
        cut.Find("[data-testid='workflows-tab-editor']").Click();
        cut.WaitForElement("[data-testid='workflow-canvas-editor']");
        cut.Find("[data-testid='workflow-canvas-toggle-components']").Click();
        cut.WaitForElement("[data-testid='workflow-canvas-provider-options']");
        SelectWorkflowPromptFromGallery(cut, dialogHost, promptTitle);
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(notificationService.Messages, message => message.Summary == "Gallery prompt bound");
            Assert.NotEmpty(cut.FindAll("[data-testid='workflow-canvas-component']"));
        });
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("disabled", cut.Find("[data-testid='workflow-canvas-validate']").OuterHtml, StringComparison.OrdinalIgnoreCase);
        });
        var component = Assert.Single(await componentLibrary.ListComponentsAsync());

        await ClickWorkflowCanvasTabAsync(cut, "workflow-canvas-tab-routes");
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(component.Name, cut.Markup);
            Assert.NotEmpty(cut.FindAll("[data-testid='workflow-canvas-edge-row']"));
        });

        await ClickWorkflowCanvasTabAsync(cut, "workflow-canvas-tab-node");
        Assert.NotEmpty(cut.FindAll("[data-testid='workflow-canvas-node-prompt-identity']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='workflow-canvas-node-provider']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='workflow-canvas-node-model-selector']"));
        Assert.True(cut.Find("[data-testid='workflow-canvas-node-instructions']").HasAttribute("readonly"));
        cut.Find("[data-testid='workflow-canvas-validate']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(notificationService.Messages, message => message.Summary == "Workflow canvas valid");
            Assert.DoesNotContain("workflow-canvas-validation-issue", cut.Markup);
        });

        cut.Find("[data-testid='workflow-canvas-run-preview']").Click();
        await ClickWorkflowCanvasTabAsync(cut, "workflow-canvas-tab-preview");
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(notificationService.Messages, message => message.Summary == "Workflow preview completed");
            Assert.Contains("Succeeded", cut.Find("[data-testid='workflow-canvas-test-result']").TextContent);
            var dialog = cut.Find("[data-testid='workflows-run-detail-dialog']");
            Assert.Contains("Summary", dialog.TextContent);
            Assert.Contains("Result", dialog.TextContent);
            Assert.Contains("Workflow LLM test output", dialog.TextContent);
            Assert.Contains("Events", dialog.TextContent);
        });
        Assert.Single(await runStore.ListRunsAsync());

        cut.Find("[data-testid='workflow-canvas-save']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(notificationService.Messages, message => message.Summary == "Workflow saved");
        });

        await cut.InvokeAsync(() => cut.Find("[data-testid='workflows-tab-workflows']").Click());
        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll("[data-testid='workflows-catalog-item']"));
        });

        var definition = Assert.Single(await catalogService.ListDefinitionsAsync());
        var detail = await catalogService.GetDefinitionAsync(definition.Id);

        Assert.NotNull(detail);
        var llmNode = Assert.Single(detail!.Definition.Graph.Nodes, node => node.Kind == WorkflowNodeKind.LlmCall);
        Assert.Equal(component.Id, llmNode.Settings.ComponentId);
        Assert.Equal(component.ProviderProfileId, llmNode.Settings.ProviderProfileId);
        Assert.Equal(component.Model, llmNode.Settings.Model);
        Assert.Equal(component.Instructions, llmNode.Settings.Instructions);
        Assert.Contains(detail.Definition.Graph.Nodes, node => node.CanvasX != 0 && node.CanvasY != 0);
    }

    [Fact]
    public async Task Workflow_prompt_details_dialog_preserves_workflow_route_and_parent_dialogs()
    {
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-nested-prompt-details-tests");
        await using var harness = await CreateInMemoryWorkflowHarnessAsync(environment);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var notifications = harness.Context.Services.GetRequiredService<NotificationService>();
        var componentLibrary = harness.Context.Services.GetRequiredService<IWorkflowComponentLibraryService>();
        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        const string promptTitle = "Workflow nested prompt details";
        const string workflowName = "Workflow with nested Gallery details";
        var dialogHost = await PrepareFinalWorkflowPromptAsync(
            harness,
            promptTitle,
            "Keep the workflow editor active while reviewing these prompt details.");
        var llmComponent = await componentLibrary.SaveComponentAsync(new LlmCallComponentSaveRequest(
            Id: null,
            Name: "Nested Gallery details LLM",
            ProviderProfileId: null,
            Model: ManagedSeedProviderFallbacks.OpenAiDefaultModel,
            WorkflowModality.Text,
            new WorkflowModelSettings(
                Temperature: 0.2,
                MaxOutputTokens: 800,
                RequireJsonOutput: false,
                ResponseFormatJsonSchema: string.Empty),
            Instructions: "Summarize the current workflow payload.",
            WorkflowValueShape.Text,
            WorkflowValueShape.Text,
            AgentPermissionsPolicy.Default));
        await catalogService.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            workflowName,
            Description: "Exercises nested Prompt Gallery details from an LLM node.",
            WorkflowLifecycleStatus.Draft,
            CreateStarterGraph(llmComponent.Id),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false)));

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.Render<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-tab-workflows']").Click();
        cut.WaitForAssertion(() => Assert.Contains(
            cut.FindAll("[data-testid='workflows-catalog-item']"),
            item => item.TextContent.Contains(workflowName, StringComparison.Ordinal)));
        cut.FindAll("[data-testid='workflows-catalog-item']")
            .Single(item => item.TextContent.Contains(workflowName, StringComparison.Ordinal))
            .Click();
        cut.Find("[data-testid='workflows-tab-editor']").Click();
        cut.WaitForElement("[data-testid='workflow-canvas-editor']");
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(
                cut.FindAll("[data-testid='workflow-canvas-select-node']"),
                item => item.TextContent.Contains("LlmCall", StringComparison.Ordinal));
        });
        cut.FindAll("[data-testid='workflow-canvas-select-node']")
            .Single(item => item.TextContent.Contains("LlmCall", StringComparison.Ordinal))
            .Click();
        cut.WaitForElement("[data-testid='workflow-canvas-open-selected-node-details']").Click();

        var nodeDetails = cut.WaitForElement("[data-testid='workflow-canvas-node-details-modal']");
        var openGallery = nodeDetails.QuerySelector("[data-testid='prompt-gallery-picker-button']");
        Assert.NotNull(openGallery);
        openGallery.Click();

        dialogHost.WaitForElement("[data-testid='prompt-gallery-picker-dialog']");
        dialogHost.Find("[data-testid='prompt-gallery-search']").Input(promptTitle);
        dialogHost.WaitForAssertion(() =>
        {
            Assert.Contains(promptTitle, dialogHost.Markup, StringComparison.Ordinal);
            Assert.Single(dialogHost.FindAll("[data-testid='prompt-gallery-edit']"));
        });
        dialogHost.Find("[data-testid='prompt-gallery-edit']").Click();

        dialogHost.WaitForAssertion(() =>
        {
            Assert.Equal(
                promptTitle,
                dialogHost.Find("[data-testid='prompt-gallery-editor-title']").GetAttribute("value"));
            Assert.Equal("/agents/workflows", new Uri(navigation.Uri).AbsolutePath);
            Assert.NotEmpty(cut.FindAll("[data-testid='workflow-canvas-node-details-modal']"));
        });

        var promptEditor = dialogHost.Find("[data-testid='prompt-gallery-item-editor']");
        var saveDraft = promptEditor.QuerySelector("button[type='submit']");
        Assert.NotNull(saveDraft);
        saveDraft.Click();
        dialogHost.WaitForAssertion(() =>
        {
            Assert.Contains(notifications.Messages, message => message.Summary == "Prompt draft saved");
        });

        var cancel = dialogHost.Find("[data-testid='prompt-gallery-item-editor']")
            .QuerySelectorAll("button")
            .Single(button => button.TextContent.Contains("Cancel", StringComparison.Ordinal));
        cancel.Click();

        dialogHost.WaitForAssertion(() =>
        {
            Assert.Empty(dialogHost.FindAll("[data-testid='prompt-gallery-item-editor']"));
            Assert.NotEmpty(dialogHost.FindAll("[data-testid='prompt-gallery-picker-dialog']"));
            Assert.Equal(
                promptTitle,
                dialogHost.Find("[data-testid='prompt-gallery-search']").GetAttribute("value"));
            Assert.NotEmpty(cut.FindAll("[data-testid='workflow-canvas-node-details-modal']"));
            Assert.Equal("/agents/workflows", new Uri(navigation.Uri).AbsolutePath);
        });
    }

    [Fact]
    public async Task Workflow_canvas_preview_prompts_for_project_context_and_can_skip_project_writes()
    {
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var runner = new CapturingWorkflowTestRunner();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<IWorkflowTestRunner>();
            services.AddSingleton<IWorkflowTestRunner>(runner);
            services.RemoveAll<IProjectStructureRuntimeGateway>();
            services.AddSingleton<IProjectStructureRuntimeGateway>(new PreviewProjectGateway(projectId));
        });
        var definition = CreateProjectStructurePreviewDefinition();

        var cut = harness.Context.Render<WorkflowCanvasEditor>(parameters => parameters
            .Add(component => component.Definition, definition)
            .Add(component => component.Components, [])
            .Add(component => component.ProviderOptions, []));

        cut.WaitForElement("[data-testid='workflow-canvas-run-preview']");
        cut.Find("[data-testid='workflow-canvas-run-preview']").Click();

        cut.WaitForElement("[data-testid='workflow-canvas-preview-input-dialog']");
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Project structure preview target", cut.Markup);
            Assert.Contains(projectId.ToString("D"), cut.Find("[data-testid='workflow-canvas-preview-project-id']").GetAttribute("value"));
        });

        cut.Find("[data-testid='workflow-canvas-preview-node-id']").Change("custom:test-parent-node");
        cut.Find("[data-testid='workflow-canvas-preview-simulate-store']").Change(true);
        cut.Find("[data-testid='workflow-canvas-preview-input-run']").Click();
        await ClickWorkflowCanvasTabAsync(cut, "workflow-canvas-tab-preview");

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(runner.LastRequest);
            Assert.Contains("Succeeded", cut.Find("[data-testid='workflow-canvas-test-result']").TextContent);
        });

        Assert.NotNull(runner.LastRequest);
        using var inputDocument = System.Text.Json.JsonDocument.Parse(runner.LastRequest!.InputJson);
        Assert.Equal(projectId.ToString("D"), inputDocument.RootElement.GetProperty("projectId").GetString());
        Assert.Equal(projectId.ToString("D"), inputDocument.RootElement.GetProperty("project").GetProperty("id").GetString());
        Assert.Equal("custom:test-parent-node", inputDocument.RootElement.GetProperty("nodeId").GetString());
        Assert.Equal("custom:test-parent-node", inputDocument.RootElement.GetProperty("runContext").GetProperty("workflowNodeId").GetString());
        var storeNode = Assert.Single(runner.LastRequest.DraftDefinition!.Graph.Nodes, node => node.Id.Value == "store");
        Assert.Equal(WorkflowNodeKind.Executor, storeNode.Kind);
        Assert.Equal(WorkflowExecutorIds.ProjectStructure, storeNode.Settings.ExecutorId);
        var simulatedStep = Assert.Single(runner.LastRequest.PreviewSimulationPlan.Steps);
        Assert.Equal(storeNode.Id, simulatedStep.NodeId);
        Assert.Equal(WorkflowExecutorIds.ProjectStructure, simulatedStep.SourceExecutorId);
        Assert.Contains("inputPayload", simulatedStep.OutputTemplateJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Workflow_canvas_marks_planned_runtime_backends_unavailable()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var definition = CreatePreviewProgressDefinition();

        var cut = harness.Context.Render<WorkflowCanvasEditor>(parameters => parameters
            .Add(component => component.Definition, definition)
            .Add(component => component.Components, [])
            .Add(component => component.ProviderOptions, []));

        var runtimeSelect = cut.Find("[data-testid='workflow-canvas-runtime']");
        var durableOption = Assert.Single(
            runtimeSelect.QuerySelectorAll("option"),
            option => string.Equals(option.GetAttribute("value"), nameof(WorkflowRuntimeBackendKind.DurableTask), StringComparison.Ordinal));

        Assert.True(durableOption.HasAttribute("disabled"));
        Assert.Contains("Planned", durableOption.TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not registered", durableOption.GetAttribute("title"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Workflow_canvas_floating_windows_stay_below_the_canvas_toolbar()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var definition = CreatePreviewProgressDefinition();

        var cut = harness.Context.Render<WorkflowCanvasEditor>(parameters => parameters
            .Add(component => component.Definition, definition)
            .Add(component => component.Components, [])
            .Add(component => component.ProviderOptions, []));

        cut.WaitForAssertion(() =>
        {
            var windows = cut.FindComponents<CanvasFloatingWindow>();

            Assert.Equal(2, windows.Count);
            Assert.All(windows, window => Assert.Equal(".cw-toolbar", window.Instance.SafeTopSelector));
            Assert.Empty(cut.FindAll(".workflow-canvas-overlay-safe-top"));
        });
    }

    [Fact]
    public async Task Workflow_canvas_reports_immutable_current_definition_node_selection()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var definition = CreatePreviewProgressDefinition();
        WorkflowAgentChatNodeSelection? selected = null;

        var cut = harness.Context.Render<WorkflowCanvasEditor>(parameters => parameters
            .Add(component => component.Definition, definition)
            .Add(component => component.Components, [])
            .Add(component => component.ProviderOptions, [])
            .Add(component => component.SelectedNodeChanged,
                EventCallback.Factory.Create<WorkflowAgentChatNodeSelection?>(
                    this,
                    value => selected = value)));

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(selected);
            Assert.Equal(definition.Id, selected!.DefinitionId);
            Assert.Equal(definition.Graph.StartNodeId, selected.NodeId);
        });

        var anotherNode = definition.Graph.Nodes.First(node => node.Id != definition.Graph.StartNodeId);
        var picker = cut.FindAll(".workflow-canvas-node-picker")
            .Single(item => item.TextContent.Contains(anotherNode.Name, StringComparison.Ordinal));
        picker.Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(definition.Id, selected?.DefinitionId);
            Assert.Equal(anotherNode.Id, selected?.NodeId);
            Assert.Equal(anotherNode.Name, selected?.Name);
            Assert.Equal(anotherNode.Kind, selected?.Kind);
        });
    }

    [Fact]
    public async Task Workflow_canvas_stats_count_workflow_node_usages_not_available_inventory()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var usedComponent = CreateWorkflowComponent("Used summary call");
        var definition = CreateWorkflowUsageStatsDefinition(usedComponent.Id);

        var cut = harness.Context.Render<WorkflowCanvasEditor>(parameters => parameters
            .Add(component => component.Definition, definition)
            .Add(component => component.Components,
            [
                usedComponent,
                CreateWorkflowComponent("Unused research call"),
                CreateWorkflowComponent("Unused validation call")
            ])
            .Add(component => component.ProviderOptions, []));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Workflow usage stats target", cut.Markup, StringComparison.Ordinal);
            var stats = cut.FindAll(".cw-stage-stats .cw-stat-chip")
                .ToDictionary(
                    chip => chip.QuerySelector("span")!.TextContent.Trim(),
                    chip => chip.QuerySelector("strong")!.TextContent.Trim());

            Assert.Equal("6", stats["Nodes"]);
            Assert.Equal("5", stats["Edges"]);
            Assert.Equal("2", stats["Components"]);
            Assert.Equal("2", stats["Executors"]);
            Assert.Equal("Valid", stats["Validation"]);
        });
    }

    [Fact]
    public async Task Workflow_canvas_preview_selects_running_node_from_progress()
    {
        var runner = new NodeProgressWorkflowTestRunner(new WorkflowNodeId("work"));
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<IWorkflowTestRunner>();
            services.AddSingleton<IWorkflowTestRunner>(runner);
        });
        var definition = CreatePreviewProgressDefinition();

        var cut = harness.Context.Render<WorkflowCanvasEditor>(parameters => parameters
            .Add(component => component.Definition, definition)
            .Add(component => component.Components, [])
            .Add(component => component.ProviderOptions, []));

        cut.WaitForElement("[data-testid='workflow-canvas-run-preview']");
        cut.Find("[data-testid='workflow-canvas-run-preview']").Click();
        await ClickWorkflowCanvasTabAsync(cut, "workflow-canvas-tab-preview");

        cut.WaitForAssertion(() =>
        {
            Assert.True(runner.HadProgressObserver);
            Assert.Contains("Succeeded", cut.Find("[data-testid='workflow-canvas-test-result']").TextContent);
            var surface = cut.FindComponent<CanvasWorkbench>().Instance.Surface;
            Assert.Equal(["work"], surface.UiState.SelectedNodeIds);
        });
    }

    [Fact]
    public async Task Workflow_canvas_reconnects_linear_route_after_delete_and_accepts_canvas_connections()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(RegisterDeterministicWorkflowLlmInvoker);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var notificationService = harness.Context.Services.GetRequiredService<NotificationService>();
        const string promptTitle = "Linear route prompt";
        var dialogHost = await PrepareFinalWorkflowPromptAsync(
            harness,
            promptTitle,
            "Keep the workflow route linear.");

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.Render<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-tab-editor']");
        cut.Find("[data-testid='workflows-tab-editor']").Click();
        cut.WaitForElement("[data-testid='workflow-canvas-editor']");
        cut.Find("[data-testid='workflow-canvas-toggle-components']").Click();
        SelectWorkflowPromptFromGallery(cut, dialogHost, promptTitle);
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(notificationService.Messages, message => message.Summary == "Gallery prompt bound");
        });
        cut.WaitForElement("[data-testid='workflow-canvas-place-component']");

        cut.WaitForAssertion(() =>
        {
            Assert.Single(
                cut.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes,
                node => node.Kind == WorkflowNodeKind.LlmCall.ToString());
        });
        SelectWorkflowPromptFromGallery(cut, dialogHost, promptTitle);
        cut.WaitForAssertion(() =>
        {
            var surface = cut.FindComponent<CanvasWorkbench>().Instance.Surface;
            Assert.Equal(CanvasWorkbenchModes.Authoring, surface.Mode);
            Assert.Equal(2, surface.Nodes.Count(node => node.Kind == WorkflowNodeKind.LlmCall.ToString()));
            Assert.Contains(surface.Links, link => link.SourceId == "start" && link.TargetId == "llm");
            Assert.Contains(surface.Links, link => link.SourceId == "llm" && link.TargetId == "llm-1");
            Assert.Contains(surface.Links, link => link.SourceId == "llm-1" && link.TargetId == "end");
        });

        await cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnContextAction("llm", "workflow-node:remove", 0, 0));

        cut.WaitForAssertion(() =>
        {
            var surface = cut.FindComponent<CanvasWorkbench>().Instance.Surface;
            Assert.DoesNotContain(surface.Nodes, node => node.Id == "llm");
            Assert.Contains(surface.Links, link => link.SourceId == "start" && link.TargetId == "llm-1");
            Assert.DoesNotContain(surface.Links, link => link.SourceId == "llm" || link.TargetId == "llm");
        });

        var deleteRequest = new CanvasWorkbenchContextActionRequest(
            NodeId: "llm-1",
            ActionId: "delete-link",
            X: 0,
            Y: 0,
            TargetKind: "link",
            LinkSourceId: "start",
            LinkTargetId: "llm-1",
            LinkKind: "Always",
            LinkSourcePortId: "workflow:output",
            LinkTargetPortId: "workflow:input");
        await cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnContextActionRequest(SerializationPersistencePack.Serialize(deleteRequest)));

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain(
                cut.FindComponent<CanvasWorkbench>().Instance.Surface.Links,
                link => link.SourceId == "start" && link.TargetId == "llm-1");
        });

        var createRequest = deleteRequest with
        {
            ActionId = "connection:create"
        };
        await cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnContextActionRequest(SerializationPersistencePack.Serialize(createRequest)));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(
                cut.FindComponent<CanvasWorkbench>().Instance.Surface.Links,
                link => link.SourceId == "start" && link.TargetId == "llm-1");
        });

        cut.Find("[data-testid='workflow-canvas-validate']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(notificationService.Messages, message => message.Summary == "Workflow canvas valid");
            Assert.DoesNotContain("workflow-canvas-validation-issue", cut.Markup);
        });
    }

    [Fact]
    public async Task Workflow_canvas_authors_typed_predicate_route_metadata()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(RegisterDeterministicWorkflowLlmInvoker);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var notificationService = harness.Context.Services.GetRequiredService<NotificationService>();
        const string promptTitle = "Predicate route prompt";
        var dialogHost = await PrepareFinalWorkflowPromptAsync(
            harness,
            promptTitle,
            "Route high-value invoices with a typed predicate.");

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.Render<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-tab-editor']");
        cut.Find("[data-testid='workflows-tab-editor']").Click();
        cut.WaitForElement("[data-testid='workflow-canvas-editor']");
        cut.Find("[data-testid='workflow-canvas-toggle-components']").Click();
        SelectWorkflowPromptFromGallery(cut, dialogHost, promptTitle);
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(notificationService.Messages, message => message.Summary == "Gallery prompt bound");
        });
        await ClickWorkflowCanvasTabAsync(cut, "workflow-canvas-tab-routes");
        cut.WaitForElement("[data-testid='workflow-canvas-edit-edge']");

        cut.Find("[data-testid='workflow-canvas-edit-edge']").Click();
        cut.Find("[data-testid='workflow-canvas-edge-route-kind']").Change(WorkflowRouteKind.Predicate.ToString());
        cut.WaitForElement("[data-testid='workflow-canvas-edge-route-json-path']");
        cut.Find("[data-testid='workflow-canvas-edge-route-label']").Change("High value");
        cut.Find("[data-testid='workflow-canvas-edge-route-json-path']").Change("$.invoice.total");
        cut.Find("[data-testid='workflow-canvas-edge-route-operator']").Change(WorkflowRouteOperator.GreaterThanOrEqual.ToString());
        cut.Find("[data-testid='workflow-canvas-edge-route-value-kind']").Change(WorkflowRouteValueKind.Number.ToString());
        cut.Find("[data-testid='workflow-canvas-edge-route-expected-value']").Change("5000");
        cut.Find("[data-testid='workflow-canvas-add-edge']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("High value", cut.Find("[data-testid='workflow-canvas-edge-route-summary']").TextContent);
            Assert.Contains("$.invoice.total", cut.Find("[data-testid='workflow-canvas-edge-route-summary']").TextContent);
        });

        cut.Find("[data-testid='workflow-canvas-validate']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(notificationService.Messages, message => message.Summary == "Workflow canvas valid");
            Assert.DoesNotContain("workflow-canvas-validation-issue", cut.Markup);
        });
    }

    [Fact]
    public async Task Workflow_canvas_decision_context_action_adds_and_edits_routes_in_node_dialog()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(RegisterDeterministicWorkflowLlmInvoker);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.Render<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-tab-editor']");
        cut.Find("[data-testid='workflows-tab-editor']").Click();
        cut.WaitForElement("[data-testid='workflow-canvas-editor']");

        var request = new CanvasWorkbenchCreateActionRequest(
            "workflow-decision:create:Switch",
            SourceNodeId: "start",
            X: 420,
            Y: 220,
            ParentNodeId: "start",
            Title: "SWITCH",
            Subtitle: string.Empty,
            Notes: "Route by workflow category.",
            PlacementKind: "child",
            CreateMode: "dialog",
            ObjectSubtype: "Switch",
            UploadedFile: null,
            InputValues:
            [
                new CanvasWorkbenchInputValue { Key = "jsonPath", Value = "$.route" },
                new CanvasWorkbenchInputValue { Key = "caseValues", Value = "alpha, beta" },
                new CanvasWorkbenchInputValue { Key = "defaultLabel", Value = "DEFAULT" }
            ]);

        await cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnCreateAction(SerializationPersistencePack.Serialize(request)));

        cut.WaitForAssertion(() =>
        {
            var switchNode = cut.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes.Single(node => node.Title == "SWITCH");
            Assert.Contains(switchNode.ContextActions, action =>
                action.Children.Any(child => child.ActionId == "workflow-decision:add-route"));
        });

        var nodeId = cut.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes.Single(node => node.Title == "SWITCH").Id;
        await cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnContextAction(nodeId, "workflow-decision:add-route", 0, 0));

        cut.WaitForElement("[data-testid='workflow-canvas-decision-route-editor']");
        cut.Find("[data-testid='workflow-canvas-decision-route-label']").Change("Case Gamma");
        cut.Find("[data-testid='workflow-canvas-decision-route-expected-value']").Change("gamma");
        cut.Find("[data-testid='workflow-canvas-decision-save-route']").Click();

        cut.WaitForAssertion(() =>
        {
            var surface = cut.FindComponent<CanvasWorkbench>().Instance.Surface;
            Assert.Contains(surface.Links, link => link.Label == "Case Gamma");
            Assert.Contains(surface.Nodes, node => node.Title == "Case Gamma");
            Assert.Contains("4 route(s)", cut.Markup);
        });

        cut.FindAll("[data-testid='workflow-canvas-decision-edit-route']").First().Click();
        cut.WaitForElement("[data-testid='workflow-canvas-decision-route-editor']");
        cut.Find("[data-testid='workflow-canvas-decision-route-label']").Change("Case Alpha Updated");
        cut.Find("[data-testid='workflow-canvas-decision-save-route']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(cut.FindComponent<CanvasWorkbench>().Instance.Surface.Links, link => link.Label == "Case Alpha Updated");
            Assert.Contains("Case Alpha Updated", cut.Markup);
        });
    }

    [Fact]
    public async Task Workflow_example_seed_creates_production_examples_when_enabled()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"workflow-example-seed-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);
        var store = new InMemoryWorkflowCatalogStore();
        var catalogService = new InMemoryWorkflowCatalogService(store, new WorkflowDefinitionValidator());
        var templatePack = new WorkflowTemplatePackLoader().Load();
        var physicalPathPolicyFactory = new PhysicalFileSystemPathPolicyFactory();
        var seeder = new WorkflowExampleCatalogSeedService(
            catalogService,
            catalogService,
            catalogService,
            new WorkspaceFileService(workspaceRoot, physicalPathPolicyFactory),
            new WorkspacePathResolutionService(workspaceRoot, physicalPathPolicyFactory),
            new ClosedXmlSpreadsheetDocumentService(),
            Options.Create(new WorkflowExampleCatalogSeedOptions
            {
                Enabled = true,
                SeedSampleWorkspaceFiles = true
            }),
            NullLogger<WorkflowExampleCatalogSeedService>.Instance);

        try
        {
            await seeder.EnsureSeededAsync();
        }
        finally
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }

        var definitions = await catalogService.ListDefinitionsAsync();
        var examples = definitions
            .Where(item => item.Name.StartsWith("Example:", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(templatePack.Workflows.Count, examples.Length);

        var components = await catalogService.ListComponentsAsync();
        Assert.Equal(templatePack.Workflows.Count, components.Count(component => component.Name.StartsWith("Example LLM:", StringComparison.Ordinal)));
        foreach (var example in examples)
        {
            var detail = await catalogService.GetDefinitionAsync(example.Id);
            Assert.NotNull(detail);
            Assert.True(detail!.Validation.Succeeded, string.Join("; ", detail.Validation.Issues.Select(issue => issue.Message)));
        }

        var invoice = Assert.Single(examples, item => item.Name == "Example: Invoice Workbook Risk Switch");
        var invoiceDetail = await catalogService.GetDefinitionAsync(invoice.Id);
        Assert.NotNull(invoiceDetail);
        Assert.True(invoiceDetail!.Validation.Succeeded);
        Assert.Contains(invoiceDetail.Definition.Graph.Edges, edge => edge.Routing.Kind == WorkflowRouteKind.SwitchDefault);

        var fanOut = Assert.Single(examples, item => item.Name == "Example: Pipeline Workbook Fan-out");
        var fanOutDetail = await catalogService.GetDefinitionAsync(fanOut.Id);
        Assert.NotNull(fanOutDetail);
        Assert.True(fanOutDetail!.Validation.Succeeded);
        Assert.Contains(fanOutDetail.Definition.Graph.Edges, edge => edge.Routing.Kind == WorkflowRouteKind.FanOutSelector);

        var internet = Assert.Single(examples, item => item.Name == "Example: Internet Research Capture");
        var internetDetail = await catalogService.GetDefinitionAsync(internet.Id);
        Assert.NotNull(internetDetail);
        Assert.True(internetDetail!.Validation.Succeeded);
        Assert.Contains(internetDetail.Definition.Graph.Nodes, node =>
            node.Settings.ExecutorId == WorkflowExecutorIds.HttpFetch &&
            node.Settings.ExecutorSettingsJson.Contains("urlJsonPath", StringComparison.Ordinal));

        var folderReport = Assert.Single(examples, item => item.Name == "Example: Local Folder Summary Markdown Report");
        var folderReportDetail = await catalogService.GetDefinitionAsync(folderReport.Id);
        Assert.NotNull(folderReportDetail);
        Assert.True(folderReportDetail!.Validation.Succeeded);
        Assert.Contains(folderReportDetail.Definition.Graph.Nodes, node => node.Settings.ExecutorId == WorkflowExecutorIds.MarkdownRender);

        var taskTransform = Assert.Single(examples, item => item.Name == "Example: JSON Transform Project Task Creation");
        var taskTransformDetail = await catalogService.GetDefinitionAsync(taskTransform.Id);
        Assert.NotNull(taskTransformDetail);
        Assert.True(taskTransformDetail!.Validation.Succeeded);
        Assert.Contains(taskTransformDetail.Definition.Graph.Nodes, node => node.Settings.ExecutorId == WorkflowExecutorIds.JsonTransform);
    }

    [Fact]
    public async Task Workflow_example_seed_preserves_non_managed_definitions_with_template_names()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"workflow-example-seed-preserve-{Guid.NewGuid():N}");
        var store = new InMemoryWorkflowCatalogStore();
        var catalogService = new InMemoryWorkflowCatalogService(store, new WorkflowDefinitionValidator());
        var templatePack = new WorkflowTemplatePackLoader().Load();
        var template = templatePack.Workflows[0];
        var component = await catalogService.SaveComponentAsync(new LlmCallComponentSaveRequest(
            Id: null,
            Name: "User component",
            ProviderProfileId: null,
            Model: "gpt-5-mini",
            WorkflowModality.Text,
            new WorkflowModelSettings(0.2, 256, RequireJsonOutput: false, ResponseFormatJsonSchema: string.Empty),
            "User-owned workflow component.",
            WorkflowValueShape.Text,
            WorkflowValueShape.Text,
            AgentPermissionsPolicy.Default));
        var userDescription = "User-owned workflow. No managed seed marker.";
        var userDefinition = await catalogService.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            Name: $"{templatePack.Manifest.DefinitionNamePrefix}{template.Name}",
            Description: userDescription,
            WorkflowLifecycleStatus.Active,
            CreateStarterGraph(component.Id),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false)));
        var physicalPathPolicyFactory = new PhysicalFileSystemPathPolicyFactory();
        var seeder = new WorkflowExampleCatalogSeedService(
            catalogService,
            catalogService,
            catalogService,
            new WorkspaceFileService(workspaceRoot, physicalPathPolicyFactory),
            new WorkspacePathResolutionService(workspaceRoot, physicalPathPolicyFactory),
            new ClosedXmlSpreadsheetDocumentService(),
            Options.Create(new WorkflowExampleCatalogSeedOptions
            {
                Enabled = true,
                SeedSampleWorkspaceFiles = false
            }),
            NullLogger<WorkflowExampleCatalogSeedService>.Instance);

        try
        {
            await seeder.EnsureSeededAsync();
        }
        finally
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }

        var preserved = await catalogService.GetDefinitionAsync(userDefinition.Id);
        var definitions = await catalogService.ListDefinitionsAsync();

        Assert.NotNull(preserved);
        Assert.Equal(userDescription, preserved!.Definition.Description);
        Assert.DoesNotContain(templatePack.Manifest.SeedMarker, preserved.Definition.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(templatePack.Workflows.Count, definitions.Count(item => item.Name.StartsWith("Example:", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Workflow_history_paginates_runs_and_events_and_moves_full_payload_to_detail_dialog()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(RegisterDeterministicWorkflowLlmInvoker);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var runStore = harness.Context.Services.GetRequiredService<IWorkflowRunStore>();
        var definition = await CreateHistoryDefinitionAsync(catalogService);
        var newestRunId = new WorkflowRunId(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));
        var now = DateTimeOffset.UtcNow;

        for (var index = 0; index < 12; index++)
        {
            var runId = index == 0
                ? newestRunId
                : new WorkflowRunId(Guid.Parse($"00000000-0000-0000-0000-{index:x12}"));
            await runStore.SaveRunAsync(new WorkflowRunSnapshot(
                runId,
                definition.Id,
                definition.VersionId,
                WorkflowRunState.Completed,
                WorkflowRuntimeBackendKind.InProcess,
                $"history-run-{index}",
                $"History run {index} completed with compact card coverage.",
                now.AddMinutes(-index),
                now.AddMinutes(-index)));
        }

        var hiddenTail = "UNIQUE_FULL_EVENT_TAIL";
        for (var index = 0; index < 11; index++)
        {
            var message = index == 0
                ? $"Executor completed with a long payload summary {new string('x', 180)} {hiddenTail}"
                : $"Executor event {index}";
            await runStore.SaveEventAsync(new WorkflowEventRecord(
                Guid.Parse($"00000000-0000-0000-0000-{index + 1:x12}"),
                newestRunId,
                index % 2 == 0 ? WorkflowEventKind.ExecutorCompleted : WorkflowEventKind.SuperStep,
                new WorkflowNodeId("history-node"),
                message,
                $"{{\"index\":{index},\"marker\":\"payload-{index}\"}}",
                now.AddSeconds(index)));
        }

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.Render<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-tab-history']");
        cut.Find("[data-testid='workflows-tab-history']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(8, cut.FindAll("[data-testid='workflows-run-item']").Count);
            Assert.Equal(8, cut.FindAll("[data-testid='workflows-run-event']").Count);
            Assert.Contains("Page 1 of 2 - 12 runs", cut.Find("[data-testid='workflows-run-pager']").TextContent);
            Assert.Contains("Page 1 of 2 - 11 events", cut.Find("[data-testid='workflows-event-pager']").TextContent);
            Assert.DoesNotContain(hiddenTail, cut.Markup);
        });

        cut.FindAll("[data-testid='workflows-event-detail']").First().Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("workflows-event-detail-dialog", cut.Markup);
            Assert.Contains(hiddenTail, cut.Markup);
            Assert.Contains("payload-0", cut.Markup);
        });

        cut.Find("[data-testid='workflows-event-detail-dialog'] button[aria-label='Close']").Click();
        cut.FindAll("[data-testid='workflows-run-detail']").First().Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("workflows-run-detail-dialog", cut.Markup);
            Assert.Contains("history-run-0", cut.Markup);
        });

        cut.Find("[data-testid='workflows-run-page-next']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Page 2 of 2 - 12 runs", cut.Find("[data-testid='workflows-run-pager']").TextContent);
            Assert.Equal(4, cut.FindAll("[data-testid='workflows-run-item']").Count);
        });
    }

    [Fact]
    public async Task Workflow_history_displays_typed_failure_diagnostic_without_raw_message()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(RegisterDeterministicWorkflowLlmInvoker);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var runStore = harness.Context.Services.GetRequiredService<IWorkflowRunStore>();
        var definition = await CreateHistoryDefinitionAsync(catalogService);
        var runId = WorkflowRunId.New();
        var now = DateTimeOffset.UtcNow;
        var diagnostic = new WorkflowFailureDiagnosticEnvelope(
            WorkflowFailureKind.Executor,
            WorkflowFailureRetryability.RetryableAfterRepair,
            "Executor settings are invalid.",
            "Fix the executor settings JSON for node 'store-project'.",
            "Executor settings parse failure: token=[REDACTED]",
            "corr-workflows-page",
            definition.Id,
            definition.VersionId,
            runId,
            new WorkflowNodeId("store-project"),
            WorkflowExecutorIds.ProjectStructure,
            WorkflowFailureSourceContext.ForExecutor(WorkflowExecutorIds.ProjectStructure),
            now);
        var payloadJson = WorkflowEventPayloads.Serialize(
            WorkflowEventPayloadSource.Runtime,
            "WorkflowExecutorFailed",
            nodeId: new WorkflowNodeId("store-project"),
            executorId: WorkflowExecutorIds.ProjectStructure,
            inlineJson: WorkflowRuntimeFailureDiagnosticMapper.Serialize(diagnostic));

        await runStore.SaveRunAsync(new WorkflowRunSnapshot(
            runId,
            definition.Id,
            definition.VersionId,
            WorkflowRunState.Failed,
            WorkflowRuntimeBackendKind.InProcess,
            "failure-history-run",
            "Workflow executor failed with token=raw-token-value.",
            now,
            now));
        await runStore.SaveEventAsync(new WorkflowEventRecord(
            Guid.NewGuid(),
            runId,
            WorkflowEventKind.ExecutorFailed,
            new WorkflowNodeId("store-project"),
            "Workflow executor failed with token=raw-token-value.",
            payloadJson,
            now));

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.Render<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-tab-history']");
        cut.Find("[data-testid='workflows-tab-history']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Fix the executor settings JSON", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("raw-token-value", cut.Markup, StringComparison.Ordinal);
        });

        cut.Find("[data-testid='workflows-event-detail']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("workflows-event-detail-dialog", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Executor settings parse failure: token=[REDACTED]", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("raw-token-value", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Workflow_canvas_preserves_maximized_state_when_selection_changes()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(RegisterDeterministicWorkflowLlmInvoker);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.Render<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-tab-editor']");
        cut.Find("[data-testid='workflows-tab-editor']").Click();
        cut.WaitForElement("[data-testid='workflow-canvas-editor']");

        FindButtonByTitle(cut, "Maximize canvas").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.True(cut.FindComponent<CanvasWorkbench>().Instance.Surface.UiState.IsMaximized);
        });

        await cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnSelectionChanged("end", "[\"end\"]", 1));

        cut.WaitForAssertion(() =>
        {
            var canvas = cut.FindComponent<CanvasWorkbench>().Instance;
            Assert.True(canvas.Surface.UiState.IsMaximized);
            Assert.Equal(new[] { "end" }, canvas.Surface.UiState.SelectedNodeIds);
        });
    }

    [Fact]
    public async Task Agents_shell_exposes_workflows_page_navigation()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/agents");
        RenderFragment agentsHomePageContent = builder =>
        {
            builder.OpenComponent<AgentsHomePage>(0);
            builder.CloseComponent();
        };
        var cut = harness.Context.Render<AppToolbarActionsTestHost>(parameters => parameters
            .Add(p => p.ChildContent, agentsHomePageContent));
        var agentsHomePage = cut.FindComponent<AgentsHomePage>();

        cut.WaitForElement("[data-testid='agents-shell-open-workflows']");
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("Loading canonical agent runtime", cut.Markup);
        });
        Assert.Contains("Open workflows", cut.Markup, StringComparison.Ordinal);
        var openWorkflows = typeof(AgentsHomePage).GetMethod(
            "OpenWorkflows",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(openWorkflows);
        await cut.InvokeAsync(() => openWorkflows.Invoke(agentsHomePage.Instance, null));

        Assert.EndsWith("/agents/workflows", navigation.Uri, StringComparison.Ordinal);
    }

    private static void RegisterDeterministicWorkflowLlmInvoker(IServiceCollection services)
    {
        services.RemoveAll<IWorkflowLlmComponentInvoker>();
        services.AddScoped<IWorkflowLlmComponentInvoker, DeterministicWorkflowLlmComponentInvoker>();
    }

    private static void RegisterCountingWorkflowComponentLibrary(IServiceCollection services)
    {
        services.AddSingleton<WorkflowComponentLibraryCallCounter>();
        services.RemoveAll<IWorkflowComponentLibraryService>();
        services.AddScoped<IWorkflowComponentLibraryService>(serviceProvider => new CountingWorkflowComponentLibraryService(
            serviceProvider.GetRequiredService<PersistentWorkflowCatalogService>(),
            serviceProvider.GetRequiredService<WorkflowComponentLibraryCallCounter>()));
    }

    private static void RegisterRacingWorkflowCatalog(IServiceCollection services)
    {
        services.RemoveAll<IWorkflowCatalogService>();
        services.AddScoped<RacingWorkflowCatalogService>();
        services.AddScoped<IWorkflowCatalogService>(serviceProvider =>
            serviceProvider.GetRequiredService<RacingWorkflowCatalogService>());
    }

    private static void RegisterCountingWorkflowRunStore(IServiceCollection services)
    {
        services.AddSingleton<WorkflowRunStoreCallCounter>();
        services.RemoveAll<IWorkflowRunStore>();
        services.RemoveAll<IWorkflowArtifactStore>();
        services.RemoveAll<IWorkflowExternalRequestStore>();
        services.RemoveAll<IWorkflowCheckpointStore>();
        services.AddSingleton<CountingWorkflowRunStore>();
        services.AddSingleton<IWorkflowRunStore>(serviceProvider => serviceProvider.GetRequiredService<CountingWorkflowRunStore>());
        services.AddSingleton<IWorkflowArtifactStore>(serviceProvider => serviceProvider.GetRequiredService<CountingWorkflowRunStore>());
        services.AddSingleton<IWorkflowExternalRequestStore>(serviceProvider => serviceProvider.GetRequiredService<CountingWorkflowRunStore>());
        services.AddSingleton<IWorkflowCheckpointStore>(serviceProvider => serviceProvider.GetRequiredService<CountingWorkflowRunStore>());
    }

    private static Task<ComponentTestHarness> CreateInMemoryWorkflowHarnessAsync(
        CanDoItAllTestEnvironment environment,
        Action<IServiceCollection>? configureServices = null)
    {
        var profile = environment.CreateInMemoryProfile("primary");
        return ComponentTestHarness.CreateAsync(configureServices, new TestHarnessOptions
        {
            TestEnvironment = environment,
            ActiveProfile = profile,
            SchemaModules = TestSchemaBootstrapModules.Default
        });
    }

    private static async Task<IRenderedComponent<DialogHost>> PrepareFinalWorkflowPromptAsync(
        ComponentTestHarness harness,
        string title,
        string content)
    {
        var promptGallery = harness.Context.Services.GetRequiredService<IPromptGalleryService>();
        var saveResult = await promptGallery.SaveDraftAsync(new PromptGalleryDraft(
            Id: null,
            ProjectId: null,
            CollectionId: null,
            title,
            "Canonical prompt for a workflow canvas component test.",
            PromptGalleryItemKind.FullPrompt,
            "workflow",
            content,
            Tags: ["workflow"],
            SupportedConsumers: [PromptGalleryConsumer.Workflow]));
        Assert.True(saveResult.IsSuccess);
        var saveReceipt = saveResult.Value;
        var versionResult = await promptGallery.CreateVersionAsync(
            saveReceipt.PromptArtifactId,
            new PromptVersionCreateRequest(
                "Workflow canvas test fixture",
                saveReceipt.UpdatedAtUtc));
        Assert.True(
            versionResult.IsSuccess,
            string.Join(" ", versionResult.Errors.Select(error => $"{error.Code}: {error.Message}")));
        return harness.Context.Render<DialogHost>();
    }

    private static void SelectWorkflowPromptFromGallery(
        IRenderedComponent<IComponent> workflow,
        IRenderedComponent<IComponent> dialogHost,
        string promptTitle)
    {
        workflow.WaitForElement(
            "[data-testid='workflow-canvas-components-window'] [data-testid='prompt-gallery-picker-button']").Click();
        dialogHost.WaitForElement("[data-testid='prompt-gallery-picker-dialog']");
        dialogHost.Find("[data-testid='prompt-gallery-search']").Input(promptTitle);
        dialogHost.WaitForAssertion(() =>
        {
            Assert.Contains(promptTitle, dialogHost.Markup, StringComparison.Ordinal);
            Assert.Single(dialogHost.FindAll("[data-testid='prompt-gallery-select']"));
        });
        dialogHost.Find("[data-testid='prompt-gallery-select']").Click();
    }

    private static void OpenTemplatePreview(IRenderedComponent<IComponent> cut, string templateName)
    {
        cut.WaitForElement("[data-testid='workflows-tab-workflows']");
        cut.Find("[data-testid='workflows-tab-workflows']").Click();
        cut.WaitForElement("[data-testid='workflows-open-template-catalogue']").Click();
        cut.WaitForElement("[data-testid='workflows-template-catalogue-dialog']");
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(
                cut.FindAll("[data-testid='workflows-template-catalogue-item']"),
                item => item.TextContent.Contains(templateName, StringComparison.Ordinal));
        });

        var templateItem = cut.FindAll("[data-testid='workflows-template-catalogue-item']")
            .First(item => item.TextContent.Contains(templateName, StringComparison.Ordinal));
        templateItem.QuerySelector("[data-testid='workflows-template-preview']")
            ?.Click();
    }

    private static async Task SaveTemplateDraftAsync(
        IWorkflowCatalogService catalogService,
        IWorkflowComponentLibraryService componentLibrary,
        WorkflowTemplatePack templatePack,
        WorkflowTemplateDefinition template,
        string name)
    {
        var component = await componentLibrary.SaveComponentAsync(new LlmCallComponentSaveRequest(
            Id: null,
            Name: $"Test LLM: {name}",
            ProviderProfileId: null,
            Model: ManagedSeedProviderFallbacks.OpenAiDefaultModel,
            WorkflowModality.Text,
            templatePack.CreateModelSettings(),
            templatePack.CreateComponentInstructions(template),
            templatePack.JsonShape,
            templatePack.JsonShape,
            AgentPermissionsPolicy.Default));
        await catalogService.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            Name: name,
            Description: template.Description,
            Status: WorkflowLifecycleStatus.Draft,
            Graph: templatePack.CreateGraph(template, component.Id),
            RuntimePolicy: templatePack.RuntimePolicy)
        {
            InputParameters = templatePack.CreateInputParameters(template)
        });
    }

    private static IElement FindButtonByTitle(IRenderedComponent<IComponent> cut, string title)
        => cut.FindAll("button")
            .First(button => button.GetAttribute("title")?.Contains(title, StringComparison.Ordinal) == true);

    private static Task InvokeSelectDefinitionAsync(WorkflowsPage page, WorkflowId definitionId)
    {
        var method = typeof(WorkflowsPage).GetMethod(
            "SelectDefinitionAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return Assert.IsAssignableFrom<Task>(method?.Invoke(page, [definitionId]));
    }

    private static Task InvokeLoadRunsPageAsync(WorkflowsPage page, WorkflowId definitionId)
    {
        var method = typeof(WorkflowsPage).GetMethod(
            "LoadRunsPageAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return Assert.IsAssignableFrom<Task>(method?.Invoke(page, [definitionId, 0, null, null]));
    }

    private static Task InvokeSelectRunAsync(WorkflowsPage page, WorkflowRunId runId)
    {
        var method = typeof(WorkflowsPage).GetMethod(
            "SelectRunAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return Assert.IsAssignableFrom<Task>(method?.Invoke(page, [runId, true, null, null]));
    }

    private static WorkflowDefinition? ReadSelectedDefinition(WorkflowsPage page)
    {
        var field = typeof(WorkflowsPage).GetField(
            "selectedDefinition",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return Assert.IsType<WorkflowDefinition>(field?.GetValue(page));
    }

    private static WorkflowDefinition? ReadSelectedDefinitionOrNull(WorkflowsPage page)
    {
        var field = typeof(WorkflowsPage).GetField(
            "selectedDefinition",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return field?.GetValue(page) as WorkflowDefinition;
    }

    private static WorkflowRunSnapshot? ReadSelectedRun(WorkflowsPage page)
    {
        var field = typeof(WorkflowsPage).GetField(
            "selectedRun",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return Assert.IsType<WorkflowRunSnapshot>(field?.GetValue(page));
    }

    private static WorkflowRunSnapshot? ReadSelectedRunOrNull(WorkflowsPage page)
    {
        var field = typeof(WorkflowsPage).GetField(
            "selectedRun",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return field?.GetValue(page) as WorkflowRunSnapshot;
    }

    private static IReadOnlyList<WorkflowRunSnapshot> ReadRuns(WorkflowsPage page)
    {
        var field = typeof(WorkflowsPage).GetField(
            "runs",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return Assert.IsAssignableFrom<IReadOnlyList<WorkflowRunSnapshot>>(field?.GetValue(page));
    }

    private static AgentChatContextAccessState ReadAgentChatAccessState(WorkflowsPage page)
    {
        var property = typeof(WorkflowsPage).GetProperty(
            "AgentChatAccessState",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return Assert.IsType<AgentChatContextAccessState>(property?.GetValue(page));
    }

    private static AgentChatContextSurface ReadAgentChatSurface(WorkflowsPage page)
    {
        var property = typeof(WorkflowsPage).GetProperty(
            "AgentChatSurface",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return Assert.IsType<AgentChatContextSurface>(property?.GetValue(page));
    }

    private static WorkflowRunSnapshot CreateRun(
        WorkflowDefinition definition,
        string backendRunId,
        DateTimeOffset createdAtUtc)
        => new(
            WorkflowRunId.New(),
            definition.Id,
            definition.VersionId,
            WorkflowRunState.Completed,
            WorkflowRuntimeBackendKind.InProcess,
            backendRunId,
            $"{backendRunId} completed.",
            createdAtUtc,
            createdAtUtc);

    private static Task ClickWorkflowCanvasTabAsync(IRenderedComponent<IComponent> cut, string testId)
        => cut.InvokeAsync(() =>
        {
            var tab = cut.Find($"[data-testid='{testId}']");
            if (string.Equals(tab.TagName, "button", StringComparison.OrdinalIgnoreCase))
            {
                tab.Click();
                return;
            }

            var button = tab.QuerySelector("button") ??
                         tab.QuerySelector("[role='tab']") ??
                         throw new InvalidOperationException($"Workflow canvas tab '{testId}' did not render a clickable tab element.");
            button.Click();
        });

    private static void ClickTabButton(IRenderedComponent<IComponent> cut, string text)
    {
        var button = cut.FindAll("button")
            .First(button => button.TextContent.Contains(text, StringComparison.OrdinalIgnoreCase));
        button.Click();
    }

    private static IElement EnsureWorkflowToolboxVisible(IRenderedComponent<IComponent> cut)
    {
        const string searchSelector = "input[placeholder='Search nodes, executors, files, HTTP, spreadsheets']";
        var inputs = cut.FindAll(searchSelector);
        if (inputs.Count > 0)
        {
            return inputs.First();
        }

        cut.Find("[data-testid='workflow-canvas-toggle-toolbox']").Click();
        return cut.WaitForElement(searchSelector);
    }

    private static void AssertNoWorkflowPageError(IRenderedComponent<IComponent> cut)
    {
        var errors = cut.FindAll("[data-testid='workflows-error']");
        Assert.True(errors.Count == 0, string.Join(" | ", errors.Select(error => error.TextContent.Trim())));
    }

    private static WorkflowDefinition CreateCanvasLoadDefinition(
        WorkflowId workflowId,
        WorkflowVersionId versionId,
        string name,
        bool includeWorkNode,
        DateTimeOffset timestamp)
    {
        var start = new WorkflowNodeId("start");
        var work = new WorkflowNodeId("work");
        var end = new WorkflowNodeId("end");
        var graph = includeWorkNode
            ? new WorkflowGraph(
                start,
                [
                    CreateHistoryNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                    CreateHistoryNode(work, WorkflowNodeKind.StrictLogic, inputShape: WorkflowValueShape.Text, resultShape: WorkflowValueShape.Text),
                    CreateHistoryNode(end, WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
                ],
                [
                    new WorkflowEdge(
                        new WorkflowEdgeId("start-to-work"),
                        start,
                        SourcePortId: null,
                        work,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty),
                    new WorkflowEdge(
                        new WorkflowEdgeId("work-to-end"),
                        work,
                        SourcePortId: null,
                        end,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty)
                ])
            : new WorkflowGraph(
                start,
                [
                    CreateHistoryNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                    CreateHistoryNode(end, WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
                ],
                [
                    new WorkflowEdge(
                        new WorkflowEdgeId("start-to-end"),
                        start,
                        SourcePortId: null,
                        end,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty)
                ]);

        return new WorkflowDefinition(
            workflowId,
            versionId,
            name,
            "Workflow definition used to verify selected editor loading.",
            WorkflowLifecycleStatus.Active,
            graph,
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            timestamp,
            timestamp);
    }

    private static Task<WorkflowDefinition> CreateCanvasLoadDefinitionAsync(IWorkflowCatalogService catalogService)
    {
        var start = new WorkflowNodeId("start");
        var work = new WorkflowNodeId("work");
        var end = new WorkflowNodeId("end");
        return catalogService.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            Name: "Editor full load workflow",
            Description: "Workflow definition used to verify selected editor loading.",
            WorkflowLifecycleStatus.Active,
            new WorkflowGraph(
                start,
                [
                    CreateHistoryNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                    CreateHistoryNode(work, WorkflowNodeKind.StrictLogic, inputShape: WorkflowValueShape.Text, resultShape: WorkflowValueShape.Text),
                    CreateHistoryNode(end, WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
                ],
                [
                    new WorkflowEdge(
                        new WorkflowEdgeId("start-to-work"),
                        start,
                        SourcePortId: null,
                        work,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty),
                    new WorkflowEdge(
                        new WorkflowEdgeId("work-to-end"),
                        work,
                        SourcePortId: null,
                        end,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty)
                ]),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false)));
    }

    private static Task<WorkflowDefinition> CreateHistoryDefinitionAsync(IWorkflowCatalogService catalogService)
    {
        var start = new WorkflowNodeId("start");
        var end = new WorkflowNodeId("end");
        return catalogService.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            Name: "Paged history workflow",
            Description: "Workflow definition used to verify bounded history paging.",
            WorkflowLifecycleStatus.Active,
            new WorkflowGraph(
                start,
                [
                    CreateHistoryNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                    CreateHistoryNode(end, WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
                ],
                [
                    new WorkflowEdge(
                        new WorkflowEdgeId("start-to-end"),
                        start,
                        SourcePortId: null,
                        end,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty)
                ]),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false)));
    }

    private static WorkflowDefinition CreateProjectStructurePreviewDefinition()
    {
        var start = new WorkflowNodeId("start");
        var store = new WorkflowNodeId("store");
        var end = new WorkflowNodeId("end");
        var now = DateTimeOffset.UtcNow;
        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Project structure preview target",
            "Workflow used to verify preview input prompting.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(
                start,
                [
                    CreateHistoryNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                    new WorkflowNode(
                        store,
                        WorkflowNodeKind.Executor,
                        "Store preview output",
                        [],
                        new WorkflowNodeSettings(
                            ComponentId: null,
                            AgentId: null,
                            SubworkflowId: null,
                            ExternalRequestKind: null,
                            Instructions: "Create a project asset during preview.",
                            InputShape: WorkflowValueShape.Text,
                            ResultShape: WorkflowValueShape.Text)
                        {
                            ExecutorId = WorkflowExecutorIds.ProjectStructure,
                            ExecutorSettingsJson = "{\"operation\":\"CreateAsset\",\"title\":\"Preview artifact\",\"contentFromInput\":true}"
                        }),
                    CreateHistoryNode(end, WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
                ],
                [
                    new WorkflowEdge(
                        new WorkflowEdgeId("start-to-store"),
                        start,
                        SourcePortId: null,
                        store,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty),
                    new WorkflowEdge(
                        new WorkflowEdgeId("store-to-end"),
                        store,
                        SourcePortId: null,
                        end,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty)
                ]),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            now,
            now);
    }

    private static WorkflowDefinition CreatePreviewProgressDefinition()
    {
        var start = new WorkflowNodeId("start");
        var work = new WorkflowNodeId("work");
        var end = new WorkflowNodeId("end");
        var now = DateTimeOffset.UtcNow;
        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Preview progress workflow",
            "Workflow used to verify canvas selection follows preview execution.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(
                start,
                [
                    CreateHistoryNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                    CreateHistoryNode(work, WorkflowNodeKind.StrictLogic, inputShape: WorkflowValueShape.Text, resultShape: WorkflowValueShape.Text),
                    CreateHistoryNode(end, WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
                ],
                [
                    new WorkflowEdge(
                        new WorkflowEdgeId("start-to-work"),
                        start,
                        SourcePortId: null,
                        work,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty),
                    new WorkflowEdge(
                        new WorkflowEdgeId("work-to-end"),
                        work,
                        SourcePortId: null,
                        end,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty)
                ]),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            now,
            now);
    }

    private static WorkflowDefinition CreateWorkflowUsageStatsDefinition(WorkflowComponentId componentId)
    {
        var start = new WorkflowNodeId("start");
        var firstLlm = new WorkflowNodeId("llm-a");
        var firstExecutor = new WorkflowNodeId("executor-a");
        var secondLlm = new WorkflowNodeId("llm-b");
        var secondExecutor = new WorkflowNodeId("executor-b");
        var end = new WorkflowNodeId("end");
        var now = DateTimeOffset.UtcNow;

        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Workflow usage stats target",
            "Workflow used to verify canvas stat counts.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(
                start,
                [
                    CreateHistoryNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                    CreateLlmUsageNode(firstLlm, componentId),
                    CreateExecutorUsageNode(firstExecutor),
                    CreateLlmUsageNode(secondLlm, componentId),
                    CreateExecutorUsageNode(secondExecutor),
                    CreateHistoryNode(end, WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
                ],
                [
                    CreateWorkflowEdge("start-to-llm-a", start, firstLlm),
                    CreateWorkflowEdge("llm-a-to-executor-a", firstLlm, firstExecutor),
                    CreateWorkflowEdge("executor-a-to-llm-b", firstExecutor, secondLlm),
                    CreateWorkflowEdge("llm-b-to-executor-b", secondLlm, secondExecutor),
                    CreateWorkflowEdge("executor-b-to-end", secondExecutor, end)
                ]),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            now,
            now);
    }

    private static WorkflowNode CreateLlmUsageNode(WorkflowNodeId id, WorkflowComponentId componentId)
        => new(
            id,
            WorkflowNodeKind.LlmCall,
            id.Value,
            [],
            new WorkflowNodeSettings(
                componentId,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: "Summarize the current payload.",
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text));

    private static WorkflowNode CreateExecutorUsageNode(WorkflowNodeId id)
        => new(
            id,
            WorkflowNodeKind.Executor,
            id.Value,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: "Store the current payload.",
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text)
            {
                ExecutorId = WorkflowExecutorIds.ProjectStructure,
                ExecutorSettingsJson = "{}"
            });

    private static WorkflowEdge CreateWorkflowEdge(
        string id,
        WorkflowNodeId sourceNodeId,
        WorkflowNodeId targetNodeId)
        => new(
            new WorkflowEdgeId(id),
            sourceNodeId,
            SourcePortId: null,
            targetNodeId,
            TargetPortId: null,
            WorkflowEdgeKind.Direct,
            ConditionExpression: string.Empty);

    private static LlmCallComponent CreateWorkflowComponent(string name)
    {
        var now = DateTimeOffset.UtcNow;

        return new LlmCallComponent(
            WorkflowComponentId.New(),
            name,
            ProviderProfileId: null,
            "gpt-5.4",
            WorkflowModality.Text,
            new WorkflowModelSettings(
                Temperature: 0.2,
                MaxOutputTokens: 800,
                RequireJsonOutput: false,
                ResponseFormatJsonSchema: string.Empty),
            "Summarize the input.",
            WorkflowValueShape.Text,
            WorkflowValueShape.Text,
            AgentPermissionsPolicy.Default,
            now,
            now);
    }

    private static WorkflowNode CreateHistoryNode(
        WorkflowNodeId id,
        WorkflowNodeKind kind,
        WorkflowValueShape? inputShape = null,
        WorkflowValueShape? resultShape = null)
        => new(
            id,
            kind,
            id.Value,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: inputShape ?? WorkflowValueShape.Text,
                ResultShape: resultShape ?? WorkflowValueShape.Text));

    private static WorkflowGraph CreateStarterGraph(WorkflowComponentId componentId)
    {
        var start = new WorkflowNodeId("start");
        var llm = new WorkflowNodeId("llm");
        var end = new WorkflowNodeId("end");
        return new WorkflowGraph(
            start,
            [
                CreateHistoryNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                new WorkflowNode(
                    llm,
                    WorkflowNodeKind.LlmCall,
                    "LLM",
                    [],
                    new WorkflowNodeSettings(
                        componentId,
                        AgentId: null,
                        SubworkflowId: null,
                        ExternalRequestKind: null,
                        Instructions: "Summarize.",
                        InputShape: WorkflowValueShape.Text,
                        ResultShape: WorkflowValueShape.Text)),
                CreateHistoryNode(end, WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
            ],
            [
                new WorkflowEdge(
                    new WorkflowEdgeId("start-to-llm"),
                    start,
                    SourcePortId: null,
                    llm,
                    TargetPortId: null,
                    WorkflowEdgeKind.Direct,
                    ConditionExpression: string.Empty),
                new WorkflowEdge(
                    new WorkflowEdgeId("llm-to-end"),
                    llm,
                    SourcePortId: null,
                    end,
                    TargetPortId: null,
                    WorkflowEdgeKind.Direct,
                    ConditionExpression: string.Empty)
            ]);
    }

    private sealed class RacingWorkflowCatalogService(
        PersistentWorkflowCatalogService inner) : IWorkflowCatalogService
    {
        private readonly Dictionary<WorkflowId, PendingDefinitionRequest> pendingRequests = [];

        public void Delay(params WorkflowId[] definitionIds)
        {
            foreach (var definitionId in definitionIds)
            {
                pendingRequests[definitionId] = new PendingDefinitionRequest();
            }
        }

        public Task WaitForRequestAsync(WorkflowId definitionId)
            => GetPendingRequest(definitionId).Started.Task;

        public void Complete(WorkflowId definitionId)
            => GetPendingRequest(definitionId).Completion.TrySetResult(null);

        public void Fail(WorkflowId definitionId)
            => GetPendingRequest(definitionId).Completion.TrySetResult(
                new InvalidOperationException($"Delayed workflow '{definitionId}' failed."));

        public Task<IReadOnlyList<WorkflowCatalogItem>> ListDefinitionsAsync(
            CancellationToken cancellationToken = default)
            => inner.ListDefinitionsAsync(cancellationToken);

        public async Task<WorkflowDefinitionDetail?> GetDefinitionAsync(
            WorkflowId workflowId,
            WorkflowVersionId? versionId = null,
            CancellationToken cancellationToken = default)
        {
            if (pendingRequests.TryGetValue(workflowId, out var pendingRequest))
            {
                pendingRequest.Started.TrySetResult();
                var exception = await pendingRequest.Completion.Task.WaitAsync(cancellationToken);
                if (exception is not null)
                {
                    throw exception;
                }
            }

            return await inner.GetDefinitionAsync(workflowId, versionId, cancellationToken);
        }

        public Task<WorkflowDefinitionDetail?> GetLatestDefinitionByStatusAsync(
            WorkflowId workflowId,
            WorkflowLifecycleStatus status,
            CancellationToken cancellationToken = default)
            => inner.GetLatestDefinitionByStatusAsync(workflowId, status, cancellationToken);

        public Task<WorkflowDefinition> SaveDefinitionAsync(
            WorkflowDefinitionSaveRequest request,
            CancellationToken cancellationToken = default)
            => inner.SaveDefinitionAsync(request, cancellationToken);

        public Task<WorkflowDefinition> ChangeDefinitionStatusAsync(
            WorkflowDefinitionStatusChangeRequest request,
            CancellationToken cancellationToken = default)
            => inner.ChangeDefinitionStatusAsync(request, cancellationToken);

        public Task<WorkflowDefinitionExportEnvelope?> ExportDefinitionAsync(
            WorkflowId workflowId,
            WorkflowVersionId? versionId = null,
            CancellationToken cancellationToken = default)
            => inner.ExportDefinitionAsync(workflowId, versionId, cancellationToken);

        public Task<WorkflowDefinition> ImportDefinitionAsync(
            WorkflowDefinitionImportRequest request,
            CancellationToken cancellationToken = default)
            => inner.ImportDefinitionAsync(request, cancellationToken);

        public Task DeleteDefinitionAsync(
            WorkflowId workflowId,
            CancellationToken cancellationToken = default)
            => inner.DeleteDefinitionAsync(workflowId, cancellationToken);

        public Task<WorkflowValidationResult> ValidateDefinitionAsync(
            WorkflowDefinition definition,
            CancellationToken cancellationToken = default)
            => inner.ValidateDefinitionAsync(definition, cancellationToken);

        private PendingDefinitionRequest GetPendingRequest(WorkflowId definitionId)
            => pendingRequests.TryGetValue(definitionId, out var pendingRequest)
                ? pendingRequest
                : throw new InvalidOperationException($"Workflow '{definitionId}' is not delayed.");

        private sealed class PendingDefinitionRequest
        {
            public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource<Exception?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    private sealed class BlockingInitialWorkflowCatalogService(
        PersistentWorkflowCatalogService inner) : IWorkflowCatalogService
    {
        private readonly TaskCompletionSource initialListStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource initialListCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitForInitialListRequestAsync()
            => initialListStarted.Task;

        public void CompleteInitialListRequest()
            => initialListCompletion.TrySetResult();

        public async Task<IReadOnlyList<WorkflowCatalogItem>> ListDefinitionsAsync(
            CancellationToken cancellationToken = default)
        {
            initialListStarted.TrySetResult();
            await initialListCompletion.Task.WaitAsync(cancellationToken);
            return await inner.ListDefinitionsAsync(cancellationToken);
        }

        public Task<WorkflowDefinitionDetail?> GetDefinitionAsync(
            WorkflowId workflowId,
            WorkflowVersionId? versionId = null,
            CancellationToken cancellationToken = default)
            => inner.GetDefinitionAsync(workflowId, versionId, cancellationToken);

        public Task<WorkflowDefinitionDetail?> GetLatestDefinitionByStatusAsync(
            WorkflowId workflowId,
            WorkflowLifecycleStatus status,
            CancellationToken cancellationToken = default)
            => inner.GetLatestDefinitionByStatusAsync(workflowId, status, cancellationToken);

        public Task<WorkflowDefinition> SaveDefinitionAsync(
            WorkflowDefinitionSaveRequest request,
            CancellationToken cancellationToken = default)
            => inner.SaveDefinitionAsync(request, cancellationToken);

        public Task<WorkflowDefinition> ChangeDefinitionStatusAsync(
            WorkflowDefinitionStatusChangeRequest request,
            CancellationToken cancellationToken = default)
            => inner.ChangeDefinitionStatusAsync(request, cancellationToken);

        public Task<WorkflowDefinitionExportEnvelope?> ExportDefinitionAsync(
            WorkflowId workflowId,
            WorkflowVersionId? versionId = null,
            CancellationToken cancellationToken = default)
            => inner.ExportDefinitionAsync(workflowId, versionId, cancellationToken);

        public Task<WorkflowDefinition> ImportDefinitionAsync(
            WorkflowDefinitionImportRequest request,
            CancellationToken cancellationToken = default)
            => inner.ImportDefinitionAsync(request, cancellationToken);

        public Task DeleteDefinitionAsync(
            WorkflowId workflowId,
            CancellationToken cancellationToken = default)
            => inner.DeleteDefinitionAsync(workflowId, cancellationToken);

        public Task<WorkflowValidationResult> ValidateDefinitionAsync(
            WorkflowDefinition definition,
            CancellationToken cancellationToken = default)
            => inner.ValidateDefinitionAsync(definition, cancellationToken);
    }

    private sealed class RecordingWorkflowCuratorChatLauncher : IAgentChatLauncher
    {
        private readonly TaskCompletionSource startObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource startRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<Guid> StartedAgentIds { get; } = [];

        public bool HoldStartOpen { get; set; }

        public void ShowCatalog(AgentChatCatalogTab tab = AgentChatCatalogTab.Agents)
        {
        }

        public async Task<ActiveAgentChat> StartNewChatAsync(
            Guid agentId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartedAgentIds.Add(agentId);
            startObserved.TrySetResult();
            if (HoldStartOpen)
            {
                await startRelease.Task.WaitAsync(cancellationToken);
            }

            return CreateActiveChat(agentId, chatSessionId: null);
        }

        public Task WaitForStartAsync()
            => startObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));

        public void ReleaseStart()
            => startRelease.TrySetResult();

        public Task<ActiveAgentChat> OpenChatAsync(
            Guid agentId,
            Guid chatSessionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(CreateActiveChat(agentId, chatSessionId));

        private static ActiveAgentChat CreateActiveChat(Guid agentId, Guid? chatSessionId)
        {
            var now = DateTimeOffset.UtcNow;
            return new ActiveAgentChat(
                AgentChatHandleId.Create(),
                new AgentChatIdentity(agentId, "Workflow Curator Agent", "Workflow specialist", null),
                chatSessionId,
                ActiveAgentChatVisibility.Visible,
                ActiveAgentChatRunState.Idle,
                now,
                now,
                HiddenAtUtc: null);
        }
    }

    private class MissingWorkflowCuratorWorkspaceProxy : DispatchProxy
    {
        public int ListAgentsCallCount { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name is "add_ExecutionUpdated" or "remove_ExecutionUpdated")
            {
                return null;
            }

            if (targetMethod?.Name == nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync))
            {
                ListAgentsCallCount++;
                return Task.FromResult<IReadOnlyList<AgentDefinition>>([]);
            }

            throw new InvalidOperationException(
                $"Workspace service member '{targetMethod?.Name}' was not expected in this component test.");
        }
    }

    private sealed class RacingWorkflowRuntimeManager(params WorkflowRunSnapshot[] runs) : IWorkflowRuntimeManager
    {
        private readonly IReadOnlyDictionary<WorkflowRunId, WorkflowRunSnapshot> runsById = runs.ToDictionary(run => run.RunId);
        private readonly Dictionary<WorkflowRunId, PendingRunRequest> pendingRuns = [];

        public void DelayRuns(params WorkflowRunId[] runIds)
        {
            foreach (var runId in runIds)
            {
                pendingRuns[runId] = new PendingRunRequest();
            }
        }

        public Task WaitForRunRequestAsync(WorkflowRunId runId)
            => GetPendingRun(runId).Started.Task;

        public void CompleteRun(WorkflowRunId runId)
            => GetPendingRun(runId).Completion.TrySetResult();

        public Task<WorkflowRunSnapshot> StartAsync(
            WorkflowDefinition definition,
            WorkflowRunStartRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async Task<WorkflowRunSnapshot?> GetRunAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
        {
            if (pendingRuns.TryGetValue(runId, out var pendingRequest))
            {
                pendingRequest.Started.TrySetResult();
                await pendingRequest.Completion.Task.WaitAsync(cancellationToken);
            }

            return runsById.GetValueOrDefault(runId);
        }

        public Task<IReadOnlyList<WorkflowRunSnapshot>> ListRunsAsync(
            WorkflowId? workflowId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WorkflowRunSnapshot>>(runsById.Values
                .Where(run => !workflowId.HasValue || run.WorkflowId == workflowId.Value)
                .ToList());

        public Task<IReadOnlyList<WorkflowEventRecord>> ListEventsAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WorkflowEventRecord>>([]);

        public Task<IReadOnlyList<WorkflowCheckpointRecord>> ListCheckpointsAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WorkflowCheckpointRecord>>([]);

        public Task<WorkflowListPage<WorkflowEventRecord>> ListEventPageAsync(
            WorkflowEventPageRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowRunSnapshot> CancelAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowRunCancellationResult> RequestCancellationAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowRunSnapshot> RespondToExternalRequestAsync(
            WorkflowExternalRequestId requestId,
            string responseJson,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowExternalResponseResult> SubmitExternalResponseAsync(
            WorkflowExternalRequestId requestId,
            string responseJson,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        private PendingRunRequest GetPendingRun(WorkflowRunId runId)
            => pendingRuns.TryGetValue(runId, out var pendingRequest)
                ? pendingRequest
                : throw new InvalidOperationException($"Workflow run '{runId}' is not delayed.");

        private sealed class PendingRunRequest
        {
            public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    private sealed class DeterministicWorkflowLlmComponentInvoker : IWorkflowLlmComponentInvoker
    {
        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowDefinition definition,
            WorkflowNode node,
            LlmCallComponent component,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
                node.Id,
                $"Workflow LLM test output: {input.PayloadJson}",
                component.ResultShape));
        }
    }

    private sealed class WorkflowComponentLibraryCallCounter
    {
        private int listComponentsCount;
        private int listProviderOptionsCount;

        public int ListComponentsCount => listComponentsCount;

        public int ListProviderOptionsCount => listProviderOptionsCount;

        public void IncrementListComponents()
        {
            Interlocked.Increment(ref listComponentsCount);
        }

        public void IncrementListProviderOptions()
        {
            Interlocked.Increment(ref listProviderOptionsCount);
        }
    }

    private sealed class CountingWorkflowComponentLibraryService(
        IWorkflowComponentLibraryService inner,
        WorkflowComponentLibraryCallCounter counter) : IWorkflowComponentLibraryService
    {
        public async Task<IReadOnlyList<WorkflowProviderOption>> ListProviderOptionsAsync(CancellationToken cancellationToken = default)
        {
            counter.IncrementListProviderOptions();
            return await inner.ListProviderOptionsAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<LlmCallComponent>> ListComponentsAsync(CancellationToken cancellationToken = default)
        {
            counter.IncrementListComponents();
            return await inner.ListComponentsAsync(cancellationToken);
        }

        public Task<LlmCallComponent?> GetComponentAsync(
            WorkflowComponentId componentId,
            CancellationToken cancellationToken = default)
            => inner.GetComponentAsync(componentId, cancellationToken);

        public Task<LlmCallComponent> SaveComponentAsync(
            LlmCallComponentSaveRequest request,
            CancellationToken cancellationToken = default)
            => inner.SaveComponentAsync(request, cancellationToken);

        public Task DeleteComponentAsync(
            WorkflowComponentId componentId,
            CancellationToken cancellationToken = default)
            => inner.DeleteComponentAsync(componentId, cancellationToken);
    }

    private sealed class WorkflowRunStoreCallCounter
    {
        private int getRunCount;
        private int listRunPageCount;
        private int listEventsCount;
        private int listEventPageCount;
        private int listArtifactsCount;
        private int listPendingExternalRequestsCount;

        public int GetRunCount => getRunCount;

        public int ListRunPageCount => listRunPageCount;

        public int ListEventsCount => listEventsCount;

        public int ListEventPageCount => listEventPageCount;

        public int ListArtifactsCount => listArtifactsCount;

        public int ListPendingExternalRequestsCount => listPendingExternalRequestsCount;

        public void IncrementGetRun()
        {
            Interlocked.Increment(ref getRunCount);
        }

        public void IncrementListRunPage()
        {
            Interlocked.Increment(ref listRunPageCount);
        }

        public void IncrementListEvents()
        {
            Interlocked.Increment(ref listEventsCount);
        }

        public void IncrementListEventPage()
        {
            Interlocked.Increment(ref listEventPageCount);
        }

        public void IncrementListArtifacts()
        {
            Interlocked.Increment(ref listArtifactsCount);
        }

        public void IncrementListPendingExternalRequests()
        {
            Interlocked.Increment(ref listPendingExternalRequestsCount);
        }
    }

    private sealed class CountingWorkflowRunStore(WorkflowRunStoreCallCounter counter) :
        IWorkflowRunStore,
        IWorkflowArtifactStore,
        IWorkflowExternalRequestStore
    {
        private readonly InMemoryWorkflowRunStore inner = new();
        private readonly Dictionary<WorkflowId, PendingRunRequest> pendingRunPages = [];

        public void DelayRunPages(params WorkflowId[] workflowIds)
        {
            foreach (var workflowId in workflowIds)
            {
                pendingRunPages[workflowId] = new PendingRunRequest();
            }
        }

        public Task WaitForRunPageRequestAsync(WorkflowId workflowId)
            => GetPendingRunPage(workflowId).Started.Task;

        public void CompleteRunPage(WorkflowId workflowId)
            => GetPendingRunPage(workflowId).Completion.TrySetResult();

        public Task CreateRunWithStartedEventAsync(
            WorkflowRunSnapshot run,
            WorkflowEventRecord startedEvent,
            CancellationToken cancellationToken = default)
            => inner.CreateRunWithStartedEventAsync(run, startedEvent, cancellationToken);

        public Task<WorkflowRunTransitionResult> TryTransitionRunAsync(
            WorkflowRunId runId,
            IReadOnlyCollection<WorkflowRunState> expectedStates,
            WorkflowRunSnapshot updatedRun,
            WorkflowEventRecord? transitionEvent = null,
            CancellationToken cancellationToken = default)
            => inner.TryTransitionRunAsync(
                runId,
                expectedStates,
                updatedRun,
                transitionEvent,
                cancellationToken);

        public Task<WorkflowExternalResponseAcceptanceResult> TryAcceptExternalResponseAsync(
            WorkflowExternalRequestId requestId,
            string responseJson,
            DateTimeOffset respondedAtUtc,
            CancellationToken cancellationToken = default)
            => inner.TryAcceptExternalResponseAsync(
                requestId,
                responseJson,
                respondedAtUtc,
                cancellationToken);

        public Task SaveRunAsync(
            WorkflowRunSnapshot run,
            CancellationToken cancellationToken = default)
            => inner.SaveRunAsync(run, cancellationToken);

        public Task<WorkflowRunSnapshot?> GetRunAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
        {
            counter.IncrementGetRun();
            return inner.GetRunAsync(runId, cancellationToken);
        }

        public Task<IReadOnlyList<WorkflowRunSnapshot>> ListRunsAsync(
            WorkflowId? workflowId = null,
            CancellationToken cancellationToken = default)
            => inner.ListRunsAsync(workflowId, cancellationToken);

        public async Task<WorkflowListPage<WorkflowRunSnapshot>> ListRunPageAsync(
            WorkflowRunPageRequest request,
            CancellationToken cancellationToken = default)
        {
            counter.IncrementListRunPage();
            if (request.WorkflowId is { } workflowId &&
                pendingRunPages.TryGetValue(workflowId, out var pendingRequest))
            {
                pendingRequest.Started.TrySetResult();
                await pendingRequest.Completion.Task.WaitAsync(cancellationToken);
            }

            return await inner.ListRunPageAsync(request, cancellationToken);
        }

        public Task SaveEventAsync(
            WorkflowEventRecord workflowEvent,
            CancellationToken cancellationToken = default)
            => inner.SaveEventAsync(workflowEvent, cancellationToken);

        public Task<IReadOnlyList<WorkflowEventRecord>> ListEventsAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
        {
            counter.IncrementListEvents();
            return inner.ListEventsAsync(runId, cancellationToken);
        }

        public Task<WorkflowListPage<WorkflowEventRecord>> ListEventPageAsync(
            WorkflowEventPageRequest request,
            CancellationToken cancellationToken = default)
        {
            counter.IncrementListEventPage();
            return inner.ListEventPageAsync(request, cancellationToken);
        }

        public Task<WorkflowCheckpointRecord> SaveCheckpointAsync(
            WorkflowCheckpointRecord checkpoint,
            CancellationToken cancellationToken = default)
            => inner.SaveCheckpointAsync(checkpoint, cancellationToken);

        public Task<WorkflowCheckpointRecord?> GetCheckpointAsync(
            WorkflowCheckpointId checkpointId,
            CancellationToken cancellationToken = default)
            => inner.GetCheckpointAsync(checkpointId, cancellationToken);

        public Task<IReadOnlyList<WorkflowCheckpointRecord>> ListCheckpointsAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => inner.ListCheckpointsAsync(runId, cancellationToken);

        public Task<WorkflowCheckpointRecord> MarkCheckpointResumedAsync(
            WorkflowCheckpointId checkpointId,
            DateTimeOffset resumedAtUtc,
            CancellationToken cancellationToken = default)
            => inner.MarkCheckpointResumedAsync(checkpointId, resumedAtUtc, cancellationToken);

        public Task SaveExternalRequestAsync(
            WorkflowExternalRequestRecord request,
            CancellationToken cancellationToken = default)
            => inner.SaveExternalRequestAsync(request, cancellationToken);

        public Task<WorkflowExternalRequestRecord?> GetExternalRequestAsync(
            WorkflowExternalRequestId requestId,
            CancellationToken cancellationToken = default)
            => inner.GetExternalRequestAsync(requestId, cancellationToken);

        public Task<IReadOnlyList<WorkflowExternalRequestRecord>> ListPendingExternalRequestsAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
        {
            counter.IncrementListPendingExternalRequests();
            return inner.ListPendingExternalRequestsAsync(runId, cancellationToken);
        }

        public Task SaveArtifactAsync(
            WorkflowArtifactRecord artifact,
            CancellationToken cancellationToken = default)
            => inner.SaveArtifactAsync(artifact, cancellationToken);

        public Task<IReadOnlyList<WorkflowArtifactRecord>> ListArtifactsAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
        {
            counter.IncrementListArtifacts();
            return inner.ListArtifactsAsync(runId, cancellationToken);
        }

        async Task<WorkflowArtifactRecord> IWorkflowArtifactStore.SaveArtifactAsync(
            WorkflowArtifactRecord artifact,
            CancellationToken cancellationToken)
        {
            await inner.SaveArtifactAsync(artifact, cancellationToken);
            return artifact;
        }

        Task<IReadOnlyList<WorkflowExternalRequestRecord>> IWorkflowExternalRequestStore.ListPendingRequestsAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken)
            => ListPendingExternalRequestsAsync(runId, cancellationToken);

        Task<WorkflowExternalRequestRecord> IWorkflowExternalRequestStore.SaveRequestAsync(
            WorkflowExternalRequestRecord request,
            CancellationToken cancellationToken)
            => ((IWorkflowExternalRequestStore)inner).SaveRequestAsync(request, cancellationToken);

        Task<WorkflowExternalRequestRecord> IWorkflowExternalRequestStore.MarkRespondedAsync(
            WorkflowExternalRequestId requestId,
            string responseJson,
            DateTimeOffset respondedAtUtc,
            CancellationToken cancellationToken)
            => ((IWorkflowExternalRequestStore)inner).MarkRespondedAsync(requestId, responseJson, respondedAtUtc, cancellationToken);

        private PendingRunRequest GetPendingRunPage(WorkflowId workflowId)
            => pendingRunPages.TryGetValue(workflowId, out var pendingRequest)
                ? pendingRequest
                : throw new InvalidOperationException($"Workflow '{workflowId}' does not have a delayed run page.");

        private sealed class PendingRunRequest
        {
            public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    private sealed class CapturingWorkflowTestRunner : IWorkflowTestRunner
    {
        public WorkflowTestRunRequest? LastRequest { get; private set; }

        public Task<WorkflowTestRunResult> RunAsync(
            WorkflowTestRunRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var definition = request.DraftDefinition ?? CreateProjectStructurePreviewDefinition();
            var now = DateTimeOffset.UtcNow;
            var run = new WorkflowRunSnapshot(
                WorkflowRunId.New(),
                definition.Id,
                definition.VersionId,
                WorkflowRunState.Completed,
                WorkflowRuntimeBackendKind.InProcess,
                BackendRunId: "captured-preview",
                Summary: "Captured preview completed.",
                now,
                now);
            return Task.FromResult(new WorkflowTestRunResult(
                Succeeded: true,
                WorkflowValidationResult.Success,
                run,
                Events: [],
                Artifacts: [],
                PendingExternalRequests: [],
                ErrorMessage: string.Empty));
        }
    }

    private sealed class NodeProgressWorkflowTestRunner(WorkflowNodeId runningNodeId) : IWorkflowTestRunner
    {
        public bool HadProgressObserver { get; private set; }

        public async Task<WorkflowTestRunResult> RunAsync(
            WorkflowTestRunRequest request,
            CancellationToken cancellationToken = default)
        {
            var definition = request.DraftDefinition ?? CreatePreviewProgressDefinition();
            var observer = WorkflowNodeExecutionProgressScope.Current;
            HadProgressObserver = observer is not null;
            if (observer is not null)
            {
                await observer.RecordAsync(
                    new WorkflowNodeExecutionProgress(
                        definition.Id,
                        definition.VersionId,
                        RunId: null,
                        runningNodeId,
                        WorkflowNodeExecutionProgressState.Started,
                        DateTimeOffset.UtcNow),
                    cancellationToken);
            }

            var now = DateTimeOffset.UtcNow;
            var run = new WorkflowRunSnapshot(
                WorkflowRunId.New(),
                definition.Id,
                definition.VersionId,
                WorkflowRunState.Completed,
                WorkflowRuntimeBackendKind.InProcess,
                BackendRunId: "progress-preview",
                Summary: "Progress preview completed.",
                now,
                now);
            return new WorkflowTestRunResult(
                Succeeded: true,
                WorkflowValidationResult.Success,
                run,
                Events: [],
                Artifacts: [],
                PendingExternalRequests: [],
                ErrorMessage: string.Empty);
        }
    }

    private sealed class WorkflowRouteProjectGateway : IProjectStructureRuntimeGateway
    {
        private ProjectStructureRuntimeProjectSummary? project;
        private WorkflowId? workflowId;

        public void SetProjectWorkflow(Guid projectId, string projectName, WorkflowId attachedWorkflowId)
        {
            project = new ProjectStructureRuntimeProjectSummary(
                projectId,
                projectName,
                ProjectStructureRuntimeProjectStatus.Active,
                CurrentPhase: "Execution",
                PhaseCount: 1,
                ParentCount: 0,
                ChildCount: 0,
                DateTimeOffset.UtcNow);
            workflowId = attachedWorkflowId;
        }

        public Task<IReadOnlyList<ProjectStructureRuntimeProjectSummary>> ListProjectsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectStructureRuntimeProjectSummary>>(
                project is null ? [] : [project]);

        public Task<ProjectStructureRuntimeReadResponse> ReadStructureAsync(
            Guid projectId,
            ProjectStructureRuntimeReadRequest request,
            CancellationToken cancellationToken = default)
        {
            if (project?.Id != projectId || !workflowId.HasValue)
            {
                throw new InvalidOperationException($"Project '{projectId:D}' was not configured for the route test.");
            }

            var metadata = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
            {
                Workflow = new ProjectWorkflowNodeMetadata
                {
                    WorkflowId = workflowId.Value,
                    WorkflowName = "Attached workflow"
                }
            });
            IReadOnlyList<ProjectStructureRuntimeNodeSummary> nodes =
            [
                new ProjectStructureRuntimeNodeSummary(
                    Id: "workflow-route-node",
                    ParentId: null,
                    ObjectType: ProjectObjectType.WorkflowDefinition,
                    ObjectSubtype: string.Empty,
                    Title: "Attached workflow",
                    Subtitle: string.Empty,
                    Status: "Ready",
                    Notes: null,
                    Route: $"/agents/workflows?projectId={projectId:D}&workflowId={workflowId.Value.Value:D}",
                    ArtifactKind: "workflow-definition",
                    ArtifactId: workflowId.Value.Value,
                    MediaRelativePath: null,
                    MediaContentType: null,
                    MediaOriginalFileName: null,
                    Badges: [],
                    ProgressMode: string.Empty,
                    ProgressPercent: 0,
                    MarkerIcon: string.Empty,
                    MarkerTone: string.Empty,
                    MarkerLabel: string.Empty,
                    Priority: 0,
                    EffectivePriority: 0,
                    StartUtc: null,
                    EndUtc: null,
                    MetadataJson: metadata,
                    ProjectRole: ProjectStructureRuntimeProjectRole.ActiveProject,
                    RelatedProjectId: null,
                    ParentProjectCount: 0,
                    X: null,
                    Y: null)
            ];
            return Task.FromResult(new ProjectStructureRuntimeReadResponse(
                projectId,
                project.Name,
                nodes,
                Links: [],
                Warnings: []));
        }

        public Task<ProjectStructureRuntimeNodeSummary> CreateNodeAsync(
            Guid projectId,
            ProjectStructureRuntimeNodeCreateRequest request,
            ProjectStructureRuntimeAgentContext agent,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Route tests only read project structure.");

        public Task<ProjectStructureRuntimeNodeSummary> CreateAssetAsync(
            Guid projectId,
            ProjectStructureRuntimeAssetCreateRequest request,
            ProjectStructureRuntimeAgentContext agent,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Route tests only read project structure.");
    }

    private sealed class PreviewProjectGateway(Guid projectId) : IProjectStructureRuntimeGateway
    {
        public Task<IReadOnlyList<ProjectStructureRuntimeProjectSummary>> ListProjectsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectStructureRuntimeProjectSummary>>(
            [
                new ProjectStructureRuntimeProjectSummary(
                    projectId,
                    "Project structure preview target",
                    ProjectStructureRuntimeProjectStatus.Active,
                    CurrentPhase: "Execution",
                    PhaseCount: 1,
                    ParentCount: 0,
                    ChildCount: 0,
                    DateTimeOffset.UtcNow)
            ]);

        public Task<ProjectStructureRuntimeReadResponse> ReadStructureAsync(
            Guid projectId,
            ProjectStructureRuntimeReadRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Preview gateway only lists projects.");

        public Task<ProjectStructureRuntimeNodeSummary> CreateNodeAsync(
            Guid projectId,
            ProjectStructureRuntimeNodeCreateRequest request,
            ProjectStructureRuntimeAgentContext agent,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Preview gateway only lists projects.");

        public Task<ProjectStructureRuntimeNodeSummary> CreateAssetAsync(
            Guid projectId,
            ProjectStructureRuntimeAssetCreateRequest request,
            ProjectStructureRuntimeAgentContext agent,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Preview gateway only lists projects.");
    }
}
