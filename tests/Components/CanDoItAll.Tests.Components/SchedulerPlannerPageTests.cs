using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AppComponents;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.SchedulerPlanner.Pages;
using CanDoItAll.Tests.Components.AgentFramework;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.Tests.Components.Processes;

public sealed class SchedulerPlannerPageTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Scheduler_page_opens_exact_scheduler_agent_from_avatar_action()
    {
        var launcher = new RecordingSchedulerAgentChatLauncher();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<IAgentChatLauncher>();
            services.AddSingleton<IAgentChatLauncher>(launcher);
        });

        RenderFragment schedulerPlannerPageContent = builder =>
        {
            builder.OpenComponent<SchedulerPlannerPage>(0);
            builder.CloseComponent();
        };
        var cut = harness.Context.Render<AppToolbarActionsTestHost>(parameters => parameters
            .Add(p => p.ChildContent, schedulerPlannerPageContent));
        var page = cut.FindComponent<SchedulerPlannerPage>();
        cut.WaitForElement("[data-testid='scheduler-agent-open']");
        cut.WaitForAssertion(() => Assert.False(
            cut.Find("[data-testid='scheduler-agent-open']").HasAttribute("disabled")));
        var button = cut.Find("[data-testid='scheduler-agent-open']");
        Assert.Equal("Open Scheduler Agent", button.GetAttribute("aria-label"));
        Assert.EndsWith(
            "/avatar-03.jpg",
            button.QuerySelector("img")?.GetAttribute("src"),
            StringComparison.Ordinal);

        var surface = ReadSchedulerAgentChatSurface(page.Instance);
        var access = Assert.Single(surface.AgentAccess);
        Assert.Equal(SchedulerAgentIdentity.AgentId, access.AgentId);
        Assert.Equal(
            AgentChatContextPermission.Read | AgentChatContextPermission.Mutate,
            access.Permissions);
        Assert.Equal(
            AgentChatContextCompletionRefreshMode.OnSuccessfulRun,
            surface.CompletionRefreshMode);

        button.Click();

        cut.WaitForAssertion(() => Assert.Equal(SchedulerAgentIdentity.AgentId, launcher.StartedAgentId));
    }

    [Fact]
    public async Task Scheduler_page_renders_tabs_and_canvas_calendar_host()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();

        var cut = harness.Context.Render<SchedulerPlannerPage>();

        cut.WaitForAssertion(() =>
        {
            var errors = cut.FindAll("[data-testid='scheduler-error']")
                .Select(element => element.TextContent.Trim())
                .ToArray();
            if (errors.Length > 0)
            {
                Assert.Fail(string.Join(Environment.NewLine, errors));
            }

            Assert.NotEmpty(cut.FindAll("[data-testid='scheduler-tabs']"));
        });
        cut.WaitForElement("[data-testid='scheduler-calendar']");

        Assert.Contains("Calendar", cut.Markup);
        Assert.Contains("Schedules", cut.Markup);
        Assert.Contains("New schedule", cut.Markup);
        Assert.Contains("History", cut.Markup);
        Assert.Contains("scheduler-planner-calendar", cut.Markup);
    }

    [Fact]
    public async Task Scheduler_history_renders_route_and_retry_policy_labels()
    {
        var workflowId = Guid.NewGuid();
        var workflowVersionId = Guid.NewGuid();
        var workspace = CreateHistoryWorkspace(workflowId, workflowVersionId);
        var defaultEditor = new SchedulerPlanEditorModel
        {
            TargetKind = SchedulerPlanTargetKind.Workflow,
            TargetId = workflowId,
            TargetVersionId = workflowVersionId,
            CronExpression = "0 0 9 ? * MON-FRI",
            TimeZoneId = "UTC",
            InputJson = "{}",
            IsEnabled = true
        };

        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<ISchedulerPlannerService>();
            services.AddSingleton<ISchedulerPlannerService>(new StubSchedulerPlannerService(workspace, defaultEditor));
        });

        var cut = harness.Context.Render<SchedulerPlannerPage>();

        cut.WaitForElement("[data-testid='scheduler-tabs']");
        await cut.InvokeAsync(() =>
        {
            cut.FindAll("button[role='tab']")
                .Single(element => element.TextContent.Contains("History", StringComparison.Ordinal))
                .Click();
        });
        cut.WaitForElement("[data-testid='scheduler-history']");

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(SchedulerPlanRunRoutes.NoMessages, cut.Markup);
            Assert.Contains(SchedulerPlanRunRoutes.WaitingForApproval, cut.Markup);
            Assert.Contains("failed", cut.Markup);
            Assert.Contains("No retry: no matching message.", cut.Markup);
            Assert.Contains("No retry: waiting for approval.", cut.Markup);
            Assert.Contains("Retry scheduled: project write.", cut.Markup);
        });
    }

    [Fact]
    public async Task Scheduler_target_picker_uses_dialog_cards_and_filters()
    {
        var processId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var workflowVersionId = Guid.NewGuid();
        var workspace = new SchedulerPlannerWorkspace(
            [],
            [],
            [
                new SchedulerTargetOption(
                    SchedulerPlanTargetKind.Process,
                    processId,
                    null,
                    "Onboarding process",
                    "Role-first process for new customer onboarding.",
                    "Published"),
                new SchedulerTargetOption(
                    SchedulerPlanTargetKind.Workflow,
                    workflowId,
                    workflowVersionId,
                    "Release workflow",
                    "Workflow for release readiness validation.",
                    "Draft")
            ],
            new CanvasCalendarSurface
            {
                SurfaceId = "scheduler-planner-calendar",
                InitialView = "week",
                SelectedDate = "2026-05-12",
                Timezone = "UTC",
                Locale = "en-US",
                AllowCreate = false,
                AllowEdit = false,
                AllowDelete = false,
                AllowDragDrop = false,
                AllowResize = false
            });
        var defaultEditor = new SchedulerPlanEditorModel
        {
            TargetKind = SchedulerPlanTargetKind.Process,
            TargetId = processId,
            CronExpression = "0 0 9 ? * MON-FRI",
            TimeZoneId = "UTC",
            InputJson = "{}",
            IsEnabled = true
        };

        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<ISchedulerPlannerService>();
            services.AddSingleton<ISchedulerPlannerService>(new StubSchedulerPlannerService(workspace, defaultEditor));
        });

        var cut = harness.Context.Render<SchedulerPlannerPage>();

        cut.WaitForElement("[data-testid='scheduler-tabs']");
        cut.FindAll("button[role='tab']")
            .Single(element => element.TextContent.Contains("New schedule", StringComparison.Ordinal))
            .Click();
        cut.WaitForElement("[data-testid='scheduler-target-open']");
        cut.Find("[data-testid='scheduler-target-open']").Click();
        cut.WaitForElement("[data-testid='scheduler-target-dialog']");

        Assert.Equal(2, cut.FindAll("[data-testid='scheduler-target-card']").Count);
        Assert.Contains("Onboarding process", cut.Markup);
        Assert.Contains("Release workflow", cut.Markup);
        Assert.NotEmpty(cut.FindAll("[data-testid='scheduler-target-tag-filter']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='scheduler-target-filter-processes']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='scheduler-target-filter-workflows']"));

        cut.Find("[data-testid='scheduler-target-filter-workflows']").Change(false);

        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindAll("[data-testid='scheduler-target-card']"));
            Assert.Contains("Onboarding process", cut.Markup);
            Assert.DoesNotContain("Release workflow", cut.Markup);
        });
    }

    [Fact]
    public async Task Scheduler_typed_workflow_input_form_syncs_options_raw_json_and_cron_preset()
    {
        var workflowId = Guid.NewGuid();
        var workflowVersionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var workspace = CreateWorkflowWorkspace(workflowId, workflowVersionId);
        var defaultEditor = new SchedulerPlanEditorModel
        {
            Name = "Office365 watch",
            TargetKind = SchedulerPlanTargetKind.Workflow,
            TargetId = workflowId,
            TargetVersionId = workflowVersionId,
            CronExpression = "0 0 9 ? * MON-FRI",
            TimeZoneId = "UTC",
            InputJson = "{}",
            IsEnabled = true
        };
        var optionService = new StubSchedulerWorkflowInputOptionService(projectId);

        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<ISchedulerPlannerService>();
            services.RemoveAll<ISchedulerWorkflowInputSchemaService>();
            services.RemoveAll<ISchedulerWorkflowInputOptionService>();
            services.AddSingleton<ISchedulerPlannerService>(new StubSchedulerPlannerService(workspace, defaultEditor));
            services.AddSingleton<ISchedulerWorkflowInputSchemaService>(new StubSchedulerWorkflowInputSchemaService(
                CreateOffice365WorkflowInputSchema(workflowId, workflowVersionId)));
            services.AddSingleton<ISchedulerWorkflowInputOptionService>(optionService);
        });

        var cut = harness.Context.Render<SchedulerPlannerPage>();

        cut.WaitForElement("[data-testid='scheduler-tabs']");
        cut.FindAll("button[role='tab']")
            .Single(element => element.TextContent.Contains("New schedule", StringComparison.Ordinal))
            .Click();
        cut.WaitForElement("[data-testid='scheduler-typed-inputs']");

        Assert.Contains("Ada Lovelace &lt;ada@example.com&gt;", cut.Markup);
        Assert.Contains("Use default connection", cut.Markup);
        Assert.Contains("CanDoItAllProcessed", cut.Markup);
        Assert.Equal("336", cut.Find("[data-testid='scheduler-input-lookbackHours']").GetAttribute("value"));

        cut.Find("[data-testid='scheduler-input-emailAddress']").Change("ada@example.com");
        cut.Find("[data-testid='scheduler-input-projectId']").Change(projectId.ToString("D"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Inbox", cut.Markup);
        });

        cut.Find("[data-testid='scheduler-input-nodeId']").Change("node-inbox");
        cut.Find("[data-testid='scheduler-cron-preset-every-two-hours']").Click();

        cut.WaitForAssertion(() =>
        {
            var inputJson = cut.Find("[data-testid='scheduler-input-json']").GetAttribute("value") ?? string.Empty;
            Assert.Contains("0 0 0/2 ? * *", cut.Markup);
            Assert.Contains("\"emailAddress\": \"ada@example.com\"", inputJson);
            Assert.Contains($"\"projectId\": \"{projectId:D}\"", inputJson);
            Assert.Contains("\"nodeId\": \"node-inbox\"", inputJson);
        });

        var manualJson = $$"""
            {
              "emailAddress": "manual@example.com",
              "projectId": "{{projectId:D}}",
              "nodeId": "node-inbox",
              "processedCategory": "ManualProcessed",
              "lookbackHours": 24
            }
            """;
        cut.Find("[data-testid='scheduler-input-json']").Change(manualJson);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("manual@example.com", cut.Find("[data-testid='scheduler-input-emailAddress']").GetAttribute("value"));
            Assert.Equal("24", cut.Find("[data-testid='scheduler-input-lookbackHours']").GetAttribute("value"));
            Assert.Equal("ManualProcessed", cut.Find("[data-testid='scheduler-input-processedCategory']").GetAttribute("value"));
        });
    }

    [Fact]
    public async Task Scheduler_typed_workflow_input_validation_errors_render_before_save()
    {
        var workflowId = Guid.NewGuid();
        var workflowVersionId = Guid.NewGuid();
        var workspace = CreateWorkflowWorkspace(workflowId, workflowVersionId);
        var schedulerService = new StubSchedulerPlannerService(
            workspace,
            new SchedulerPlanEditorModel
            {
                Name = "Office365 watch",
                TargetKind = SchedulerPlanTargetKind.Workflow,
                TargetId = workflowId,
                TargetVersionId = workflowVersionId,
                CronExpression = "0 0 9 ? * MON-FRI",
                TimeZoneId = "UTC",
                InputJson = "{}",
                IsEnabled = true
            });

        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<ISchedulerPlannerService>();
            services.RemoveAll<ISchedulerWorkflowInputSchemaService>();
            services.RemoveAll<ISchedulerWorkflowInputOptionService>();
            services.AddSingleton<ISchedulerPlannerService>(schedulerService);
            services.AddSingleton<ISchedulerWorkflowInputSchemaService>(new StubSchedulerWorkflowInputSchemaService(
                CreateOffice365WorkflowInputSchema(workflowId, workflowVersionId)));
            services.AddSingleton<ISchedulerWorkflowInputOptionService>(new StubSchedulerWorkflowInputOptionService(Guid.NewGuid()));
        });

        var cut = harness.Context.Render<SchedulerPlannerPage>();

        cut.WaitForElement("[data-testid='scheduler-tabs']");
        cut.FindAll("button[role='tab']")
            .Single(element => element.TextContent.Contains("New schedule", StringComparison.Ordinal))
            .Click();
        cut.WaitForElement("[data-testid='scheduler-typed-inputs']");
        cut.Find("[data-testid='scheduler-save']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Workflow input needs attention", cut.Markup);
            Assert.Contains("Email address is required.", cut.Markup);
            Assert.Equal(0, schedulerService.SaveCount);
        });
    }

    [Fact]
    public async Task Scheduler_typed_workflow_input_clearing_required_value_removes_json_and_blocks_save()
    {
        var workflowId = Guid.NewGuid();
        var workflowVersionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var workspace = CreateWorkflowWorkspace(workflowId, workflowVersionId);
        var schedulerService = new StubSchedulerPlannerService(
            workspace,
            new SchedulerPlanEditorModel
            {
                Name = "Office365 watch",
                TargetKind = SchedulerPlanTargetKind.Workflow,
                TargetId = workflowId,
                TargetVersionId = workflowVersionId,
                CronExpression = "0 0 9 ? * MON-FRI",
                TimeZoneId = "UTC",
                InputJson = "{}",
                IsEnabled = true
            });

        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<ISchedulerPlannerService>();
            services.RemoveAll<ISchedulerWorkflowInputSchemaService>();
            services.RemoveAll<ISchedulerWorkflowInputOptionService>();
            services.AddSingleton<ISchedulerPlannerService>(schedulerService);
            services.AddSingleton<ISchedulerWorkflowInputSchemaService>(new StubSchedulerWorkflowInputSchemaService(
                CreateOffice365WorkflowInputSchema(workflowId, workflowVersionId)));
            services.AddSingleton<ISchedulerWorkflowInputOptionService>(new StubSchedulerWorkflowInputOptionService(projectId));
        });

        var cut = harness.Context.Render<SchedulerPlannerPage>();

        cut.WaitForElement("[data-testid='scheduler-tabs']");
        cut.FindAll("button[role='tab']")
            .Single(element => element.TextContent.Contains("New schedule", StringComparison.Ordinal))
            .Click();
        cut.WaitForElement("[data-testid='scheduler-typed-inputs']");
        cut.Find("[data-testid='scheduler-input-emailAddress']").Change("ada@example.com");
        cut.Find("[data-testid='scheduler-input-projectId']").Change(projectId.ToString("D"));
        cut.WaitForAssertion(() => Assert.Contains("Inbox", cut.Markup));
        cut.Find("[data-testid='scheduler-input-nodeId']").Change("node-inbox");

        cut.WaitForAssertion(() =>
        {
            var inputJson = cut.Find("[data-testid='scheduler-input-json']").GetAttribute("value") ?? string.Empty;
            Assert.Contains("\"emailAddress\": \"ada@example.com\"", inputJson);
        });

        cut.Find("[data-testid='scheduler-input-emailAddress']").Change(string.Empty);

        cut.WaitForAssertion(() =>
        {
            var inputJson = cut.Find("[data-testid='scheduler-input-json']").GetAttribute("value") ?? string.Empty;
            Assert.DoesNotContain("emailAddress", inputJson);
        });

        cut.Find("[data-testid='scheduler-save']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Workflow input needs attention", cut.Markup);
            Assert.Contains("Email address is required.", cut.Markup);
            Assert.Equal(0, schedulerService.SaveCount);
        });
    }

    private static SchedulerPlannerWorkspace CreateWorkflowWorkspace(
        Guid workflowId,
        Guid workflowVersionId)
    {
        return new SchedulerPlannerWorkspace(
            [],
            [],
            [
                new SchedulerTargetOption(
                    SchedulerPlanTargetKind.Workflow,
                    workflowId,
                    workflowVersionId,
                    "Office365 email watch summary",
                    "Polls Office365 mail by sender and writes summaries into project structure.",
                    "Active / Local")
            ],
            new CanvasCalendarSurface
            {
                SurfaceId = "scheduler-planner-calendar",
                InitialView = "week",
                SelectedDate = "2026-05-12",
                Timezone = "UTC",
                Locale = "en-US",
                AllowCreate = false,
                AllowEdit = false,
                AllowDelete = false,
                AllowDragDrop = false,
                AllowResize = false
            });
    }

    private static SchedulerPlannerWorkspace CreateScheduledPlanWorkspace(
        Guid planId,
        Guid workflowId,
        Guid workflowVersionId,
        IReadOnlyList<SchedulerTargetOption>? additionalTargets = null)
    {
        var updatedAtUtc = new DateTimeOffset(2026, 5, 12, 8, 0, 0, TimeSpan.Zero);
        var nextFireAtUtc = new DateTimeOffset(2026, 5, 13, 9, 0, 0, TimeSpan.Zero);
        var plannedEvent = new CanvasCalendarEvent
        {
            Id = $"planned-{planId:N}-{nextFireAtUtc.UtcTicks}",
            EventId = $"planned-{planId:N}-{nextFireAtUtc.UtcTicks}",
            Title = "Office365 email watch",
            Description = "At 09:00 on Monday through Friday every month (UTC). / Workflow / Office365 email watch summary",
            StartUtc = nextFireAtUtc,
            EndUtc = nextFireAtUtc.AddMinutes(30),
            Timezone = "UTC",
            TimezoneName = "UTC",
            EventType = SchedulerPlanTargetKind.Workflow.ToString(),
            Status = "Scheduled",
            Category = SchedulerPlanTargetKind.Workflow.ToString(),
            Color = "#2563eb",
            ReadOnly = false,
            RepositoryId = planId.ToString("D")
        };
        var targetOptions = new List<SchedulerTargetOption>
        {
            new(
                SchedulerPlanTargetKind.Workflow,
                workflowId,
                workflowVersionId,
                "Office365 email watch summary",
                "Polls Office365 mail by sender and writes summaries into project structure.",
                "Active / Local")
        };

        if (additionalTargets is not null)
        {
            targetOptions.AddRange(additionalTargets);
        }

        return new SchedulerPlannerWorkspace(
            [
                new SchedulerPlanSummary(
                    planId,
                    "Office365 email watch",
                    "Polls Office365 mail by sender and writes summaries into project structure.",
                    SchedulerPlanTargetKind.Workflow,
                    workflowId,
                    workflowVersionId,
                    "Office365 email watch summary",
                    "0 0 9 ? * MON-FRI",
                    "At 09:00 on Monday through Friday every month (UTC).",
                    "UTC",
                    SchedulerPlanMisfirePolicy.FireOnceNow,
                    true,
                    null,
                    null,
                    nextFireAtUtc,
                    null,
                    string.Empty,
                    updatedAtUtc)
            ],
            [],
            targetOptions,
            new CanvasCalendarSurface
            {
                SurfaceId = "scheduler-planner-calendar",
                InitialView = "week",
                SelectedDate = "2026-05-12",
                Timezone = "UTC",
                Locale = "en-US",
                AllowCreate = false,
                AllowEdit = false,
                AllowDelete = false,
                AllowDragDrop = false,
                AllowResize = false,
                Events = [plannedEvent]
            });
    }

    private static SchedulerPlannerWorkspace CreateHistoryWorkspace(
        Guid workflowId,
        Guid workflowVersionId)
    {
        var planId = Guid.NewGuid();
        var firedAtUtc = new DateTimeOffset(2026, 5, 12, 9, 0, 0, TimeSpan.Zero);

        return new SchedulerPlannerWorkspace(
            [],
            [
                new SchedulerPlanRunSummary(
                    Guid.NewGuid(),
                    planId,
                    "Office365 email watch",
                    SchedulerPlanTargetKind.Workflow,
                    "Office365 email watch summary",
                    firedAtUtc,
                    SchedulerPlanRunDispatchStatus.NoMessages,
                    1,
                    Guid.NewGuid(),
                    SchedulerPlanRunRoutes.NoMessages,
                    SchedulerPlanRunRetryCategory.NoAction,
                    "No unprocessed Office365 email matched the configured address.",
                    string.Empty,
                    firedAtUtc),
                new SchedulerPlanRunSummary(
                    Guid.NewGuid(),
                    planId,
                    "Office365 email watch",
                    SchedulerPlanTargetKind.Workflow,
                    "Office365 email watch summary",
                    firedAtUtc.AddMinutes(5),
                    SchedulerPlanRunDispatchStatus.WaitingForApproval,
                    1,
                    Guid.NewGuid(),
                    SchedulerPlanRunRoutes.WaitingForApproval,
                    SchedulerPlanRunRetryCategory.WorkflowWaitingForApproval,
                    "Workflow run is waiting for approval.",
                    string.Empty,
                    firedAtUtc.AddMinutes(5)),
                new SchedulerPlanRunSummary(
                    Guid.NewGuid(),
                    planId,
                    "Office365 email watch",
                    SchedulerPlanTargetKind.Workflow,
                    "Office365 email watch summary",
                    firedAtUtc.AddMinutes(10),
                    SchedulerPlanRunDispatchStatus.Failed,
                    1,
                    null,
                    SchedulerPlanRunRoutes.Failed,
                    SchedulerPlanRunRetryCategory.ProjectWriteFailure,
                    string.Empty,
                    "Project structure write failed while creating scheduler task nodes.",
                    firedAtUtc.AddMinutes(10))
            ],
            [
                new SchedulerTargetOption(
                    SchedulerPlanTargetKind.Workflow,
                    workflowId,
                    workflowVersionId,
                    "Office365 email watch summary",
                    "Polls Office365 mail by sender and writes summaries into project structure.",
                    "Active / Local")
            ],
            new CanvasCalendarSurface
            {
                SurfaceId = "scheduler-planner-calendar",
                InitialView = "week",
                SelectedDate = "2026-05-12",
                Timezone = "UTC",
                Locale = "en-US",
                AllowCreate = false,
                AllowEdit = false,
                AllowDelete = false,
                AllowDragDrop = false,
                AllowResize = false
            });
    }

    private static SchedulerWorkflowInputSchema CreateOffice365WorkflowInputSchema(
        Guid workflowId,
        Guid workflowVersionId)
    {
        return new SchedulerWorkflowInputSchema(
            new WorkflowId(workflowId),
            new WorkflowVersionId(workflowVersionId),
            "Office365 email watch summary",
            [
                new WorkflowInputParameterDescriptor(
                    "connectionId",
                    "Office365 connection",
                    WorkflowInputParameterKind.ExternalConnectionId,
                    false,
                    "Optional OAuth connection. Leave blank to use the latest connected Office365 mailbox.",
                    "$.connectionId",
                    string.Empty,
                    new WorkflowInputParameterOptionSource(
                        WorkflowInputParameterOptionSourceKind.Office365Connections,
                        string.Empty,
                        []),
                    null,
                    null,
                    string.Empty),
                new WorkflowInputParameterDescriptor(
                    "emailAddress",
                    "Email address",
                    WorkflowInputParameterKind.EmailAddress,
                    true,
                    "CRM contact email or manually typed sender address.",
                    "$.emailAddress",
                    string.Empty,
                    new WorkflowInputParameterOptionSource(
                        WorkflowInputParameterOptionSourceKind.CrmContacts,
                        string.Empty,
                        []),
                    null,
                    null,
                    "sender@example.com"),
                new WorkflowInputParameterDescriptor(
                    "projectId",
                    "Project",
                    WorkflowInputParameterKind.ProjectId,
                    true,
                    "Project where generated summaries and tasks are created.",
                    "$.projectId",
                    string.Empty,
                    new WorkflowInputParameterOptionSource(
                        WorkflowInputParameterOptionSourceKind.ProjectStructureProjects,
                        string.Empty,
                        []),
                    null,
                    null,
                    string.Empty),
                new WorkflowInputParameterDescriptor(
                    "nodeId",
                    "Parent node",
                    WorkflowInputParameterKind.ProjectNodeId,
                    true,
                    "Project-structure parent node for generated content.",
                    "$.nodeId",
                    string.Empty,
                    new WorkflowInputParameterOptionSource(
                        WorkflowInputParameterOptionSourceKind.ProjectStructureNodes,
                        "projectId",
                        []),
                    null,
                    null,
                    string.Empty),
                new WorkflowInputParameterDescriptor(
                    "processedCategory",
                    "Processed category",
                    WorkflowInputParameterKind.Category,
                    false,
                    "Category added after successful project write.",
                    "$.processedCategory",
                    "CanDoItAllProcessed",
                    WorkflowInputParameterOptionSource.None,
                    null,
                    null,
                    "CanDoItAllProcessed"),
                new WorkflowInputParameterDescriptor(
                    "lookbackHours",
                    "Lookback hours",
                    WorkflowInputParameterKind.Integer,
                    false,
                    "Maximum mail age to scan.",
                    "$.lookbackHours",
                    "336",
                    WorkflowInputParameterOptionSource.None,
                    1,
                    720,
                    "336")
            ],
            UsesRawJsonFallback: false);
    }

    private sealed class StubSchedulerPlannerService(
        SchedulerPlannerWorkspace workspace,
        SchedulerPlanEditorModel defaultEditor) : ISchedulerPlannerService
    {
        public int SaveCount { get; private set; }

        public int DeleteCount { get; private set; }

        public Guid? LastLoadedPlanId { get; private set; }

        public Guid? DeletedPlanId { get; private set; }

        public SchedulerPlanEditorModel? LastSavedEditor { get; private set; }

        public Task<SchedulerPlannerWorkspace> GetWorkspaceAsync(
            SchedulerHistoryQuery? historyQuery = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(workspace);
        }

        public Task<SchedulerPlanEditorModel> CreateDefaultEditorAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(defaultEditor);
        }

        public Task<SchedulerPlanEditorModel> GetPlanEditorAsync(
            Guid planId,
            CancellationToken cancellationToken = default)
        {
            LastLoadedPlanId = planId;
            var plan = workspace.Plans.FirstOrDefault(item => item.Id == planId);
            if (plan is null)
            {
                throw new KeyNotFoundException($"Scheduler plan '{planId:D}' was not found.");
            }

            return Task.FromResult(new SchedulerPlanEditorModel
            {
                Id = plan.Id,
                Name = plan.Name,
                Description = plan.Description,
                TargetKind = plan.TargetKind,
                TargetId = plan.TargetId,
                TargetVersionId = plan.TargetVersionId,
                CronExpression = plan.CronExpression,
                TimeZoneId = plan.TimeZoneId,
                MisfirePolicy = plan.MisfirePolicy,
                IsEnabled = plan.IsEnabled,
                StartAtUtc = plan.StartAtUtc,
                EndAtUtc = plan.EndAtUtc,
                InputJson = "{}"
            });
        }

        public Task<SchedulerPlanSummary> SavePlanAsync(
            SchedulerPlanEditorModel editor,
            CancellationToken cancellationToken = default)
        {
            SaveCount++;
            LastSavedEditor = new SchedulerPlanEditorModel
            {
                Id = editor.Id,
                Name = editor.Name,
                Description = editor.Description,
                TargetKind = editor.TargetKind,
                TargetId = editor.TargetId,
                TargetVersionId = editor.TargetVersionId,
                CronExpression = editor.CronExpression,
                TimeZoneId = editor.TimeZoneId,
                MisfirePolicy = editor.MisfirePolicy,
                IsEnabled = editor.IsEnabled,
                StartAtUtc = editor.StartAtUtc,
                EndAtUtc = editor.EndAtUtc,
                InputJson = editor.InputJson
            };

            return Task.FromResult(new SchedulerPlanSummary(
                editor.Id ?? Guid.NewGuid(),
                editor.Name,
                editor.Description,
                editor.TargetKind,
                editor.TargetId,
                editor.TargetVersionId,
                "Target",
                editor.CronExpression,
                "Every weekday at 09:00 (UTC).",
                editor.TimeZoneId,
                editor.MisfirePolicy,
                editor.IsEnabled,
                editor.StartAtUtc,
                editor.EndAtUtc,
                null,
                null,
                string.Empty,
                DateTimeOffset.UtcNow));
        }

        public Task SetPlanEnabledAsync(
            Guid planId,
            bool isEnabled,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeletePlanAsync(
            Guid planId,
            CancellationToken cancellationToken = default)
        {
            DeleteCount++;
            DeletedPlanId = planId;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Scheduler_schedules_tab_filters_cards_and_opens_edit_delete_dialogs()
    {
        var workflowId = Guid.NewGuid();
        var workflowVersionId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var workspace = CreateScheduledPlanWorkspace(planId, workflowId, workflowVersionId);
        var schedulerService = new StubSchedulerPlannerService(
            workspace,
            new SchedulerPlanEditorModel
            {
                Name = "Office365 email watch",
                TargetKind = SchedulerPlanTargetKind.Workflow,
                TargetId = workflowId,
                TargetVersionId = workflowVersionId,
                CronExpression = "0 0 9 ? * MON-FRI",
                TimeZoneId = "UTC",
                InputJson = "{}",
                IsEnabled = true
            });

        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<ISchedulerPlannerService>();
            services.AddSingleton<ISchedulerPlannerService>(schedulerService);
        });

        var cut = harness.Context.Render<SchedulerPlannerPage>();

        cut.WaitForElement("[data-testid='scheduler-tabs']");
        cut.FindAll("button[role='tab']")
            .Single(element => element.TextContent.Contains("Schedules", StringComparison.Ordinal))
            .Click();
        cut.WaitForElement("[data-testid='scheduler-plan-list']");

        Assert.Single(cut.FindAll("[data-testid='scheduler-plan-card']"));
        Assert.Contains("Office365 email watch", cut.Markup);
        Assert.NotEmpty(cut.FindAll("[data-testid='scheduler-schedules-search']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='scheduler-schedules-target-kind']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='scheduler-schedules-state']"));

        cut.Find("[data-testid='scheduler-schedules-search']").Change("missing");
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[data-testid='scheduler-plan-card']"));
            Assert.Contains("No schedules match the current filters", cut.Markup);
        });

        cut.Find("[data-testid='scheduler-schedules-search']").Change("Office365");
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='scheduler-plan-card']")));

        cut.Find("[data-testid='scheduler-plan-edit']").Click();
        cut.WaitForElement("[data-testid='scheduler-edit-dialog']");
        Assert.Equal("Office365 email watch", cut.Find("[data-testid='scheduler-edit-name']").GetAttribute("value"));

        cut.Find("[data-testid='scheduler-edit-save']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, schedulerService.SaveCount);
            Assert.Equal(planId, schedulerService.LastSavedEditor?.Id);
        });

        cut.Find("[data-testid='scheduler-plan-delete']").Click();
        cut.WaitForElement("[data-testid='scheduler-delete-dialog']");
        Assert.Contains("Remove schedule", cut.Markup);

        cut.Find("[data-testid='scheduler-delete-confirm']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, schedulerService.DeleteCount);
            Assert.Equal(planId, schedulerService.DeletedPlanId);
        });
    }

    [Fact]
    public async Task Scheduler_edit_dialog_can_change_selected_workflow()
    {
        var workflowId = Guid.NewGuid();
        var workflowVersionId = Guid.NewGuid();
        var replacementWorkflowId = Guid.NewGuid();
        var replacementWorkflowVersionId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var workspace = CreateScheduledPlanWorkspace(
            planId,
            workflowId,
            workflowVersionId,
            [
                new SchedulerTargetOption(
                    SchedulerPlanTargetKind.Workflow,
                    replacementWorkflowId,
                    replacementWorkflowVersionId,
                    "Replacement workflow",
                    "Runs the replacement workflow for scheduler edits.",
                    "Active / Local")
            ]);
        var schedulerService = new StubSchedulerPlannerService(
            workspace,
            new SchedulerPlanEditorModel
            {
                Name = "Office365 email watch",
                TargetKind = SchedulerPlanTargetKind.Workflow,
                TargetId = workflowId,
                TargetVersionId = workflowVersionId,
                CronExpression = "0 0 9 ? * MON-FRI",
                TimeZoneId = "UTC",
                InputJson = "{}",
                IsEnabled = true
            });

        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<ISchedulerPlannerService>();
            services.AddSingleton<ISchedulerPlannerService>(schedulerService);
        });

        var cut = harness.Context.Render<SchedulerPlannerPage>();

        cut.WaitForElement("[data-testid='scheduler-tabs']");
        await cut.InvokeAsync(() =>
        {
            cut.FindAll("button[role='tab']")
                .Single(element => element.TextContent.Contains("Schedules", StringComparison.Ordinal))
                .Click();
        });
        await cut.InvokeAsync(() => cut.WaitForElement("[data-testid='scheduler-plan-edit']").Click());
        cut.WaitForElement("[data-testid='scheduler-edit-dialog']");

        Assert.Contains("Office365 email watch summary", cut.Markup);

        await cut.InvokeAsync(() => cut.Find("[data-testid='scheduler-edit-target-open']").Click());
        cut.WaitForElement("[data-testid='scheduler-target-dialog']");
        Assert.Contains("Change schedule target", cut.Markup);

        await cut.InvokeAsync(() =>
        {
            cut.FindAll("[data-testid='scheduler-target-card']")
                .Single(element => element.TextContent.Contains("Replacement workflow", StringComparison.Ordinal))
                .Click();
        });

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Replacement workflow", cut.Markup);
            Assert.Empty(cut.FindAll("[data-testid='scheduler-target-dialog']"));
        });

        await cut.InvokeAsync(() => cut.Find("[data-testid='scheduler-edit-save']").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, schedulerService.SaveCount);
            Assert.Equal(planId, schedulerService.LastSavedEditor?.Id);
            Assert.Equal(replacementWorkflowId, schedulerService.LastSavedEditor?.TargetId);
            Assert.Equal(replacementWorkflowVersionId, schedulerService.LastSavedEditor?.TargetVersionId);
        });
    }

    [Fact]
    public async Task Scheduler_calendar_double_click_opens_edit_dialog_for_selected_planned_event()
    {
        var workflowId = Guid.NewGuid();
        var workflowVersionId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var workspace = CreateScheduledPlanWorkspace(planId, workflowId, workflowVersionId);
        var schedulerService = new StubSchedulerPlannerService(
            workspace,
            new SchedulerPlanEditorModel
            {
                Name = "Office365 email watch",
                TargetKind = SchedulerPlanTargetKind.Workflow,
                TargetId = workflowId,
                TargetVersionId = workflowVersionId,
                CronExpression = "0 0 9 ? * MON-FRI",
                TimeZoneId = "UTC",
                InputJson = "{}",
                IsEnabled = true
            });

        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<ISchedulerPlannerService>();
            services.AddSingleton<ISchedulerPlannerService>(schedulerService);
        });

        var cut = harness.Context.Render<SchedulerPlannerPage>();

        cut.WaitForElement("[data-testid='scheduler-calendar']");
        var calendarEvent = workspace.CalendarSurface.Events.Single();
        var calendar = cut.FindComponent<CanvasCalendar>();
        await cut.InvokeAsync(() => calendar.Instance.OnSelectionChanged(
            JsonSerializer.Serialize(calendarEvent, JsonOptions),
            "{}"));

        cut.Find("[data-testid='scheduler-calendar']").TriggerEvent("ondblclick", new MouseEventArgs());

        cut.WaitForElement("[data-testid='scheduler-edit-dialog']");
        Assert.Equal("Office365 email watch", cut.Find("[data-testid='scheduler-edit-name']").GetAttribute("value"));
        Assert.Equal(planId, schedulerService.LastLoadedPlanId);
    }

    [Fact]
    public async Task Scheduler_calendar_double_click_opens_edit_dialog_when_selection_callback_arrives_after_double_click()
    {
        var workflowId = Guid.NewGuid();
        var workflowVersionId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var workspace = CreateScheduledPlanWorkspace(planId, workflowId, workflowVersionId);
        var schedulerService = new StubSchedulerPlannerService(
            workspace,
            new SchedulerPlanEditorModel
            {
                Name = "Office365 email watch",
                TargetKind = SchedulerPlanTargetKind.Workflow,
                TargetId = workflowId,
                TargetVersionId = workflowVersionId,
                CronExpression = "0 0 9 ? * MON-FRI",
                TimeZoneId = "UTC",
                InputJson = "{}",
                IsEnabled = true
            });

        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<ISchedulerPlannerService>();
            services.AddSingleton<ISchedulerPlannerService>(schedulerService);
        });

        var cut = harness.Context.Render<SchedulerPlannerPage>();

        cut.WaitForElement("[data-testid='scheduler-calendar']");
        cut.Find("[data-testid='scheduler-calendar']").TriggerEvent("ondblclick", new MouseEventArgs());

        var calendarEvent = workspace.CalendarSurface.Events.Single();
        var calendar = cut.FindComponent<CanvasCalendar>();
        await cut.InvokeAsync(() => calendar.Instance.OnSelectionChanged(
            JsonSerializer.Serialize(calendarEvent, JsonOptions),
            "{}"));

        cut.WaitForElement("[data-testid='scheduler-edit-dialog']");
        Assert.Equal("Office365 email watch", cut.Find("[data-testid='scheduler-edit-name']").GetAttribute("value"));
        Assert.Equal(planId, schedulerService.LastLoadedPlanId);
    }

    [Fact]
    public async Task Scheduler_calendar_repeated_event_selection_opens_edit_dialog_for_canvas_double_click()
    {
        var workflowId = Guid.NewGuid();
        var workflowVersionId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var workspace = CreateScheduledPlanWorkspace(planId, workflowId, workflowVersionId);
        var schedulerService = new StubSchedulerPlannerService(
            workspace,
            new SchedulerPlanEditorModel
            {
                Name = "Office365 email watch",
                TargetKind = SchedulerPlanTargetKind.Workflow,
                TargetId = workflowId,
                TargetVersionId = workflowVersionId,
                CronExpression = "0 0 9 ? * MON-FRI",
                TimeZoneId = "UTC",
                InputJson = "{}",
                IsEnabled = true
            });

        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<ISchedulerPlannerService>();
            services.AddSingleton<ISchedulerPlannerService>(schedulerService);
        });

        var cut = harness.Context.Render<SchedulerPlannerPage>();

        cut.WaitForElement("[data-testid='scheduler-calendar']");
        var calendarEvent = workspace.CalendarSurface.Events.Single();
        var calendar = cut.FindComponent<CanvasCalendar>();
        var selectedEventJson = JsonSerializer.Serialize(calendarEvent, JsonOptions);

        await cut.InvokeAsync(() => calendar.Instance.OnSelectionChanged(selectedEventJson, "{}"));
        await cut.InvokeAsync(() => calendar.Instance.OnSelectionChanged(selectedEventJson, "{}"));

        cut.WaitForElement("[data-testid='scheduler-edit-dialog']");
        Assert.Equal("Office365 email watch", cut.Find("[data-testid='scheduler-edit-name']").GetAttribute("value"));
        Assert.Equal(planId, schedulerService.LastLoadedPlanId);
    }

    private sealed class StubSchedulerWorkflowInputSchemaService(
        SchedulerWorkflowInputSchema schema) : ISchedulerWorkflowInputSchemaService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public Task<SchedulerWorkflowInputSchema> ResolveSchemaAsync(
            WorkflowId workflowId,
            WorkflowVersionId? versionId = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(schema);
        }

        public Task<SchedulerWorkflowInputValidationResult> ValidateInputAsync(
            WorkflowId workflowId,
            WorkflowVersionId? versionId,
            string? inputJson,
            CancellationToken cancellationToken = default)
        {
            var issues = new List<SchedulerWorkflowInputValidationIssue>();
            JsonObject? root;
            try
            {
                root = JsonNode.Parse(string.IsNullOrWhiteSpace(inputJson) ? "{}" : inputJson) as JsonObject;
            }
            catch (JsonException exception)
            {
                return Task.FromResult(new SchedulerWorkflowInputValidationResult(
                    false,
                    inputJson ?? "{}",
                    [new SchedulerWorkflowInputValidationIssue(string.Empty, exception.Message)]));
            }

            if (root is null)
            {
                return Task.FromResult(new SchedulerWorkflowInputValidationResult(
                    false,
                    inputJson ?? "{}",
                    [new SchedulerWorkflowInputValidationIssue(string.Empty, "Workflow input must be a JSON object.")]));
            }

            RequireText(root, "emailAddress", "Email address is required.", issues);
            RequireText(root, "projectId", "Project is required.", issues);
            RequireText(root, "nodeId", "Parent node is required.", issues);

            return Task.FromResult(new SchedulerWorkflowInputValidationResult(
                issues.Count == 0,
                root.ToJsonString(JsonOptions),
                issues));
        }

        private static void RequireText(
            JsonObject root,
            string propertyName,
            string message,
            List<SchedulerWorkflowInputValidationIssue> issues)
        {
            if (!root.TryGetPropertyValue(propertyName, out var value) ||
                value is not JsonValue jsonValue ||
                !jsonValue.TryGetValue<string>(out var text) ||
                string.IsNullOrWhiteSpace(text))
            {
                issues.Add(new SchedulerWorkflowInputValidationIssue(propertyName, message));
            }
        }
    }

    private sealed class StubSchedulerWorkflowInputOptionService(
        Guid projectId) : ISchedulerWorkflowInputOptionService
    {
        public Task<IReadOnlyList<WorkflowInputParameterOption>> ListOptionsAsync(
            WorkflowInputParameterDescriptor parameter,
            IReadOnlyDictionary<string, string> currentValues,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<WorkflowInputParameterOption> options = parameter.OptionSource.Kind switch
            {
                WorkflowInputParameterOptionSourceKind.Office365Connections =>
                [
                    new WorkflowInputParameterOption(Guid.NewGuid().ToString("D"), "Office365 Main", "Connected")
                ],
                WorkflowInputParameterOptionSourceKind.CrmContacts =>
                [
                    new WorkflowInputParameterOption("ada@example.com", "Ada Lovelace <ada@example.com>", "CRM contact")
                ],
                WorkflowInputParameterOptionSourceKind.ProjectStructureProjects =>
                [
                    new WorkflowInputParameterOption(projectId.ToString("D"), "Project Alpha", "Active")
                ],
                WorkflowInputParameterOptionSourceKind.ProjectStructureNodes
                    when currentValues.TryGetValue("projectId", out var selectedProjectId) &&
                         string.Equals(selectedProjectId, projectId.ToString("D"), StringComparison.OrdinalIgnoreCase) =>
                [
                    new WorkflowInputParameterOption("node-inbox", "Inbox", "ProjectRoot / Active")
                ],
                _ => []
            };

            return Task.FromResult(options);
        }
    }

    [Fact]
    public async Task Scheduler_edit_selection_keeps_newer_plan_when_previous_editor_load_finishes_late()
    {
        var firstPlanId = Guid.NewGuid();
        var secondPlanId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var workflowVersionId = Guid.NewGuid();
        var workspace = CreateScheduledPlanWorkspace(firstPlanId, workflowId, workflowVersionId);
        var firstPlan = workspace.Plans.Single() with { Name = "First schedule" };
        var secondPlan = firstPlan with
        {
            Id = secondPlanId,
            Name = "Second schedule"
        };
        workspace = workspace with { Plans = [firstPlan, secondPlan] };
        var schedulerService = new RacingSchedulerPlannerService(
            workspace,
            CreateProcessEditor());

        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<ISchedulerPlannerService>();
            services.AddSingleton<ISchedulerPlannerService>(schedulerService);
        });
        var cut = harness.Context.Render<SchedulerPlannerPage>();
        cut.WaitForElement("[data-testid='scheduler-tabs']");

        var firstSelection = cut.InvokeAsync(() => InvokeOpenEditScheduleDialogAsync(cut.Instance, firstPlan));
        await schedulerService.WaitForEditorRequestAsync(firstPlanId);
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(firstPlanId, ReadSelectedPlanForEdit(cut.Instance)?.Id);
            Assert.Equal(AgentChatContextAccessState.Loading, ReadSchedulerAgentChatAccessState(cut.Instance));
        });

        var secondSelection = cut.InvokeAsync(() => InvokeOpenEditScheduleDialogAsync(cut.Instance, secondPlan));
        await schedulerService.WaitForEditorRequestAsync(secondPlanId);
        schedulerService.CompleteEditorRequest(secondPlanId);
        await secondSelection;

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(secondPlanId, ReadSelectedPlanForEdit(cut.Instance)?.Id);
            Assert.Equal(secondPlanId, ReadEditScheduleEditor(cut.Instance)?.Id);
            Assert.Equal(AgentChatContextAccessState.Ready, ReadSchedulerAgentChatAccessState(cut.Instance));
        });

        schedulerService.CompleteEditorRequest(firstPlanId);
        await firstSelection;

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(secondPlanId, ReadSelectedPlanForEdit(cut.Instance)?.Id);
            Assert.Equal(secondPlanId, ReadEditScheduleEditor(cut.Instance)?.Id);
            Assert.Equal(AgentChatContextAccessState.Ready, ReadSchedulerAgentChatAccessState(cut.Instance));
        });
    }

    [Fact]
    public async Task Scheduler_target_selection_keeps_newer_schema_when_previous_schema_load_finishes_late()
    {
        var firstWorkflowId = Guid.NewGuid();
        var firstVersionId = Guid.NewGuid();
        var secondWorkflowId = Guid.NewGuid();
        var secondVersionId = Guid.NewGuid();
        var firstTarget = CreateWorkflowTarget(firstWorkflowId, firstVersionId, "First workflow");
        var secondTarget = CreateWorkflowTarget(secondWorkflowId, secondVersionId, "Second workflow");
        var workspace = CreateTargetWorkspace(firstTarget, secondTarget);
        var schedulerService = new RacingSchedulerPlannerService(workspace, CreateProcessEditor());
        var schemaService = new RacingSchedulerWorkflowInputSchemaService();
        var optionService = new ValueEchoingWorkflowInputOptionService();

        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<ISchedulerPlannerService>();
            services.RemoveAll<ISchedulerWorkflowInputSchemaService>();
            services.RemoveAll<ISchedulerWorkflowInputOptionService>();
            services.AddSingleton<ISchedulerPlannerService>(schedulerService);
            services.AddSingleton<ISchedulerWorkflowInputSchemaService>(schemaService);
            services.AddSingleton<ISchedulerWorkflowInputOptionService>(optionService);
        });
        var cut = harness.Context.Render<SchedulerPlannerPage>();
        cut.WaitForElement("[data-testid='scheduler-tabs']");

        var firstSelection = cut.InvokeAsync(() => InvokeSelectTargetAsync(cut.Instance, firstTarget));
        await schemaService.WaitForRequestAsync(firstWorkflowId);
        var secondSelection = cut.InvokeAsync(() => InvokeSelectTargetAsync(cut.Instance, secondTarget));
        await schemaService.WaitForRequestAsync(secondWorkflowId);

        schemaService.Complete(secondWorkflowId, CreateSingleInputSchema(secondWorkflowId, secondVersionId, "second"));
        await secondSelection;

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(secondWorkflowId, ReadSchedulerEditor(cut.Instance).TargetId);
            Assert.Equal(secondWorkflowId, ReadWorkflowInputSchema(cut.Instance)?.WorkflowId.Value);
            Assert.Contains(
                ReadWorkflowInputOptions(cut.Instance).SelectMany(pair => pair.Value),
                option => option.Label == "option-second");
            Assert.Equal(AgentChatContextAccessState.Ready, ReadSchedulerAgentChatAccessState(cut.Instance));
        });

        schemaService.Complete(firstWorkflowId, CreateSingleInputSchema(firstWorkflowId, firstVersionId, "first"));
        await firstSelection;

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(secondWorkflowId, ReadSchedulerEditor(cut.Instance).TargetId);
            Assert.Equal(secondWorkflowId, ReadWorkflowInputSchema(cut.Instance)?.WorkflowId.Value);
            Assert.DoesNotContain(
                ReadWorkflowInputOptions(cut.Instance).SelectMany(pair => pair.Value),
                option => option.Label == "option-first");
            Assert.Equal(AgentChatContextAccessState.Ready, ReadSchedulerAgentChatAccessState(cut.Instance));
        });
    }

    [Fact]
    public async Task Scheduler_workflow_options_keep_newer_values_when_previous_options_finish_late()
    {
        var workflowId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var target = CreateWorkflowTarget(workflowId, versionId, "Options workflow");
        var workspace = CreateTargetWorkspace(target);
        var schema = CreateSingleInputSchema(workflowId, versionId, "initial");
        var schedulerService = new RacingSchedulerPlannerService(
            workspace,
            new SchedulerPlanEditorModel
            {
                TargetKind = SchedulerPlanTargetKind.Workflow,
                TargetId = workflowId,
                TargetVersionId = versionId,
                CronExpression = "0 0 9 ? * *",
                TimeZoneId = "UTC",
                InputJson = "{}",
                IsEnabled = true
            });
        var optionService = new RacingWorkflowInputOptionService();

        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<ISchedulerPlannerService>();
            services.RemoveAll<ISchedulerWorkflowInputSchemaService>();
            services.RemoveAll<ISchedulerWorkflowInputOptionService>();
            services.AddSingleton<ISchedulerPlannerService>(schedulerService);
            services.AddSingleton<ISchedulerWorkflowInputSchemaService>(new StubSchedulerWorkflowInputSchemaService(schema));
            services.AddSingleton<ISchedulerWorkflowInputOptionService>(optionService);
        });
        var cut = harness.Context.Render<SchedulerPlannerPage>();
        cut.WaitForElement("[data-testid='scheduler-tabs']");
        cut.WaitForAssertion(() => Assert.Equal(
            AgentChatContextAccessState.Ready,
            ReadSchedulerAgentChatAccessState(cut.Instance)));
        var parameter = schema.Parameters.Single();

        var firstLoad = cut.InvokeAsync(() => InvokeWorkflowInputValueChangedAsync(cut.Instance, parameter, "first"));
        await optionService.WaitForRequestAsync("first");
        var secondLoad = cut.InvokeAsync(() => InvokeWorkflowInputValueChangedAsync(cut.Instance, parameter, "second"));
        await optionService.WaitForRequestAsync("second");

        optionService.Complete("second");
        await secondLoad;
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(
                ReadWorkflowInputOptions(cut.Instance).SelectMany(pair => pair.Value),
                option => option.Label == "option-second");
            Assert.Equal(AgentChatContextAccessState.Ready, ReadSchedulerAgentChatAccessState(cut.Instance));
        });

        optionService.Complete("first");
        await firstLoad;
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(
                ReadWorkflowInputOptions(cut.Instance).SelectMany(pair => pair.Value),
                option => option.Label == "option-second");
            Assert.DoesNotContain(
                ReadWorkflowInputOptions(cut.Instance).SelectMany(pair => pair.Value),
                option => option.Label == "option-first");
            Assert.Equal(AgentChatContextAccessState.Ready, ReadSchedulerAgentChatAccessState(cut.Instance));
        });
    }

    private static SchedulerPlanEditorModel CreateProcessEditor()
        => new()
        {
            TargetKind = SchedulerPlanTargetKind.Process,
            TargetId = Guid.NewGuid(),
            CronExpression = "0 0 9 ? * *",
            TimeZoneId = "UTC",
            InputJson = "{}",
            IsEnabled = true
        };

    private static SchedulerTargetOption CreateWorkflowTarget(Guid workflowId, Guid versionId, string name)
        => new(
            SchedulerPlanTargetKind.Workflow,
            workflowId,
            versionId,
            name,
            $"{name} description",
            "Active");

    private static SchedulerPlannerWorkspace CreateTargetWorkspace(params SchedulerTargetOption[] targets)
        => new(
            [],
            [],
            targets,
            new CanvasCalendarSurface
            {
                SurfaceId = "scheduler-planner-calendar",
                InitialView = "week",
                SelectedDate = "2026-05-12",
                Timezone = "UTC",
                Locale = "en-US"
            });

    private static SchedulerWorkflowInputSchema CreateSingleInputSchema(
        Guid workflowId,
        Guid versionId,
        string defaultValue)
        => new(
            new WorkflowId(workflowId),
            new WorkflowVersionId(versionId),
            "Workflow",
            [
                new WorkflowInputParameterDescriptor(
                    "choice",
                    "Choice",
                    WorkflowInputParameterKind.Text,
                    false,
                    "Selection value.",
                    "$.choice",
                    defaultValue,
                    WorkflowInputParameterOptionSource.None,
                    null,
                    null,
                    defaultValue)
            ],
            UsesRawJsonFallback: false);

    private static Task InvokeOpenEditScheduleDialogAsync(
        SchedulerPlannerPage page,
        SchedulerPlanSummary plan)
        => InvokePrivateTask(page, "OpenEditScheduleDialogAsync", plan);

    private static Task InvokeSelectTargetAsync(
        SchedulerPlannerPage page,
        SchedulerTargetOption target)
        => InvokePrivateTask(page, "SelectTargetAsync", target);

    private static Task InvokeWorkflowInputValueChangedAsync(
        SchedulerPlannerPage page,
        WorkflowInputParameterDescriptor parameter,
        string value)
        => InvokePrivateTask(page, "HandleWorkflowInputValueChangedAsync", parameter, value);

    private static Task InvokePrivateTask(SchedulerPlannerPage page, string methodName, params object[] arguments)
    {
        var method = typeof(SchedulerPlannerPage).GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return Assert.IsAssignableFrom<Task>(method?.Invoke(page, arguments));
    }

    private static SchedulerPlanSummary? ReadSelectedPlanForEdit(SchedulerPlannerPage page)
        => ReadPrivateField<SchedulerPlanSummary>(page, "selectedPlanForEdit");

    private static SchedulerPlanEditorModel? ReadEditScheduleEditor(SchedulerPlannerPage page)
        => ReadPrivateField<SchedulerPlanEditorModel>(page, "editScheduleEditor");

    private static SchedulerPlanEditorModel ReadSchedulerEditor(SchedulerPlannerPage page)
        => Assert.IsType<SchedulerPlanEditorModel>(ReadPrivateField<SchedulerPlanEditorModel>(page, "editor"));

    private static SchedulerWorkflowInputSchema? ReadWorkflowInputSchema(SchedulerPlannerPage page)
        => ReadPrivateField<SchedulerWorkflowInputSchema>(page, "workflowInputSchema");

    private static IReadOnlyDictionary<string, IReadOnlyList<WorkflowInputParameterOption>> ReadWorkflowInputOptions(
        SchedulerPlannerPage page)
        => Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<WorkflowInputParameterOption>>>(
            ReadPrivateField<object>(page, "workflowInputOptionsByKey"));

    private static T? ReadPrivateField<T>(SchedulerPlannerPage page, string fieldName)
    {
        var field = typeof(SchedulerPlannerPage).GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return (T?)field?.GetValue(page);
    }

    private static AgentChatContextAccessState ReadSchedulerAgentChatAccessState(SchedulerPlannerPage page)
    {
        var property = typeof(SchedulerPlannerPage).GetProperty(
            "AgentChatAccessState",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return Assert.IsType<AgentChatContextAccessState>(property?.GetValue(page));
    }

    private static AgentChatContextSurface ReadSchedulerAgentChatSurface(SchedulerPlannerPage page)
    {
        var property = typeof(SchedulerPlannerPage).GetProperty(
            "AgentChatSurface",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return Assert.IsType<AgentChatContextSurface>(property?.GetValue(page));
    }

    private sealed class RecordingSchedulerAgentChatLauncher : IAgentChatLauncher
    {
        public Guid? StartedAgentId { get; private set; }

        public void ShowCatalog(AgentChatCatalogTab tab = AgentChatCatalogTab.Agents)
        {
        }

        public Task<ActiveAgentChat> StartNewChatAsync(
            Guid agentId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartedAgentId = agentId;
            return Task.FromResult(CreateActiveChat(agentId, chatSessionId: null));
        }

        public Task<ActiveAgentChat> OpenChatAsync(
            Guid agentId,
            Guid chatSessionId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateActiveChat(agentId, chatSessionId));
        }

        private static ActiveAgentChat CreateActiveChat(Guid agentId, Guid? chatSessionId)
        {
            var now = DateTimeOffset.UtcNow;
            return new ActiveAgentChat(
                AgentChatHandleId.Create(),
                new AgentChatIdentity(
                    agentId,
                    SchedulerAgentIdentity.DefaultDisplayName,
                    "Workflow scheduling assistant",
                    SchedulerAgentIdentity.DefaultAvatarImageUrl),
                chatSessionId,
                ActiveAgentChatVisibility.Visible,
                ActiveAgentChatRunState.Idle,
                now,
                now,
                HiddenAtUtc: null);
        }
    }

    private sealed class RacingSchedulerPlannerService(
        SchedulerPlannerWorkspace workspace,
        SchedulerPlanEditorModel defaultEditor) : ISchedulerPlannerService
    {
        private readonly Dictionary<Guid, EditorRequest> editorRequests = workspace.Plans.ToDictionary(
            plan => plan.Id,
            plan => new EditorRequest(CreateEditor(plan)));

        public Task<SchedulerPlannerWorkspace> GetWorkspaceAsync(
            SchedulerHistoryQuery? historyQuery = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(workspace);

        public Task<SchedulerPlanEditorModel> CreateDefaultEditorAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(defaultEditor);

        public Task<SchedulerPlanEditorModel> GetPlanEditorAsync(
            Guid planId,
            CancellationToken cancellationToken = default)
        {
            var request = editorRequests[planId];
            request.Started.TrySetResult();
            return request.Completion.Task;
        }

        public Task<SchedulerPlanSummary> SavePlanAsync(
            SchedulerPlanEditorModel editor,
            CancellationToken cancellationToken = default)
            => Task.FromException<SchedulerPlanSummary>(new NotSupportedException());

        public Task SetPlanEnabledAsync(
            Guid planId,
            bool isEnabled,
            CancellationToken cancellationToken = default)
            => Task.FromException(new NotSupportedException());

        public Task DeletePlanAsync(Guid planId, CancellationToken cancellationToken = default)
            => Task.FromException(new NotSupportedException());

        public Task WaitForEditorRequestAsync(Guid planId)
            => editorRequests[planId].Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        public void CompleteEditorRequest(Guid planId)
            => editorRequests[planId].Completion.TrySetResult(editorRequests[planId].Editor);

        private static SchedulerPlanEditorModel CreateEditor(SchedulerPlanSummary plan)
            => new()
            {
                Id = plan.Id,
                Name = plan.Name,
                Description = plan.Description,
                TargetKind = plan.TargetKind,
                TargetId = plan.TargetId,
                TargetVersionId = plan.TargetVersionId,
                CronExpression = plan.CronExpression,
                TimeZoneId = plan.TimeZoneId,
                MisfirePolicy = plan.MisfirePolicy,
                IsEnabled = plan.IsEnabled,
                InputJson = "{}"
            };

        private sealed record EditorRequest(SchedulerPlanEditorModel Editor)
        {
            public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource<SchedulerPlanEditorModel> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    private sealed class RacingSchedulerWorkflowInputSchemaService : ISchedulerWorkflowInputSchemaService
    {
        private readonly Dictionary<Guid, SchemaRequest> requests = [];

        public Task<SchedulerWorkflowInputSchema> ResolveSchemaAsync(
            WorkflowId workflowId,
            WorkflowVersionId? versionId = null,
            CancellationToken cancellationToken = default)
        {
            var request = GetRequest(workflowId.Value);
            request.Started.TrySetResult();
            return request.Completion.Task;
        }

        public Task<SchedulerWorkflowInputValidationResult> ValidateInputAsync(
            WorkflowId workflowId,
            WorkflowVersionId? versionId,
            string? inputJson,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new SchedulerWorkflowInputValidationResult(true, inputJson ?? "{}", []));

        public Task WaitForRequestAsync(Guid workflowId)
            => GetRequest(workflowId).Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        public void Complete(Guid workflowId, SchedulerWorkflowInputSchema schema)
            => GetRequest(workflowId).Completion.TrySetResult(schema);

        private SchemaRequest GetRequest(Guid workflowId)
        {
            if (!requests.TryGetValue(workflowId, out var request))
            {
                request = new SchemaRequest();
                requests.Add(workflowId, request);
            }

            return request;
        }

        private sealed class SchemaRequest
        {
            public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource<SchedulerWorkflowInputSchema> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    private sealed class ValueEchoingWorkflowInputOptionService : ISchedulerWorkflowInputOptionService
    {
        public Task<IReadOnlyList<WorkflowInputParameterOption>> ListOptionsAsync(
            WorkflowInputParameterDescriptor parameter,
            IReadOnlyDictionary<string, string> currentValues,
            CancellationToken cancellationToken = default)
        {
            currentValues.TryGetValue(parameter.Key, out var value);
            IReadOnlyList<WorkflowInputParameterOption> options =
            [
                new(value ?? string.Empty, $"option-{value}", string.Empty)
            ];
            return Task.FromResult(options);
        }
    }

    private sealed class RacingWorkflowInputOptionService : ISchedulerWorkflowInputOptionService
    {
        private readonly Dictionary<string, OptionRequest> requests = new(StringComparer.Ordinal);

        public Task<IReadOnlyList<WorkflowInputParameterOption>> ListOptionsAsync(
            WorkflowInputParameterDescriptor parameter,
            IReadOnlyDictionary<string, string> currentValues,
            CancellationToken cancellationToken = default)
        {
            currentValues.TryGetValue(parameter.Key, out var value);
            value ??= string.Empty;
            if (value is not ("first" or "second"))
            {
                IReadOnlyList<WorkflowInputParameterOption> immediate =
                [
                    new(value, $"option-{value}", string.Empty)
                ];
                return Task.FromResult(immediate);
            }

            var request = GetRequest(value);
            request.Started.TrySetResult();
            return request.Completion.Task;
        }

        public Task WaitForRequestAsync(string value)
            => GetRequest(value).Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        public void Complete(string value)
        {
            IReadOnlyList<WorkflowInputParameterOption> options =
            [
                new(value, $"option-{value}", string.Empty)
            ];
            GetRequest(value).Completion.TrySetResult(options);
        }

        private OptionRequest GetRequest(string value)
        {
            if (!requests.TryGetValue(value, out var request))
            {
                request = new OptionRequest();
                requests.Add(value, request);
            }

            return request;
        }

        private sealed class OptionRequest
        {
            public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource<IReadOnlyList<WorkflowInputParameterOption>> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
