using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Automation;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.SchedulerPlanner.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.Tests.Components;

public sealed class SchedulerPlannerPageTests
{
    [Fact]
    public async Task Scheduler_page_renders_tabs_and_canvas_calendar_host()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();

        var cut = harness.Context.RenderComponent<SchedulerPlannerPage>();

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

        Assert.Contains("Scheduled runs", cut.Markup);
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

        var cut = harness.Context.RenderComponent<SchedulerPlannerPage>();

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

        var cut = harness.Context.RenderComponent<SchedulerPlannerPage>();

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

        var cut = harness.Context.RenderComponent<SchedulerPlannerPage>();

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

        var cut = harness.Context.RenderComponent<SchedulerPlannerPage>();

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

        var cut = harness.Context.RenderComponent<SchedulerPlannerPage>();

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

        public Task<SchedulerPlanSummary> SavePlanAsync(
            SchedulerPlanEditorModel editor,
            CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(new SchedulerPlanSummary(
                Guid.NewGuid(),
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
}
