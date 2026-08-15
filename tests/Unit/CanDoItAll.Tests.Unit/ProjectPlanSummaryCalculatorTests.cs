using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectPlanSummaryCalculatorTests
{
    private static readonly Guid ProjectId = Guid.Parse("9f583715-e543-4cc0-9134-614221f880d8");
    private static readonly DateTimeOffset AsOfUtc = new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Build_groups_expected_cost_by_currency_and_reports_overlapping_resource_coverage()
    {
        var tasks = new[]
        {
            CreateTask("task-a", metadataJson: CreateEstimateMetadata(8m, 100m, "USD")),
            CreateTask("task-b", metadataJson: CreateEstimateMetadata(4m, 50m, "USD")),
            CreateTask("task-c", metadataJson: CreateEstimateMetadata(16m, 80m, "EUR"))
        };
        var bindings = new[]
        {
            new ProjectPlanResourceBindingFact("task-a", ProjectPlanResourceGroup.Person, "person:1"),
            new ProjectPlanResourceBindingFact("task-a", ProjectPlanResourceGroup.Person, "person:1"),
            new ProjectPlanResourceBindingFact("task-a", ProjectPlanResourceGroup.Agent, "agent:1"),
            new ProjectPlanResourceBindingFact("task-b", ProjectPlanResourceGroup.Person, "person:2"),
            new ProjectPlanResourceBindingFact("task-c", ProjectPlanResourceGroup.Workflow, "workflow:1")
        };

        var summary = Build(tasks, resourceBindings: bindings);

        Assert.Collection(
            summary.ExpectedCostTotals,
            eur =>
            {
                Assert.Equal("EUR", eur.CurrencyCode);
                Assert.Equal(80m, eur.Amount);
                Assert.Equal(1, eur.PricedTaskCount);
            },
            usd =>
            {
                Assert.Equal("USD", usd.CurrencyCode);
                Assert.Equal(150m, usd.Amount);
                Assert.Equal(2, usd.PricedTaskCount);
            });
        Assert.Equal(28m, summary.TotalExpectedEffortHours);
        Assert.Equal(3.5m, summary.TotalExpectedEffortManDays);

        var person = ResourceSummary(summary, ProjectPlanResourceGroup.Person);
        Assert.Equal(2, person.BindingCount);
        Assert.Equal(50m, person.BindingSharePercent);
        Assert.Equal(2, person.CoveredTaskCount);
        Assert.Equal(66.67m, person.TaskCoveragePercent);
        Assert.Equal(1, person.ExclusiveTaskCount);

        var agent = ResourceSummary(summary, ProjectPlanResourceGroup.Agent);
        Assert.Equal(1, agent.BindingCount);
        Assert.Equal(25m, agent.BindingSharePercent);
        Assert.Equal(1, agent.CoveredTaskCount);
        Assert.Equal(0, agent.ExclusiveTaskCount);

        var mixed = ResourceSummary(summary, ProjectPlanResourceGroup.Mixed);
        Assert.Equal(0, mixed.BindingCount);
        Assert.Equal(1, mixed.CoveredTaskCount);
        Assert.Equal(1, mixed.ExclusiveTaskCount);
        Assert.Equal(1, summary.Completeness.MixedResourceTaskCount);
    }

    [Fact]
    public void Build_projects_only_nonterminal_expected_cost_and_keeps_resource_currency_and_date()
    {
        var scheduledStartUtc = AsOfUtc.AddDays(2);
        var tasks = new[]
        {
            CreateTask(
                "agent",
                startUtc: scheduledStartUtc,
                endUtc: scheduledStartUtc.AddHours(2),
                metadataJson: CreateEstimateMetadata(expectedCostAmount: 100m, expectedCostCurrencyCode: "USD")),
            CreateTask(
                "external-overdue",
                progressPercent: 50,
                startUtc: AsOfUtc.AddDays(-2),
                endUtc: AsOfUtc.AddDays(1),
                metadataJson: CreateEstimateMetadata(expectedCostAmount: 50m, expectedCostCurrencyCode: "USD")),
            CreateTask(
                "mixed-unscheduled",
                metadataJson: CreateEstimateMetadata(expectedCostAmount: 20m, expectedCostCurrencyCode: "EUR")),
            CreateTask(
                "completed",
                status: "done",
                progressPercent: 100,
                metadataJson: CreateEstimateMetadata(expectedCostAmount: 75m, expectedCostCurrencyCode: "USD"))
        };
        var bindings = new[]
        {
            new ProjectPlanResourceBindingFact("agent", ProjectPlanResourceGroup.Agent, "agent:1"),
            new ProjectPlanResourceBindingFact("external-overdue", ProjectPlanResourceGroup.External, "organization:1"),
            new ProjectPlanResourceBindingFact("mixed-unscheduled", ProjectPlanResourceGroup.Person, "person:1"),
            new ProjectPlanResourceBindingFact("mixed-unscheduled", ProjectPlanResourceGroup.Agent, "agent:2"),
            new ProjectPlanResourceBindingFact("completed", ProjectPlanResourceGroup.Process, "process:1")
        };

        var summary = Build(tasks, resourceBindings: bindings);

        Assert.Collection(
            summary.FutureExpectedCostTotals,
            mixed =>
            {
                Assert.Equal(ProjectPlanResourceGroup.Mixed, mixed.Group);
                Assert.Equal("EUR", mixed.CurrencyCode);
                Assert.Equal(20m, mixed.Amount);
            },
            agent =>
            {
                Assert.Equal(ProjectPlanResourceGroup.Agent, agent.Group);
                Assert.Equal("USD", agent.CurrencyCode);
                Assert.Equal(100m, agent.Amount);
            },
            external =>
            {
                Assert.Equal(ProjectPlanResourceGroup.External, external.Group);
                Assert.Equal("USD", external.CurrencyCode);
                Assert.Equal(25m, external.Amount);
            });
        Assert.Equal(1, summary.UnscheduledFutureExpectedCostTaskCount);
        Assert.Collection(
            summary.FutureExpectedCostTrend,
            overdue =>
            {
                Assert.Equal(DateOnly.FromDateTime(AsOfUtc.AddDays(1).UtcDateTime), overdue.Date);
                Assert.Equal(ProjectPlanResourceGroup.External, overdue.Group);
                Assert.Equal(25m, overdue.Amount);
            },
            scheduled =>
            {
                Assert.Equal(DateOnly.FromDateTime(scheduledStartUtc.AddHours(2).UtcDateTime), scheduled.Date);
                Assert.Equal(ProjectPlanResourceGroup.Agent, scheduled.Group);
                Assert.Equal(100m, scheduled.Amount);
            });
        Assert.DoesNotContain(
            summary.FutureExpectedCostTotals,
            total => total.Group == ProjectPlanResourceGroup.Process);
    }

    [Fact]
    public void BuildManagerSummary_schedule_only_calculates_schedule_without_forecast_work()
    {
        var snapshot = new ProjectPlanManagerScheduleSnapshot(
            ProjectId,
            "Lean schedule",
            [
                new ProjectPlanScheduleTaskFact(AsOfUtc.AddHours(-2), AsOfUtc),
                new ProjectPlanScheduleTaskFact(AsOfUtc.AddHours(1), AsOfUtc.AddHours(3)),
                new ProjectPlanScheduleTaskFact(null, AsOfUtc.AddHours(4)),
                new ProjectPlanScheduleTaskFact(AsOfUtc.AddHours(5), AsOfUtc.AddHours(4))
            ]);

        var summary = new ProjectPlanSummaryCalculator().BuildManagerSummary(
            snapshot,
            new ProjectPlanManagerSummaryQuery(
                ProjectPlanManagerSummaryMode.ScheduleOnly,
                AsOfUtc));

        Assert.Equal(4, summary.TotalTaskCount);
        Assert.Equal(AsOfUtc.AddHours(-2), summary.Schedule.EarliestStartUtc);
        Assert.Equal(AsOfUtc.AddHours(3), summary.Schedule.LatestEndUtc);
        Assert.Equal(5m, summary.Schedule.DeliveryLeadTimeHours);
        Assert.Equal(4m, summary.Schedule.ScheduledTaskDurationHours);
        Assert.Empty(summary.FutureExpectedCostTotals);
        Assert.Empty(summary.FutureExpectedCostTrend);
        Assert.Equal(0, summary.UnscheduledFutureExpectedCostTaskCount);
        Assert.Contains(
            summary.Warnings,
            warning => warning.Contains("incomplete schedule", StringComparison.Ordinal));
        Assert.Contains(
            summary.Warnings,
            warning => warning.Contains("end before they start", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildManagerSummary_forecast_calculates_remaining_cost_without_dependency_state_graph()
    {
        var snapshot = new ProjectPlanManagerForecastSnapshot(
            ProjectId,
            "Lean forecast",
            [
                CreateTask(
                    "agent",
                    progressPercent: 25,
                    startUtc: AsOfUtc.AddDays(1),
                    endUtc: AsOfUtc.AddDays(2),
                    metadataJson: CreateEstimateMetadata(
                        expectedCostAmount: 100m,
                        expectedCostCurrencyCode: "USD")),
                CreateTask(
                    "external",
                    progressPercent: 101,
                    metadataJson: CreateEstimateMetadata(
                        expectedCostAmount: 50m,
                        expectedCostCurrencyCode: "USD")),
                CreateTask(
                    "completed",
                    status: "done",
                    metadataJson: CreateEstimateMetadata(
                        expectedCostAmount: 200m,
                        expectedCostCurrencyCode: "USD")),
                CreateTask("unpriced")
            ],
            [
                new ProjectPlanResourceBindingFact(
                    "agent",
                    ProjectPlanResourceGroup.Agent,
                    "agent:1"),
                new ProjectPlanResourceBindingFact(
                    "external",
                    ProjectPlanResourceGroup.External,
                    "organization:1"),
                new ProjectPlanResourceBindingFact(
                    "completed",
                    ProjectPlanResourceGroup.Process,
                    "process:1")
            ]);

        var summary = new ProjectPlanSummaryCalculator().BuildManagerSummary(
            snapshot,
            new ProjectPlanManagerSummaryQuery(
                ProjectPlanManagerSummaryMode.ScheduleAndRemainingCosts,
                AsOfUtc));

        Assert.Collection(
            summary.FutureExpectedCostTotals,
            agent =>
            {
                Assert.Equal(ProjectPlanResourceGroup.Agent, agent.Group);
                Assert.Equal(75m, agent.Amount);
            },
            external =>
            {
                Assert.Equal(ProjectPlanResourceGroup.External, external.Group);
                Assert.Equal(50m, external.Amount);
            });
        var trend = Assert.Single(summary.FutureExpectedCostTrend);
        Assert.Equal(DateOnly.FromDateTime(AsOfUtc.AddDays(2).UtcDateTime), trend.Date);
        Assert.Equal(75m, trend.Amount);
        Assert.Equal(1, summary.UnscheduledFutureExpectedCostTaskCount);
        Assert.DoesNotContain(
            summary.FutureExpectedCostTotals,
            total => total.Group == ProjectPlanResourceGroup.Process);
        Assert.Contains(
            summary.Warnings,
            warning => warning.Contains("no expected cost", StringComparison.Ordinal));
        Assert.Contains(
            summary.Warnings,
            warning => warning.Contains("outside the supported 0-100", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildManagerSummary_forecast_spreads_started_tasks_across_completion_dates()
    {
        var snapshot = new ProjectPlanManagerForecastSnapshot(
            ProjectId,
            "Completion forecast",
            [
                CreateTask(
                    "near",
                    progressPercent: 50,
                    startUtc: AsOfUtc.AddDays(-4),
                    endUtc: AsOfUtc.AddDays(1),
                    metadataJson: CreateEstimateMetadata(
                        expectedCostAmount: 100m,
                        expectedCostCurrencyCode: "USD")),
                CreateTask(
                    "far",
                    progressPercent: 25,
                    startUtc: AsOfUtc.AddDays(-2),
                    endUtc: AsOfUtc.AddDays(4),
                    metadataJson: CreateEstimateMetadata(
                        expectedCostAmount: 200m,
                        expectedCostCurrencyCode: "USD")),
                CreateTask(
                    "overdue",
                    progressPercent: 50,
                    startUtc: AsOfUtc.AddDays(-5),
                    endUtc: AsOfUtc.AddDays(-1),
                    metadataJson: CreateEstimateMetadata(
                        expectedCostAmount: 50m,
                        expectedCostCurrencyCode: "USD"))
            ],
            []);

        var summary = new ProjectPlanSummaryCalculator().BuildManagerSummary(
            snapshot,
            new ProjectPlanManagerSummaryQuery(
                ProjectPlanManagerSummaryMode.ScheduleAndRemainingCosts,
                AsOfUtc));

        Assert.Collection(
            summary.FutureExpectedCostTrend,
            overdue =>
            {
                Assert.Equal(DateOnly.FromDateTime(AsOfUtc.UtcDateTime), overdue.Date);
                Assert.Equal(25m, overdue.Amount);
            },
            near =>
            {
                Assert.Equal(DateOnly.FromDateTime(AsOfUtc.AddDays(1).UtcDateTime), near.Date);
                Assert.Equal(50m, near.Amount);
            },
            far =>
            {
                Assert.Equal(DateOnly.FromDateTime(AsOfUtc.AddDays(4).UtcDateTime), far.Date);
                Assert.Equal(150m, far.Amount);
            });
        Assert.Equal(
            summary.FutureExpectedCostTotals.Sum(static total => total.Amount),
            summary.FutureExpectedCostTrend.Sum(static point => point.Amount));
        Assert.DoesNotContain(
            summary.FutureExpectedCostTrend,
            static point => point.Amount == 225m);
    }

    [Fact]
    public void Build_interprets_depends_on_direction_and_classifies_task_states()
    {
        var tasks = new[]
        {
            CreateTask("prerequisite", status: "waiting", startUtc: AsOfUtc.AddHours(-2), endUtc: AsOfUtc.AddHours(2)),
            CreateTask("dependent", status: "running", startUtc: AsOfUtc.AddHours(-1), endUtc: AsOfUtc.AddHours(3)),
            CreateTask("running", progressPercent: 25, startUtc: AsOfUtc.AddHours(-1), endUtc: AsOfUtc.AddHours(1)),
            CreateTask("planned", startUtc: AsOfUtc.AddHours(1), endUtc: AsOfUtc.AddHours(2)),
            CreateTask("ready", startUtc: AsOfUtc.AddHours(-3), endUtc: AsOfUtc.AddHours(-1)),
            CreateTask("unscheduled"),
            CreateTask("completed", status: "done"),
            CreateTask("cancelled", status: "cancelled")
        };
        var links = new[]
        {
            new ProjectPlanLinkFact("dependent", "prerequisite", ProjectObjectLinkKind.DependsOn)
        };

        var summary = Build(tasks, links);

        Assert.Equal(1, StateCount(summary, ProjectPlanTaskState.Blocked));
        Assert.Equal(1, StateCount(summary, ProjectPlanTaskState.Waiting));
        Assert.Equal(1, StateCount(summary, ProjectPlanTaskState.Running));
        Assert.Equal(1, StateCount(summary, ProjectPlanTaskState.Planned));
        Assert.Equal(1, StateCount(summary, ProjectPlanTaskState.Ready));
        Assert.Equal(1, StateCount(summary, ProjectPlanTaskState.Unscheduled));
        Assert.Equal(1, StateCount(summary, ProjectPlanTaskState.Completed));
        Assert.Equal(1, StateCount(summary, ProjectPlanTaskState.Cancelled));

        var blocked = Assert.Single(summary.BlockedTasks);
        Assert.Equal("dependent", blocked.NodeId);
        Assert.Equal(1, blocked.BlockingTaskCount);
        Assert.Equal(["prerequisite"], blocked.BlockingTaskNodeIds);
        Assert.Equal("prerequisite", Assert.Single(summary.WaitingTasks).NodeId);
        Assert.Equal("running", Assert.Single(summary.RunningTasks).NodeId);
    }

    [Fact]
    public void Build_limits_and_orders_each_task_preview()
    {
        var tasks = new[]
        {
            CreateTask("running-3", title: "Third", progressPercent: 10, startUtc: AsOfUtc.AddHours(-1), endUtc: AsOfUtc.AddHours(3)),
            CreateTask("running-1", title: "First", progressPercent: 10, startUtc: AsOfUtc.AddHours(-3), endUtc: AsOfUtc.AddHours(1)),
            CreateTask("running-2", title: "Second", progressPercent: 10, startUtc: AsOfUtc.AddHours(-2), endUtc: AsOfUtc.AddHours(2))
        };

        var summary = Build(tasks, query: new ProjectPlanSummaryQuery(AsOfUtc, TaskPreviewLimit: 2));

        Assert.Equal(3, StateCount(summary, ProjectPlanTaskState.Running));
        Assert.Equal(["running-1", "running-2"], summary.RunningTasks.Select(item => item.NodeId));
    }

    [Fact]
    public void Build_allows_aggregate_only_summary_without_task_previews()
    {
        var tasks = new[]
        {
            CreateTask("running", progressPercent: 10)
        };

        var summary = Build(tasks, query: new ProjectPlanSummaryQuery(AsOfUtc, TaskPreviewLimit: 0));

        Assert.Equal(1, StateCount(summary, ProjectPlanTaskState.Running));
        Assert.Empty(summary.RunningTasks);
        Assert.Empty(summary.BlockedTasks);
        Assert.Empty(summary.WaitingTasks);
    }

    [Fact]
    public void Build_reports_full_blocker_count_and_bounds_deterministic_id_sample()
    {
        var prerequisites = Enumerable.Range(0, 25)
            .Select(index => CreateTask($"prerequisite-{index:D2}"))
            .ToArray();
        var tasks = prerequisites.Append(CreateTask("dependent")).ToArray();
        var links = prerequisites
            .Select(prerequisite => new ProjectPlanLinkFact(
                "dependent",
                prerequisite.NodeId,
                ProjectObjectLinkKind.DependsOn))
            .ToArray();

        var summary = Build(tasks, links);

        var blocked = Assert.Single(summary.BlockedTasks);
        Assert.Equal(25, blocked.BlockingTaskCount);
        Assert.Equal(ProjectPlanSummaryCalculator.MaximumBlockingTaskIdPreview, blocked.BlockingTaskNodeIds.Count);
        Assert.Equal(
            Enumerable.Range(0, ProjectPlanSummaryCalculator.MaximumBlockingTaskIdPreview)
                .Select(index => $"prerequisite-{index:D2}"),
            blocked.BlockingTaskNodeIds);
    }

    [Fact]
    public void Build_marks_cycle_and_downstream_tasks_as_dependency_cycle_affected()
    {
        var tasks = new[]
        {
            CreateTask("cycle-a"),
            CreateTask("cycle-b"),
            CreateTask("downstream"),
            CreateTask("independent")
        };
        var links = new[]
        {
            new ProjectPlanLinkFact("cycle-a", "cycle-b", ProjectObjectLinkKind.DependsOn),
            new ProjectPlanLinkFact("cycle-b", "cycle-a", ProjectObjectLinkKind.DependsOn),
            new ProjectPlanLinkFact("downstream", "cycle-a", ProjectObjectLinkKind.DependsOn)
        };

        var summary = Build(tasks, links);

        Assert.Equal(3, summary.Completeness.DependencyCycleAffectedTaskCount);
        Assert.Equal(3, StateCount(summary, ProjectPlanTaskState.Blocked));
        Assert.Equal(1, StateCount(summary, ProjectPlanTaskState.Unscheduled));
        Assert.Contains(summary.Warnings, warning =>
            warning.Contains("dependency cycle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_excludes_invalid_estimate_metadata_and_invalid_schedule_from_totals()
    {
        var tasks = new[]
        {
            CreateTask(
                "invalid",
                startUtc: AsOfUtc.AddHours(2),
                endUtc: AsOfUtc.AddHours(1),
                metadataJson: "{invalid-json")
        };

        var summary = Build(tasks);

        Assert.Equal(1, summary.Completeness.InvalidMetadataTaskCount);
        Assert.Equal(1, summary.Completeness.MissingEffortTaskCount);
        Assert.Equal(1, summary.Completeness.MissingExpectedCostTaskCount);
        Assert.Equal(0, summary.Completeness.MissingScheduleTaskCount);
        Assert.Equal(1, summary.Completeness.InvalidScheduleTaskCount);
        Assert.Equal(0m, summary.TotalExpectedEffortHours);
        Assert.Empty(summary.ExpectedCostTotals);
        Assert.Null(summary.Schedule.EarliestStartUtc);
        Assert.Null(summary.Schedule.LatestEndUtc);
        Assert.Null(summary.Schedule.DeliveryLeadTimeHours);
        Assert.Equal(0m, summary.Schedule.ScheduledTaskDurationHours);
        Assert.Contains(summary.Warnings, warning =>
            warning.Contains("invalid estimate metadata", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(summary.Warnings, warning =>
            warning.Contains("end before they start", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_ignores_incomplete_schedule_boundaries()
    {
        var summary = Build([
            CreateTask("end-only", endUtc: AsOfUtc.AddDays(30))
        ]);

        Assert.Null(summary.Schedule.EarliestStartUtc);
        Assert.Null(summary.Schedule.LatestEndUtc);
        Assert.Null(summary.Schedule.DeliveryLeadTimeHours);
        Assert.Equal(0m, summary.Schedule.ScheduledTaskDurationHours);
        Assert.Equal(1, summary.Completeness.MissingScheduleTaskCount);
        Assert.Equal(1, StateCount(summary, ProjectPlanTaskState.Unscheduled));
    }

    [Fact]
    public void Build_does_not_treat_out_of_range_progress_as_completion()
    {
        var summary = Build([
            CreateTask("corrupt-progress", progressPercent: 101)
        ]);

        Assert.Equal(0, StateCount(summary, ProjectPlanTaskState.Completed));
        Assert.Equal(1, StateCount(summary, ProjectPlanTaskState.Unscheduled));
        Assert.Equal(1, summary.Completeness.InvalidProgressTaskCount);
        Assert.Contains(summary.Warnings, warning =>
            warning.Contains("progress outside", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_treats_untracked_progress_as_missing_instead_of_invalid()
    {
        var summary = Build([
            CreateTask("untracked-progress", progressPercent: ProjectProgressPolicy.UntrackedPercent)
        ]);

        Assert.Equal(1, summary.Completeness.MissingProgressTaskCount);
        Assert.Equal(0, summary.Completeness.InvalidProgressTaskCount);
        Assert.DoesNotContain(summary.Warnings, warning =>
            warning.Contains("progress outside", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_prefers_explicit_cancellation_over_stale_completed_progress()
    {
        var summary = Build([
            CreateTask("cancelled", status: "cancelled", progressPercent: 100)
        ]);

        Assert.Equal(1, StateCount(summary, ProjectPlanTaskState.Cancelled));
        Assert.Equal(0, StateCount(summary, ProjectPlanTaskState.Completed));
    }

    [Fact]
    public void Build_does_not_block_task_downstream_from_completed_dependency_cycle()
    {
        var tasks = new[]
        {
            CreateTask("completed-a", status: "done", progressPercent: 100),
            CreateTask("completed-b", status: "done", progressPercent: 100),
            CreateTask("downstream")
        };
        var links = new[]
        {
            new ProjectPlanLinkFact("completed-a", "completed-b", ProjectObjectLinkKind.DependsOn),
            new ProjectPlanLinkFact("completed-b", "completed-a", ProjectObjectLinkKind.DependsOn),
            new ProjectPlanLinkFact("downstream", "completed-a", ProjectObjectLinkKind.DependsOn)
        };

        var summary = Build(tasks, links);

        Assert.Equal(3, summary.Completeness.DependencyCycleAffectedTaskCount);
        Assert.Equal(0, StateCount(summary, ProjectPlanTaskState.Blocked));
        Assert.Equal(2, StateCount(summary, ProjectPlanTaskState.Completed));
        Assert.Equal(1, StateCount(summary, ProjectPlanTaskState.Unscheduled));
    }

    [Fact]
    public void SerializeResponseSummary_uses_total_state_counts_when_previews_are_disabled()
    {
        var summary = Build(
            [
                CreateTask("running-a", progressPercent: 10),
                CreateTask("running-b", progressPercent: 20),
                CreateTask("running-c", progressPercent: 30)
            ],
            query: new ProjectPlanSummaryQuery(AsOfUtc, TaskPreviewLimit: 0));

        using var document = System.Text.Json.JsonDocument.Parse(
            ProjectStructureAnalyticsService.SerializeResponseSummary(summary));

        Assert.Equal(3, document.RootElement.GetProperty("runningTaskCount").GetInt32());
        Assert.Equal(0, document.RootElement.GetProperty("blockedTaskCount").GetInt32());
        Assert.Equal(0, document.RootElement.GetProperty("waitingTaskCount").GetInt32());
    }

    [Fact]
    public void BuildAssigneeBindings_isolates_project_task_and_maps_internal_and_external_parties()
    {
        var personId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var bindings = ProjectPlanAnalyticsQueryService.BuildAssigneeBindings(
            ProjectId,
            new HashSet<string>(["task-a"], StringComparer.Ordinal),
            [
                new ProjectWorkItemAssigneeBinding(ProjectId, "task-a", personId, ProjectPartyType.Person),
                new ProjectWorkItemAssigneeBinding(Guid.NewGuid(), "task-a", Guid.NewGuid(), ProjectPartyType.AiAgent),
                new ProjectWorkItemAssigneeBinding(ProjectId, "task-a", organizationId, ProjectPartyType.Organization),
                new ProjectWorkItemAssigneeBinding(ProjectId, "task-b", Guid.NewGuid(), ProjectPartyType.AiAgent)
            ]);

        Assert.Collection(
            bindings,
            person =>
            {
                Assert.Equal("task-a", person.TaskNodeId);
                Assert.Equal(ProjectPlanResourceGroup.Person, person.Group);
                Assert.Equal(personId.ToString("D"), person.ResourceKey);
            },
            organization =>
            {
                Assert.Equal("task-a", organization.TaskNodeId);
                Assert.Equal(ProjectPlanResourceGroup.External, organization.Group);
                Assert.Equal(organizationId.ToString("D"), organization.ResourceKey);
            });
    }

    [Fact]
    public void Build_treats_invalid_persisted_effort_unit_as_incomplete_metadata()
    {
        var metadataJson = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
        {
            WorkItem = new ProjectWorkItemMetadata
            {
                WorkItemKind = ProjectWorkItemKind.Task,
                ExpectedEffortHours = 8m,
                ExpectedEffortUnit = (ProjectWorkItemEffortUnit)999
            }
        });

        var summary = Build([
            CreateTask("corrupt-unit", metadataJson: metadataJson)
        ]);

        Assert.Equal(1, summary.Completeness.InvalidMetadataTaskCount);
        Assert.Equal(1, summary.Completeness.MissingEffortTaskCount);
        Assert.Equal(0m, summary.TotalExpectedEffortHours);
    }

    [Theory]
    [InlineData(-1, 8)]
    [InlineData(101, 8)]
    [InlineData(0, 0.5)]
    [InlineData(0, 25)]
    public void Build_rejects_out_of_range_query_values(int previewLimit, double hoursPerManDay)
    {
        var query = new ProjectPlanSummaryQuery(
            AsOfUtc,
            previewLimit,
            (decimal)hoursPerManDay);

        Assert.Throws<ArgumentOutOfRangeException>(() => Build([], query: query));
    }

    [Fact]
    public void Build_honors_pre_cancelled_token_before_entering_large_calculator_loops()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var snapshot = new ProjectPlanSnapshot(
            ProjectId,
            "Cancelled",
            [CreateTask("task")],
            [],
            []);

        Assert.Throws<OperationCanceledException>(() =>
            new ProjectPlanSummaryCalculator().Build(
                snapshot,
                new ProjectPlanSummaryQuery(AsOfUtc),
                cancellation.Token));
    }

    private static ProjectPlanSummary Build(
        IReadOnlyList<ProjectPlanTaskFact> tasks,
        IReadOnlyList<ProjectPlanLinkFact>? links = null,
        IReadOnlyList<ProjectPlanResourceBindingFact>? resourceBindings = null,
        ProjectPlanSummaryQuery? query = null)
    {
        var snapshot = new ProjectPlanSnapshot(
            ProjectId,
            "Unit test plan",
            tasks,
            links ?? [],
            resourceBindings ?? []);
        return new ProjectPlanSummaryCalculator().Build(
            snapshot,
            query ?? new ProjectPlanSummaryQuery(AsOfUtc));
    }

    private static ProjectPlanTaskFact CreateTask(
        string nodeId,
        string? title = null,
        string status = "",
        int progressPercent = 0,
        DateTimeOffset? startUtc = null,
        DateTimeOffset? endUtc = null,
        string? metadataJson = null)
    {
        return new ProjectPlanTaskFact(
            nodeId,
            title ?? nodeId,
            status,
            progressPercent,
            startUtc,
            endUtc,
            metadataJson ?? CreateEstimateMetadata());
    }

    private static string CreateEstimateMetadata(
        decimal? expectedEffortHours = null,
        decimal? expectedCostAmount = null,
        string expectedCostCurrencyCode = "")
    {
        return ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
        {
            WorkItem = new ProjectWorkItemMetadata
            {
                WorkItemKind = ProjectWorkItemKind.Task,
                ExpectedEffortHours = expectedEffortHours,
                ExpectedEffortUnit = ProjectWorkItemEffortUnit.Hours,
                ExpectedCostAmount = expectedCostAmount,
                ExpectedCostCurrencyCode = expectedCostCurrencyCode
            }
        });
    }

    private static int StateCount(ProjectPlanSummary summary, ProjectPlanTaskState state)
    {
        return summary.TaskStates.Single(item => item.State == state).TaskCount;
    }

    private static ProjectPlanResourceGroupSummary ResourceSummary(
        ProjectPlanSummary summary,
        ProjectPlanResourceGroup group)
    {
        return summary.ResourceGroups.Single(item => item.Group == group);
    }
}
