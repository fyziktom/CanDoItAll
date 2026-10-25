using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectDefinitionRulesTests {
    [Fact]
    public void Scalars_are_normalized_without_mutating_the_submitted_values() {
        var date = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Unspecified);
        var submitted = new ProjectDefinitionScalars("  Project  ", "  Description  ", "  Objective  ",
            ProjectStatus.Active, "  Build  ", date);

        var normalized = ProjectDefinitionRules.Normalize(submitted);

        Assert.True(normalized.IsSuccess);
        Assert.Equal(new("Project", "Description", "Objective", ProjectStatus.Active, "Build",
            DateTime.SpecifyKind(date, DateTimeKind.Utc)), normalized.Value);
        Assert.Equal("  Project  ", submitted.Name);
        Assert.Equal(DateTimeKind.Unspecified, submitted.TargetDateUtc!.Value.Kind);
    }

    [Fact]
    public void Child_normalization_preserves_submission_order_and_persisted_identity_in_an_owned_snapshot() {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var phases = new List<ProjectDefinitionPhaseInput> {
            new(secondId, "  Second  ", "  Second goal  ", ProjectPhaseStatus.Active, null, null),
            new(firstId, "  First  ", "  First goal  ", ProjectPhaseStatus.Planned, null, null)
        };
        var normalized = ProjectDefinitionRules.Normalize(phases, [], creating: false);
        phases.Clear();

        Assert.True(normalized.IsSuccess);
        Assert.Equal(new Guid?[] { secondId, firstId }, normalized.Value!.Phases.Select(phase => phase.Id));
        Assert.Equal(new[] { "Second", "First" }, normalized.Value.Phases.Select(phase => phase.Name));
        Assert.Equal("Second goal", normalized.Value.Phases[0].Goal);
    }

    [Theory]
    [InlineData(LimitedField.ProjectName, 200)]
    [InlineData(LimitedField.CurrentPhase, 120)]
    [InlineData(LimitedField.PhaseName, 160)]
    [InlineData(LimitedField.OptionName, 200)]
    public void Existing_schema_field_limits_accept_the_boundary_and_reject_one_over(LimitedField field, int maximum) {
        var accepted = ValidateField(field, new string('x', maximum));
        var rejected = ValidateField(field, new string('x', maximum + 1));

        Assert.True(accepted.IsSuccess);
        Assert.Contains(rejected.Errors, error => error.Code == ProjectErrorCodes.DefinitionLimitExceeded);
    }

    [Theory]
    [InlineData(EnumField.ProjectStatus)]
    [InlineData(EnumField.PhaseStatus)]
    [InlineData(EnumField.OptionCategory)]
    public void Undefined_enums_reject_before_persistence(EnumField field) {
        var result = field switch {
            EnumField.ProjectStatus => (CanDoItAll.SharedKernel.Result)ProjectDefinitionRules.Normalize(
                Scalars() with { Status = (ProjectStatus)int.MaxValue }),
            EnumField.PhaseStatus => ProjectDefinitionRules.Normalize(
                [new(null, "Phase", "Goal", (ProjectPhaseStatus)int.MaxValue, null, null)], [], creating: true),
            EnumField.OptionCategory => ProjectDefinitionRules.Normalize([], [new(null, (ProjectOptionCategory)int.MaxValue, "Option", "Notes")], creating: true),
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Blank_persisted_option_is_absent_after_normalization_but_its_identity_remains_available_for_validation() {
        var id = Guid.NewGuid();
        var normalized = ProjectDefinitionRules.Normalize([], [new(id, ProjectOptionCategory.Language, " \t ", " \n ")], creating: false);

        Assert.True(normalized.IsSuccess);
        var option = Assert.Single(normalized.Value!.Options);
        Assert.Equal(id, option.Id);
        Assert.True(ProjectDefinitionRules.IsAbsent(option));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_rejects_persisted_child_ids(bool option) {
        var result = ProjectDefinitionRules.Normalize(
            option ? [] : [new(Guid.NewGuid(), "Phase", "", ProjectPhaseStatus.Planned, null, null)],
            option ? [new(Guid.NewGuid(), ProjectOptionCategory.Language, "Option", "")] : [], creating: true);

        Assert.Contains(result.Errors, error => error.Code == ProjectErrorCodes.DefinitionChildInvalid);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Replacement_rejects_empty_and_duplicate_child_ids_including_blank_options(bool option) {
        foreach (var id in new[] { Guid.Empty, Guid.NewGuid() }) {
            var result = ProjectDefinitionRules.Normalize(
                option ? [] : [new(id, "Phase", "", ProjectPhaseStatus.Planned, null, null), new(id, "Duplicate", "", ProjectPhaseStatus.Planned, null, null)],
                option ? [new(id, ProjectOptionCategory.Language, "Option", ""), new(id, ProjectOptionCategory.Language, "", "")] : [], creating: false);

            Assert.Contains(result.Errors, error => error.Code == ProjectErrorCodes.DefinitionChildInvalid);
        }
    }

    private static CanDoItAll.SharedKernel.Result ValidateField(LimitedField field, string value) => field switch {
        LimitedField.ProjectName => ProjectDefinitionRules.Normalize(Scalars() with { Name = value }),
        LimitedField.CurrentPhase => ProjectDefinitionRules.Normalize(Scalars() with { CurrentPhase = value }),
        LimitedField.PhaseName => ProjectDefinitionRules.Normalize([new(null, value, "", ProjectPhaseStatus.Planned, null, null)], [], creating: true),
        LimitedField.OptionName => ProjectDefinitionRules.Normalize([], [new(null, ProjectOptionCategory.Language, value, "")], creating: true),
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };

    private static ProjectDefinitionScalars Scalars() => new("Project", "", "", ProjectStatus.Draft, "", null);

    public enum LimitedField { ProjectName, CurrentPhase, PhaseName, OptionName }
    public enum EnumField { ProjectStatus, PhaseStatus, OptionCategory }
}
