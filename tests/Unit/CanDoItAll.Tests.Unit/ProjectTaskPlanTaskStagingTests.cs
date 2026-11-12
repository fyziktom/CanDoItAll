using System.Text;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectTaskPlanTaskStagingTests {
    private static readonly DateTimeOffset Instant = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Proposal_captures_normalized_ordered_values_without_mutable_list_aliases() {
        var items = new List<ProjectTaskPlanDraftItem> {
            new("  First  ", " Description ", " Notes ", 0), new("Second", "", "", 6)
        };
        var proposal = ProjectTaskPlanProposal.Normalize(new(1, items));
        items[0] = new("Replaced", "", "", 1);
        items.Clear();
        Assert.Equal(new("First", "Description", "Notes", 0), proposal.Tasks[0]);
        Assert.Equal("Second", proposal.Tasks[1].Title);
        Assert.False(proposal.Tasks is ProjectTaskPlanDraftItem[]);
    }

    [Fact]
    public void Version_count_title_and_priority_are_explicit_refusals() {
        Invalid(() => ProjectTaskPlanProposal.Normalize(new(0, [Item()])));
        Invalid(() => ProjectTaskPlanProposal.Normalize(new(1, [])));
        Limited(() => ProjectTaskPlanProposal.Normalize(new(1, Enumerable.Repeat(Item(), 101).ToArray())));
        Invalid(() => Normalize(Item() with { Title = " \t" }));
        Limited(() => Normalize(Item() with { Title = new string('a', 201) }));
        Invalid(() => Normalize(Item() with { Priority = -1 }));
        Invalid(() => Normalize(Item() with { Priority = 7 }));
        Assert.Equal(200, Normalize(Item() with { Title = new string('a', 200) }).Tasks[0].Title.Length);
    }

    [Fact]
    public void Raw_aggregate_malformed_text_and_actual_encoded_proposal_are_bounded() {
        var large = Item() with { Description = new string('a', 65_536), Notes = new string('b', 65_536) };
        Limited(() => ProjectTaskPlanProposal.Normalize(new(1, [large, large])));
        Invalid(() => Normalize(Item() with { Description = "\ud800" }));
        Invalid(() => Normalize(Item() with { Notes = null! }));
        Limited(() => Normalize(Item() with { Notes = new string('\u00e9', 32_769) }));
        var escaped = Item() with { Notes = new string('\u0001', 60_000) };
        Limited(() => ProjectTaskPlanProposal.Normalize(new(1, Enumerable.Repeat(escaped, 4).ToArray())));
    }

    [Fact]
    public void Plain_task_defaults_have_no_scheduling_assignment_estimate_or_external_effect() {
        using var context = Context();
        var current = Current();
        var result = Stage(context, current, Normalize(Item()));
        var row = Assert.Single(context.ChangeTracker.Entries<ProjectObjectRecord>()).Entity;
        var metadata = ProjectObjectMetadataSerializer.Parse(row.MetadataJson).WorkItem!;
        Assert.Equal(ProjectObjectType.WorkItem, row.ObjectType);
        Assert.Equal(ProjectObjectSubtypePolicy.Task, row.ObjectSubtype);
        Assert.Equal(ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(row.ProjectId), row.ParentNodeKey);
        Assert.Equal("Task", row.Subtitle);
        Assert.Equal("Draft", row.Status);
        Assert.Equal(("progress", 28), (row.ProgressMode, row.ProgressPercent));
        Assert.Equal((760d, 100d), (row.PositionX, row.PositionY));
        Assert.Null(row.StartUtc);
        Assert.Null(row.EndUtc);
        Assert.Null(row.DurationSeconds);
        Assert.Equal("[]", row.MarkersJson);
        Assert.False(row.IsSystemManaged);
        Assert.Null(metadata.ExpectedEffortHours);
        Assert.Null(metadata.ExpectedCostAmount);
        Assert.Null(metadata.ExpectedCostBasis);
        Assert.Equal(ProjectWorkItemEffortUnit.Hours, metadata.ExpectedEffortUnit);
        Assert.Equal("", metadata.ExpectedCostCurrencyCode);
        Assert.Equal("", metadata.AssigneePartyDisplayName);
        Assert.Equal(0, metadata.DirectAssignmentRevision);
        Assert.Equal(ProjectTaskExecutionState.NotStarted, metadata.ExecutionState);
        Assert.Null(metadata.ActualStartedAtUtc);
        Assert.Null(metadata.ActualEndedAtUtc);
        Assert.Null(metadata.DueUtc);
        Assert.Equal(Instant, row.CreatedAtUtc);
        Assert.Equal(Instant, row.UpdatedAtUtc);
        Assert.Empty(context.ChangeTracker.Entries<ProjectNodeReferenceRecord>());
        Assert.Equal(row.MetadataJson, Assert.Single(result.Tasks).MetadataJson);
        using var invalid = Context();
        Invalid(() => ProjectTaskPlanTaskStaging.StageNewPlainTasks(invalid, current.Basis.Project.Expectation.Project,
            Normalize(Item()), current, default));
        Assert.Empty(invalid.ChangeTracker.Entries());
    }

    [Fact]
    public void Owner_generated_ids_preserve_input_ordinals_and_each_row_has_fresh_references() {
        using var context = Context();
        var current = Current();
        var result = Stage(context, current, ProjectTaskPlanProposal.Normalize(new(1, [Item(), Item() with { Title = "Second" }])));
        var rows = context.ChangeTracker.Entries<ProjectObjectRecord>().Select(entry => entry.Entity).ToArray();
        Assert.Equal(new[] { 0, 1 }, result.Tasks.Select(task => task.Ordinal));
        Assert.Equal(new[] { "Task", "Second" }, result.Tasks.Select(task => task.Fact.Title));
        Assert.Equal(2, result.Tasks.Select(task => task.Fact.RecordId).Distinct().Count());
        Assert.All(result.Tasks, task => Assert.Equal($"custom:{task.Fact.RecordId:N}", task.Fact.NodeKey));
        Assert.NotSame(rows[0].NodeReferences, rows[1].NodeReferences);
        Assert.NotSame(ProjectNodeReferenceCollection.Empty, rows[0].NodeReferences);
        rows[0].NodeReferences.Entries = [new("foreign", "foreign", 0)];
        Assert.Empty(rows[1].NodeReferences.Entries);
        Assert.Equal("Task", result.Tasks[0].Fact.Title);
        rows[0].Title = "Later";
        Assert.Equal("Task", result.Tasks[0].Fact.Title);
    }

    [Fact]
    public void Binding_preparation_refuses_noncanonical_metadata_references_and_external_storage() {
        Action<ProjectObjectRecord>[] changes = [
            row => row.Binding = new("foreign", "WorkItem", null, "", "", "", ""),
            row => row.Binding = new("", "", null, "file", "", "", ""),
            row => row.Binding = new("", "", Guid.NewGuid(), "", "", "", ""),
            row => row.NodeReferences.Entries = [new("", "", 0)],
            row => row.MetadataJson = row.MetadataJson.Insert(1, "\"unknown\":0,"),
            row => row.MetadataJson = row.MetadataJson.Insert(1, "\"workItem\":{},"),
            row => row.MetadataJson = row.MetadataJson.Insert(1, "\"assigneeParticipantNodeId\":\"legacy\","),
            row => row.MetadataJson = row.MetadataJson.Replace("\"description\":", "\"unsupported\":true,\"description\":", StringComparison.Ordinal),
            row => row.StartUtc = Instant,
            row => row.ObjectSubtype = "issue"
        ];
        foreach (var change in changes) {
            var row = PlainRow();
            change(row);
            Assert.ThrowsAny<InvalidOperationException>(() => ProjectNodeBindingStorage.PrepareNewPlainTaskBinding(row));
        }
    }

    [Fact]
    public void Binding_stage_requires_added_same_unchanged_prepared_row() {
        using var context = Context();
        var row = PlainRow();
        var plan = ProjectNodeBindingStorage.PrepareNewPlainTaskBinding(row);
        Assert.Throws<InvalidOperationException>(() => ProjectNodeBindingStorage.StageNewPlainTaskBinding(context, row, plan));
        context.Attach(row);
        Assert.Throws<InvalidOperationException>(() => ProjectNodeBindingStorage.StageNewPlainTaskBinding(context, row, plan));
        context.Entry(row).State = EntityState.Modified;
        Assert.Throws<InvalidOperationException>(() => ProjectNodeBindingStorage.StageNewPlainTaskBinding(context, row, plan));
        context.Entry(row).State = EntityState.Added;
        row.Title = "Changed";
        Assert.Throws<InvalidOperationException>(() => ProjectNodeBindingStorage.StageNewPlainTaskBinding(context, row, plan));
        Assert.Empty(context.ChangeTracker.Entries<ProjectNodeBindingRecord>());
        row.Title = "Task";
        ProjectNodeBindingStorage.StageNewPlainTaskBinding(context, row, plan);
        Assert.Single(context.ChangeTracker.Entries<ProjectNodeBindingRecord>());
    }

    [Fact]
    public void Duplicate_binding_and_pending_material_changes_refuse_without_altering_prior_tracker() {
        using var context = Context();
        var current = Current();
        var row = PlainRow(current.Basis.Project.Expectation.Project.ProjectId);
        var plan = ProjectNodeBindingStorage.PrepareNewPlainTaskBinding(row);
        context.Add(row);
        ProjectNodeBindingStorage.StageNewPlainTaskBinding(context, row, plan);
        var before = context.ChangeTracker.Entries().Select(entry => (entry.Entity, entry.State)).ToArray();
        Assert.Throws<InvalidOperationException>(() => ProjectNodeBindingStorage.StageNewPlainTaskBinding(context, row, plan));
        Assert.Throws<InvalidOperationException>(() => Stage(context, current, Normalize(Item())));
        Assert.Equal(before, context.ChangeTracker.Entries().Select(entry => (entry.Entity, entry.State)).ToArray());
        using var invalid = Context();
        var other = new ProjectWriteAdmission(Guid.NewGuid(), current.Basis.Project.Expectation.Project.ProjectId, Guid.NewGuid());
        Invalid(() => ProjectTaskPlanTaskStaging.StageNewPlainTasks(invalid, other, Normalize(Item()), current, Instant));
        Assert.Empty(invalid.ChangeTracker.Entries());
    }

    [Fact]
    public void Stored_whole_metadata_limit_includes_envelope_and_json_escaping_before_tracking() {
        var overhead = Encoding.UTF8.GetByteCount(PlainRow(description: "").MetadataJson);
        using var boundary = Context();
        var result = Stage(boundary, Current(), Normalize(Item() with {
            Description = new string('a', ProjectTaskPlanBasisLimits.MaximumFieldBytes - overhead)
        }));
        Assert.Equal(65_536, Encoding.UTF8.GetByteCount(result.Tasks[0].MetadataJson));
        foreach (var description in new[] { new string('a', 65_536), new string('\u0001', 20_000) }) {
            using var context = Context();
            Limited(() => Stage(context, Current(), Normalize(Item() with { Description = description })));
            Assert.Empty(context.ChangeTracker.Entries());
        }
    }

    [Fact]
    public void Combined_raw_candidate_and_encoded_basis_limits_refuse_the_complete_batch() {
        var excludedStoredTexts = Enumerable.Repeat(new string('a', 64_000), 32).ToArray();
        var rawCurrent = Current(candidateCount: 32, sourceBytes: excludedStoredTexts.Sum(ProjectTaskPlanBasisLimits.TextBytes));
        using var rawContext = Context();
        Limited(() => Stage(rawContext, rawCurrent, Normalize(Item() with { Notes = new string('a', 60_000) })));
        Assert.Empty(rawContext.ChangeTracker.Entries());
        using var countContext = Context();
        Limited(() => Stage(countContext, Current(candidateCount: 1_000), Normalize(Item())));
        Assert.Empty(countContext.ChangeTracker.Entries());
        var facts = Enumerable.Range(0, 16).Select(index => Fact(index, new string('a', 60_000))).ToArray();
        var encodedCurrent = Current(facts, sourceBytes: 1_000_000);
        Assert.True(ProjectTaskPlanBasisCodec.Encode(encodedCurrent.Basis).Length < 1_048_576);
        using var encodedContext = Context();
        var proposal = ProjectTaskPlanProposal.Normalize(new(1,
            [Item() with { Notes = new string('b', 60_000) }, Item() with { Notes = new string('c', 60_000) }]));
        Limited(() => Stage(encodedContext, encodedCurrent, proposal));
        Assert.Empty(encodedContext.ChangeTracker.Entries());
    }

    private static ProjectTaskPlanDraftItem Item() => new("Task", "Description", "Notes", 3);
    private static ProjectTaskPlanProposal Normalize(ProjectTaskPlanDraftItem item) => ProjectTaskPlanProposal.Normalize(new(1, [item]));
    private static WorkbenchDbContext Context() => new(new DbContextOptionsBuilder<WorkbenchDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static ProjectTaskPlanStagedBatch Stage(WorkbenchDbContext context, ProjectTaskPlanCurrentMaterial current,
        ProjectTaskPlanProposal proposal) => ProjectTaskPlanTaskStaging.StageNewPlainTasks(context,
            current.Basis.Project.Expectation.Project, proposal, current, Instant);

    private static ProjectTaskPlanCurrentMaterial Current(IReadOnlyList<ProjectTaskPlanTaskFact>? facts = null,
        int? candidateCount = null, long? sourceBytes = null) {
        var project = new ProjectDefinitionScalarSnapshot(new(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()),
            new("Project", "", "", ProjectStatus.Draft, "", null));
        var basis = new ProjectTaskPlanBasis(new(Guid.NewGuid()), project, Instant, facts ?? []);
        return new(basis, candidateCount ?? basis.Tasks.Count,
            sourceBytes ?? ProjectTaskPlanBasisLimits.ScalarBytes(project.Scalars));
    }

    private static ProjectTaskPlanTaskFact Fact(int index, string notes) => new(Guid.NewGuid(), $"task:{index}", null,
        "Task", "Task", notes, "Draft", 0, "progress", 28, null, null, null, "", null,
        new(ProjectTaskExecutionState.NotStarted, null, null));

    private static ProjectObjectRecord PlainRow(Guid? project = null, string description = "Description") {
        var id = Guid.NewGuid();
        var projectId = project ?? Guid.NewGuid();
        return new() {
            Id = id, ProjectId = projectId, NodeKey = $"custom:{id:N}",
            ParentNodeKey = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId),
            ObjectType = ProjectObjectType.WorkItem, ObjectSubtype = ProjectObjectSubtypePolicy.Task,
            Title = "Task", Subtitle = "Task", Notes = "Notes", Status = "Draft", ProgressMode = "progress", ProgressPercent = 28,
            NodeReferences = new(), CreatedAtUtc = Instant, UpdatedAtUtc = Instant,
            MetadataJson = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope {
                WorkItem = new() { WorkItemKind = ProjectWorkItemKind.Task, Description = description,
                    ExpectedEffortUnit = ProjectWorkItemEffortUnit.Hours, ExecutionState = ProjectTaskExecutionState.NotStarted }
            })
        };
    }

    private static void Invalid(Action action) => Assert.Equal(ProjectTaskPlanBasisRefusal.InvalidSource,
        Assert.Throws<ProjectTaskPlanBasisException>(action).Reason);
    private static void Limited(Action action) => Assert.Equal(ProjectTaskPlanBasisRefusal.LimitExceeded,
        Assert.Throws<ProjectTaskPlanBasisException>(action).Reason);
}
