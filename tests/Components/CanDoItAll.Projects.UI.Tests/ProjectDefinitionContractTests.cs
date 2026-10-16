using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Components.ProjectsUi;

public sealed class ProjectDefinitionContractTests {
    [Fact]
    public void Definition_expectation_rejects_missing_admission_and_empty_revision() {
        var admission = new ProjectWriteAdmission(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        Assert.Throws<ArgumentNullException>(() => new ProjectDefinitionExpectation(null!, Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => new ProjectDefinitionExpectation(admission, Guid.Empty));
    }

    [Fact]
    public void Editor_json_round_trip_preserves_the_original_definition_expectation_and_rejects_invalid_wire_values() {
        var expected = new ProjectDefinitionExpectation(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid());
        var editor = new ProjectEditorModel {
            Id = expected.Project.ProjectId,
            ExpectedLifetimeId = expected.Project.LifetimeId,
            ExpectedProjectAdmission = expected.Project,
            ExpectedDefinition = expected,
            Name = "Original definition"
        };
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var json = JsonSerializer.Serialize(editor, options);
        var restored = Assert.IsType<ProjectEditorModel>(JsonSerializer.Deserialize<ProjectEditorModel>(json, options));

        Assert.Equal(expected, restored.ExpectedDefinition);
        Assert.Equal(expected.Project.ProjectId, restored.Id);
        Assert.Equal(expected.Project.LifetimeId, restored.ExpectedLifetimeId);
        Assert.Null(restored.ExpectedProjectAdmission);
        var emptyRevision = JsonSerializer.Serialize(new {
            expectedDefinition = new { project = expected.Project, revision = Guid.Empty }
        }, options);
        var invalidAdmission = JsonSerializer.Serialize(new {
            expectedDefinition = new {
                project = new { databaseProfileId = Guid.Empty, projectId = expected.Project.ProjectId, lifetimeId = expected.Project.LifetimeId },
                revision = expected.Revision
            }
        }, options);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProjectEditorModel>(emptyRevision, options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProjectEditorModel>(invalidAdmission, options));
    }

    [Fact]
    public void Definition_commands_require_explicit_collections_and_freeze_submitted_membership() {
        var expected = new ProjectDefinitionExpectation(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid());
        var scalars = new ProjectDefinitionScalars("Project", "Description", "Objective", ProjectStatus.Draft, "Phase", null);
        var phase = new ProjectDefinitionPhaseInput(null, "Phase", "Goal", ProjectPhaseStatus.Planned, null, null);
        var option = new ProjectDefinitionOptionInput(null, ProjectOptionCategory.Language, "C#", "Notes");
        var phases = new List<ProjectDefinitionPhaseInput> { phase };
        var options = new List<ProjectDefinitionOptionInput> { option };
        var replace = new ReplaceProjectDefinitionCommand(expected, scalars, phases, options, null);
        var create = new CreateProjectDefinitionCommand(scalars, phases, options, null);
        var storedPhases = new List<ProjectDefinitionPhase> { new(Guid.NewGuid(), phase.Name, phase.Goal, phase.Status, null, null) };
        var storedOptions = new List<ProjectDefinitionOption> { new(Guid.NewGuid(), option.Category, option.OptionName, option.Notes) };
        var snapshot = new ProjectDefinitionSnapshot(expected, scalars, null, storedPhases, storedOptions);

        phases.Clear();
        options.Clear();
        storedPhases.Clear();
        storedOptions.Clear();

        Assert.Same(phase, Assert.Single(replace.Phases));
        Assert.Same(option, Assert.Single(replace.Options));
        Assert.Same(phase, Assert.Single(create.Phases));
        Assert.Same(option, Assert.Single(create.Options));
        Assert.Single(snapshot.Phases);
        Assert.Single(snapshot.Options);
        Assert.Throws<NotSupportedException>(() => Assert.IsAssignableFrom<IList<ProjectDefinitionPhaseInput>>(replace.Phases).Clear());
        Assert.Throws<ArgumentNullException>(() => new ReplaceProjectDefinitionCommand(expected, scalars, null!, [], null));
        Assert.Throws<ArgumentNullException>(() => new CreateProjectDefinitionCommand(scalars, [], null!, null));
        Assert.Throws<ArgumentException>(() => new ReplaceProjectDefinitionCommand(expected, scalars, [null!], [], null));
        Assert.Throws<ArgumentException>(() => new CreateProjectDefinitionCommand(scalars, [], [null!], null));
    }
}
