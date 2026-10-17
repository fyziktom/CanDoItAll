using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectTaskPlanBasisTests {
    private static readonly DateTimeOffset Instant = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Captured_facts_detach_from_mutable_source_rows_and_lists() {
        var row = Row();
        var fact = ProjectTaskPlanMaterialProjection.Project(row)!;
        var facts = new List<ProjectTaskPlanTaskFact> { fact };
        var basis = Basis(facts);
        row.Title = "Later title";
        row.MetadataJson = "{}";
        facts.Clear();
        Assert.Equal("  Exact title  ", Assert.Single(basis.Tasks).Title);
        Assert.Equal("Description", basis.Tasks[0].Description);
        Assert.False(basis.Tasks is ProjectTaskPlanTaskFact[]);
    }

    [Fact]
    public void Complete_empty_scope_is_valid_but_default_or_duplicate_occurrences_are_not() {
        Assert.Empty(Basis([]).Tasks);
        var fact = Fact();
        Invalid(() => Basis([fact, fact with { RecordId = Guid.NewGuid() }]));
        Invalid(() => Basis([fact, fact with { NodeKey = "other" }]));
        Invalid(() => Basis([fact with { RecordId = Guid.Empty }]));
        Invalid(() => Basis([fact with { NodeKey = " " }]));
        Assert.Throws<ArgumentException>(() => new DatasetNamespaceId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new ProjectWriteAdmission(Guid.Empty, Guid.NewGuid(), Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => new ProjectDefinitionExpectation(Project().Expectation.Project, Guid.Empty));
    }

    [Fact]
    public void Every_selected_task_field_changes_material_equality_and_digest() {
        var fact = Fact();
        ProjectTaskPlanTaskFact[] changes = [
            fact with { RecordId = Guid.NewGuid() }, fact with { NodeKey = "new" }, fact with { ParentNodeKey = "parent" },
            fact with { Title = "new" }, fact with { Subtitle = "new" }, fact with { Notes = "new" },
            fact with { Status = "new" }, fact with { Priority = 1 }, fact with { ProgressMode = "manual" },
            fact with { ProgressPercent = 1 }, fact with { StartUtc = Instant }, fact with { EndUtc = Instant },
            fact with { DurationSeconds = 1 }, fact with { Description = "new" }, fact with { DueUtc = Instant },
            fact with { Execution = new(ProjectTaskExecutionState.Unknown, null, null) },
            fact with { Execution = new(ProjectTaskExecutionState.Started, Instant, null) },
            fact with { Execution = new(ProjectTaskExecutionState.Cancelled, null, Instant) }
        ];
        var basis = Basis([fact]);
        foreach (var changed in changes) {
            var candidate = new ProjectTaskPlanBasis(basis.Namespace, basis.Project, Instant, [changed]);
            Assert.False(ProjectTaskPlanBasisCodec.MaterialEquals(basis, candidate));
            Assert.NotEqual(ProjectTaskPlanBasisCodec.MaterialDigest(basis), ProjectTaskPlanBasisCodec.MaterialDigest(candidate));
        }
        var completed = fact with { Execution = new(ProjectTaskExecutionState.Completed, Instant, Instant.AddHours(1)) };
        var completedBasis = new ProjectTaskPlanBasis(basis.Namespace, basis.Project, Instant, [completed]);
        foreach (var execution in new[] { completed.Execution with { ActualStartedAtUtc = Instant.AddMinutes(-1) },
                completed.Execution with { ActualEndedAtUtc = Instant.AddHours(2) } }) {
            var candidate = new ProjectTaskPlanBasis(basis.Namespace, basis.Project, Instant, [completed with { Execution = execution }]);
            Assert.False(ProjectTaskPlanBasisCodec.MaterialEquals(completedBasis, candidate));
            Assert.NotEqual(ProjectTaskPlanBasisCodec.MaterialDigest(completedBasis), ProjectTaskPlanBasisCodec.MaterialDigest(candidate));
        }
    }

    [Fact]
    public void Excluded_metadata_is_neither_material_nor_copied_into_the_basis() {
        var row = Row();
        var original = ProjectTaskPlanMaterialProjection.Project(row);
        row.MetadataJson = """{"workItem":{"workItemKind":"task","description":"Description","executionState":"notStarted","expectedEffortHours":99,"assigneePartyName":"private","directAssignmentRevision":42,"expectedCostAmount":100},"extension":{"private":"not copied"}}""";
        Assert.Equal(original, ProjectTaskPlanMaterialProjection.Project(row));
        var encoded = Encoding.UTF8.GetString(ProjectTaskPlanBasisCodec.Encode(Basis([original!])));
        Assert.DoesNotContain("private", encoded, StringComparison.Ordinal);
        Assert.DoesNotContain("expectedCost", encoded, StringComparison.Ordinal);
        Assert.DoesNotContain("assignment", encoded, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Occurrence_replacement_differs_from_restoring_identical_current_values() {
        var original = Basis([Fact()]);
        var changed = original.Tasks[0] with { Title = "Intermediate" };
        var restored = new ProjectTaskPlanBasis(original.Namespace, original.Project, Instant.AddMinutes(1),
            [changed with { Title = original.Tasks[0].Title }]);
        Assert.True(ProjectTaskPlanBasisCodec.MaterialEquals(original, restored));
        var replaced = new ProjectTaskPlanBasis(original.Namespace, original.Project, Instant,
            [original.Tasks[0] with { RecordId = Guid.NewGuid() }]);
        Assert.False(ProjectTaskPlanBasisCodec.MaterialEquals(original, replaced));
    }

    [Fact]
    public void Canonical_selection_uses_existing_case_whitespace_and_system_semantics() {
        var row = Row();
        foreach (var subtype in new[] { "task", "TASK", " \tTaSk\u2003" }) {
            row.ObjectSubtype = subtype;
            Assert.NotNull(ProjectTaskPlanMaterialProjection.Project(row));
        }
        row.IsSystemManaged = true;
        Assert.Null(ProjectTaskPlanMaterialProjection.Project(row));
        row.IsSystemManaged = false;
        row.ObjectSubtype = "issue";
        Assert.Null(ProjectTaskPlanMaterialProjection.Project(row));
    }

    [Fact]
    public void Selected_metadata_requires_material_members_and_accepts_omitted_nullable_dates() {
        var row = Row();
        Assert.Null(ProjectTaskPlanMaterialProjection.Project(row)!.DueUtc);
        string[] invalid = ["{}", "null", "{", "{\"workItem\":null}",
            """{"workItem":{"workItemKind":"task","executionState":"notStarted"}}""",
            """{"workItem":{"workItemKind":"task","description":null,"executionState":"notStarted"}}""",
            """{"workItem":{"workItemKind":"task","description":"","Description":"","executionState":"notStarted"}}""",
            """{"workItem":{},"WorkItem":{}}"""];
        foreach (var json in invalid) {
            row.MetadataJson = json;
            Invalid(() => ProjectTaskPlanMaterialProjection.Project(row));
        }
    }

    [Fact]
    public void Execution_decoding_accepts_defined_legacy_numbers_and_rejects_invalid_state_time_pairs() {
        var row = Row();
        row.MetadataJson = """{"workItem":{"workItemKind":0,"description":"Description","executionState":1}}""";
        Assert.Equal(ProjectTaskExecutionState.NotStarted, ProjectTaskPlanMaterialProjection.Project(row)!.Execution.State);
        foreach (var state in new[] { "99", "\"1\"", "\"started\"", "\"completed\"", "\"cancelled\"" }) {
            row.MetadataJson = "{\"workItem\":{\"workItemKind\":0,\"description\":\"\",\"executionState\":" + state + "}}";
            Invalid(() => ProjectTaskPlanMaterialProjection.Project(row));
        }
        row.MetadataJson = """{"workItem":{"workItemKind":"task","description":"","executionState":"completed","actualStartedAtUtc":"2026-10-16T10:00:00+02:00","actualEndedAtUtc":"2026-10-16T09:00:00Z"}}""";
        Assert.Equal(TimeSpan.Zero, ProjectTaskPlanMaterialProjection.Project(row)!.Execution.ActualStartedAtUtc!.Value.Offset);
        row.MetadataJson = row.MetadataJson.Replace("09:00:00Z", "07:00:00Z", StringComparison.Ordinal);
        Invalid(() => ProjectTaskPlanMaterialProjection.Project(row));
    }

    [Fact]
    public void Codec_round_trip_sorts_facts_and_excludes_observation_time_from_material_equality() {
        var first = Fact();
        var second = first with { RecordId = Guid.NewGuid(), NodeKey = "a" };
        var basis = Basis([first, second]);
        var decoded = ProjectTaskPlanBasisCodec.Decode(ProjectTaskPlanBasisCodec.Encode(basis));
        Assert.True(ProjectTaskPlanBasisCodec.MaterialEquals(basis, decoded));
        Assert.Equal(basis.CapturedAtUtc, decoded.CapturedAtUtc);
        Assert.Equal(ProjectTaskPlanBasisCodec.Encode(basis), ProjectTaskPlanBasisCodec.Encode(decoded));
        Assert.Equal("a", decoded.Tasks[0].NodeKey);
        var later = new ProjectTaskPlanBasis(basis.Namespace, basis.Project, Instant.AddHours(1), [second, first]);
        Assert.Equal(ProjectTaskPlanBasisCodec.MaterialDigest(basis), ProjectTaskPlanBasisCodec.MaterialDigest(later));
    }

    [Fact]
    public void Codec_rejects_unknown_missing_duplicate_null_and_invalid_bound_values() {
        var json = Encoding.UTF8.GetString(ProjectTaskPlanBasisCodec.Encode(Basis([Fact()])));
        foreach (var property in new[] { "version", "descriptor", "namespace", "databaseProfileId", "projectId", "lifetimeId",
                "definitionRevision", "scalars", "capturedAtUtc", "tasks" }) {
            var value = JsonNode.Parse(json)!.AsObject();
            value.Remove(property);
            Invalid(() => ProjectTaskPlanBasisCodec.Decode(Encoding.UTF8.GetBytes(value.ToJsonString())));
        }
        foreach (var invalid in new[] {
                json.Replace("\"version\":1", "\"version\":2", StringComparison.Ordinal),
                json.Replace("\"descriptor\":1", "\"descriptor\":2", StringComparison.Ordinal),
                json.Insert(1, "\"unknown\":0,"), json.Insert(1, "\"version\":1,"),
                json.Replace("\"priority\":0", "\"priority\":0,\"priority\":1", StringComparison.Ordinal) }) {
            Invalid(() => ProjectTaskPlanBasisCodec.Decode(Encoding.UTF8.GetBytes(invalid)));
        }
        var absentTime = JsonNode.Parse(json)!.AsObject();
        absentTime["capturedAtUtc"] = "0001-01-01T00:00:00+00:00";
        Invalid(() => ProjectTaskPlanBasisCodec.Decode(Encoding.UTF8.GetBytes(absentTime.ToJsonString())));
        var badIdentity = JsonNode.Parse(json)!.AsObject();
        badIdentity["namespace"] = Guid.Empty.ToString();
        Invalid(() => ProjectTaskPlanBasisCodec.Decode(Encoding.UTF8.GetBytes(badIdentity.ToJsonString())));
        var badScalar = JsonNode.Parse(json)!.AsObject();
        badScalar["scalars"]!["status"] = 999;
        Invalid(() => ProjectTaskPlanBasisCodec.Decode(Encoding.UTF8.GetBytes(badScalar.ToJsonString())));
        var unknownNested = JsonNode.Parse(json)!.AsObject();
        unknownNested["tasks"]![0]!["unexpected"] = true;
        Invalid(() => ProjectTaskPlanBasisCodec.Decode(Encoding.UTF8.GetBytes(unknownNested.ToJsonString())));
        var malformed = JsonNode.Parse(json)!.AsObject();
        malformed["tasks"] = null;
        Invalid(() => ProjectTaskPlanBasisCodec.Decode(Encoding.UTF8.GetBytes(malformed.ToJsonString())));
    }

    [Fact]
    public void Candidate_field_aggregate_and_encoded_byte_limits_are_enforced() {
        Assert.Equal(65_536, ProjectTaskPlanBasisLimits.TextBytes(new string('\u00e9', 32_768)));
        Limited(() => ProjectTaskPlanBasisLimits.TextBytes(new string('\u00e9', 32_769)));
        var tasks = Enumerable.Range(0, 1_000).Select(index => Fact() with { NodeKey = index.ToString() }).ToArray();
        Assert.Equal(1_000, Basis(tasks).Tasks.Count);
        Limited(() => Basis(tasks.Append(Fact() with { NodeKey = "overflow" }).ToArray()));
        ProjectTaskPlanBasisLimits.RequireBytes(2_097_152, ProjectTaskPlanBasisLimits.MaximumSourceBytes);
        Limited(() => ProjectTaskPlanBasisLimits.RequireBytes(2_097_153, ProjectTaskPlanBasisLimits.MaximumSourceBytes));
        Limited(() => Basis(Enumerable.Range(0, 33).Select(index => Fact() with {
            NodeKey = index.ToString(), Notes = new string('a', 65_536)
        }).ToArray()));
        Limited(() => ProjectTaskPlanBasisCodec.Decode(new byte[1_048_577]));
        var large = Basis(Enumerable.Range(0, 24).Select(index => Fact() with {
            NodeKey = index.ToString(), Notes = new string('a', 50_000)
        }).ToArray());
        Limited(() => ProjectTaskPlanBasisCodec.Encode(large));
        var boundaryTasks = Enumerable.Range(0, 20).Select(index => Fact() with { NodeKey = index.ToString() }).ToArray();
        var boundary = Basis(boundaryTasks);
        for (var index = 0; index < boundaryTasks.Length; index++) {
            var remaining = ProjectTaskPlanBasisLimits.MaximumEncodedBytes - ProjectTaskPlanBasisCodec.Encode(boundary).Length;
            boundaryTasks[index] = boundaryTasks[index] with { Notes = new string('a', Math.Min(remaining, 65_536)) };
            boundary = new(boundary.Namespace, boundary.Project, Instant, boundaryTasks);
        }
        Assert.Equal(1_048_576, ProjectTaskPlanBasisCodec.Encode(boundary).Length);
        boundaryTasks[^1] = boundaryTasks[^1] with { Notes = boundaryTasks[^1].Notes + "a" };
        var oneOver = new ProjectTaskPlanBasis(boundary.Namespace, boundary.Project, Instant, boundaryTasks);
        Limited(() => ProjectTaskPlanBasisCodec.Encode(oneOver));
    }

    [Fact]
    public void Namespace_lifetime_revision_and_each_project_scalar_are_material() {
        var original = Basis([]);
        var expected = original.Project.Expectation;
        var scalars = original.Project.Scalars;
        ProjectDefinitionScalars[] changes = [scalars with { Name = "new" }, scalars with { Description = "new" },
            scalars with { Objective = "new" }, scalars with { CurrentPhase = "new" },
            scalars with { Status = Enum.GetValues<ProjectStatus>().First(status => status != scalars.Status) },
            scalars with { TargetDateUtc = Instant.UtcDateTime }];
        foreach (var value in changes) {
            Assert.False(ProjectTaskPlanBasisCodec.MaterialEquals(original,
                new(original.Namespace, new(expected, value), Instant, [])));
        }
        Assert.False(ProjectTaskPlanBasisCodec.MaterialEquals(original, new(new(Guid.NewGuid()), original.Project, Instant, [])));
        Assert.False(ProjectTaskPlanBasisCodec.MaterialEquals(original,
            new(original.Namespace, new(new(expected.Project, Guid.NewGuid()), scalars), Instant, [])));
        Assert.False(ProjectTaskPlanBasisCodec.MaterialEquals(original,
            new(original.Namespace, new(new(new(expected.Project.DatabaseProfileId, expected.Project.ProjectId, Guid.NewGuid()),
                expected.Revision), scalars), Instant, [])));
    }

    private static ProjectTaskPlanSourceRow Row() => new() {
        Id = Guid.NewGuid(), NodeKey = "task:z", ObjectType = ProjectObjectType.WorkItem, ObjectSubtype = "task",
        Title = "  Exact title  ", Subtitle = "", Notes = "", Status = "Draft", Priority = 0, ProgressMode = "",
        ProgressPercent = 0, MetadataJson = """{"workItem":{"workItemKind":"task","description":"Description","executionState":"notStarted"}}"""
    };

    private static ProjectTaskPlanTaskFact Fact() => ProjectTaskPlanMaterialProjection.Project(Row())!;

    private static ProjectDefinitionScalarSnapshot Project() => new(
        new(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()), new("Project", "", "", ProjectStatus.Draft, "", null));

    private static ProjectTaskPlanBasis Basis(IReadOnlyList<ProjectTaskPlanTaskFact> tasks) => new(new(Guid.NewGuid()), Project(), Instant, tasks);

    private static void Invalid(Action action)
        => Assert.Equal(ProjectTaskPlanBasisRefusal.InvalidSource, Assert.Throws<ProjectTaskPlanBasisException>(action).Reason);

    private static void Limited(Action action)
        => Assert.Equal(ProjectTaskPlanBasisRefusal.LimitExceeded, Assert.Throws<ProjectTaskPlanBasisException>(action).Reason);
}
