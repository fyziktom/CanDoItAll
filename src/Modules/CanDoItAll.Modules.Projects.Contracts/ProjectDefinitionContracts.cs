using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects;

[JsonConverter(typeof(ProjectDefinitionExpectationJsonConverter))]
public sealed record ProjectDefinitionExpectation {
    public ProjectDefinitionExpectation(ProjectWriteAdmission project, Guid revision) {
        ArgumentNullException.ThrowIfNull(project);
        if (revision == Guid.Empty) {
            throw new ArgumentException("A definition expectation requires a nonempty revision.", nameof(revision));
        }
        Project = project;
        Revision = revision;
    }

    public ProjectWriteAdmission Project { get; }
    public Guid Revision { get; }
}

internal sealed class ProjectDefinitionExpectationJsonConverter : JsonConverter<ProjectDefinitionExpectation> {
    public ProjectDefinitionExpectationJsonConverter() {
    }

    public override ProjectDefinitionExpectation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        try {
            var value = JsonSerializer.Deserialize<SerializedExpectation>(ref reader, options)
                ?? throw new JsonException("A definition expectation must be an object.");
            var project = value.Project ?? throw new JsonException("A definition expectation requires a project admission.");
            return new(new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId), value.Revision);
        } catch (ArgumentException exception) {
            throw new JsonException("A definition expectation requires a valid project admission and nonempty revision.", exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, ProjectDefinitionExpectation value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, new SerializedExpectation(
            new(value.Project.DatabaseProfileId, value.Project.ProjectId, value.Project.LifetimeId), value.Revision), options);

    private sealed record SerializedExpectation(SerializedAdmission? Project, Guid Revision);
    private sealed record SerializedAdmission(Guid DatabaseProfileId, Guid ProjectId, Guid LifetimeId);
}

public sealed record ProjectDefinitionScalars(
    string Name,
    string Description,
    string Objective,
    ProjectStatus Status,
    string CurrentPhase,
    DateTime? TargetDateUtc);

public sealed record ProjectDefinitionScalarSnapshot {
    public ProjectDefinitionScalarSnapshot(ProjectDefinitionExpectation expectation, ProjectDefinitionScalars scalars) {
        ArgumentNullException.ThrowIfNull(expectation);
        ArgumentNullException.ThrowIfNull(scalars);
        Expectation = expectation;
        Scalars = scalars;
    }

    public ProjectDefinitionExpectation Expectation { get; }
    public ProjectDefinitionScalars Scalars { get; }
}
public sealed record ProjectDefinitionExternalIdentity(string Namespace, string Key);

public sealed record ProjectDefinitionPhaseInput(
    Guid? Id,
    string Name,
    string Goal,
    ProjectPhaseStatus Status,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc);

public sealed record ProjectDefinitionOptionInput(
    Guid? Id,
    ProjectOptionCategory Category,
    string OptionName,
    string Notes);

public sealed record ReplaceProjectDefinitionCommand {
    public ReplaceProjectDefinitionCommand(ProjectDefinitionExpectation expected, ProjectDefinitionScalars scalars,
        IReadOnlyList<ProjectDefinitionPhaseInput> phases, IReadOnlyList<ProjectDefinitionOptionInput> options,
        ProjectDefinitionExternalIdentity? assignExternalIdentity) {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(scalars);
        Expected = expected;
        Scalars = scalars;
        Phases = ProjectDefinitionCollections.Snapshot(phases, nameof(phases));
        Options = ProjectDefinitionCollections.Snapshot(options, nameof(options));
        AssignExternalIdentity = assignExternalIdentity;
    }

    public ProjectDefinitionExpectation Expected { get; }
    public ProjectDefinitionScalars Scalars { get; }
    public IReadOnlyList<ProjectDefinitionPhaseInput> Phases { get; }
    public IReadOnlyList<ProjectDefinitionOptionInput> Options { get; }
    public ProjectDefinitionExternalIdentity? AssignExternalIdentity { get; }
}

public sealed record UpdateProjectDefinitionScalarsCommand {
    public UpdateProjectDefinitionScalarsCommand(ProjectDefinitionExpectation expected, ProjectDefinitionScalars scalars) {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(scalars);
        Expected = expected;
        Scalars = scalars;
    }

    public ProjectDefinitionExpectation Expected { get; }
    public ProjectDefinitionScalars Scalars { get; }
}

public sealed record CreateProjectDefinitionCommand {
    public CreateProjectDefinitionCommand(ProjectDefinitionScalars scalars,
        IReadOnlyList<ProjectDefinitionPhaseInput> phases, IReadOnlyList<ProjectDefinitionOptionInput> options,
        ProjectDefinitionExternalIdentity? externalIdentity) {
        ArgumentNullException.ThrowIfNull(scalars);
        Scalars = scalars;
        Phases = ProjectDefinitionCollections.Snapshot(phases, nameof(phases));
        Options = ProjectDefinitionCollections.Snapshot(options, nameof(options));
        ExternalIdentity = externalIdentity;
    }

    public ProjectDefinitionScalars Scalars { get; }
    public IReadOnlyList<ProjectDefinitionPhaseInput> Phases { get; }
    public IReadOnlyList<ProjectDefinitionOptionInput> Options { get; }
    public ProjectDefinitionExternalIdentity? ExternalIdentity { get; }
}

public sealed record ProjectDefinitionPhase(
    Guid Id,
    string Name,
    string Goal,
    ProjectPhaseStatus Status,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc);

public sealed record ProjectDefinitionOption(
    Guid Id,
    ProjectOptionCategory Category,
    string OptionName,
    string Notes);

public sealed record ProjectDefinitionSnapshot {
    public ProjectDefinitionSnapshot(ProjectDefinitionExpectation expectation, ProjectDefinitionScalars scalars,
        ProjectDefinitionExternalIdentity? externalIdentity, IReadOnlyList<ProjectDefinitionPhase> phases,
        IReadOnlyList<ProjectDefinitionOption> options) {
        ArgumentNullException.ThrowIfNull(expectation);
        ArgumentNullException.ThrowIfNull(scalars);
        Expectation = expectation;
        Scalars = scalars;
        ExternalIdentity = externalIdentity;
        Phases = ProjectDefinitionCollections.Snapshot(phases, nameof(phases));
        Options = ProjectDefinitionCollections.Snapshot(options, nameof(options));
    }

    public ProjectDefinitionExpectation Expectation { get; }
    public ProjectDefinitionScalars Scalars { get; }
    public ProjectDefinitionExternalIdentity? ExternalIdentity { get; }
    public IReadOnlyList<ProjectDefinitionPhase> Phases { get; }
    public IReadOnlyList<ProjectDefinitionOption> Options { get; }
}

public sealed record ProjectDefinitionScalarsAcknowledgement(
    ProjectDefinitionExpectation Definition,
    ProjectDefinitionScalars Scalars,
    ProjectDefinitionExternalIdentity? ExternalIdentity);

internal static class ProjectDefinitionCollections {
    internal static IReadOnlyList<T> Snapshot<T>(IReadOnlyList<T> values, string parameterName) where T : class {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        var snapshot = values.ToArray();
        if (Array.Exists(snapshot, value => value is null)) {
            throw new ArgumentException("A definition collection cannot contain null rows.", parameterName);
        }
        return Array.AsReadOnly(snapshot);
    }
}
