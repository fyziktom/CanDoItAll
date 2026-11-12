using System.Text;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal enum ProjectTaskPlanBasisDescriptor { CanonicalManualTaskPlanV1 = 1 }
internal enum ProjectTaskPlanBasisRefusal { SourceMissing, LifetimeChanged, DatasetChanged, InvalidSource, LimitExceeded }

internal abstract record ProjectTaskPlanBasisReadResult {
    private ProjectTaskPlanBasisReadResult() {
    }

    internal sealed record Captured(ProjectTaskPlanBasis Basis) : ProjectTaskPlanBasisReadResult;
    internal sealed record Refused(ProjectTaskPlanBasisRefusal Reason) : ProjectTaskPlanBasisReadResult;
}

internal sealed class ProjectTaskPlanBasisException(ProjectTaskPlanBasisRefusal reason)
    : InvalidOperationException("The task planning source is invalid or exceeds its bounded scope.") {
    internal ProjectTaskPlanBasisRefusal Reason { get; } = reason;
}

internal static class ProjectTaskPlanBasisLimits {
    internal const int Version = 1;
    internal const int MaximumCandidates = 1_000;
    internal const int MaximumFieldBytes = 65_536;
    internal const int MaximumSourceBytes = 2_097_152;
    internal const int MaximumEncodedBytes = 1_048_576;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    internal static int TextBytes(string value) {
        if (value is null) {
            throw new ProjectTaskPlanBasisException(ProjectTaskPlanBasisRefusal.InvalidSource);
        }
        if (value.Length > MaximumFieldBytes) {
            throw new ProjectTaskPlanBasisException(ProjectTaskPlanBasisRefusal.LimitExceeded);
        }
        int bytes;
        try {
            bytes = Utf8.GetByteCount(value);
        } catch (EncoderFallbackException) {
            throw new ProjectTaskPlanBasisException(ProjectTaskPlanBasisRefusal.InvalidSource);
        }
        RequireBytes(bytes, MaximumFieldBytes);
        return bytes;
    }

    internal static void RequireBytes(long bytes, int maximum) {
        if (bytes < 0 || bytes > maximum) {
            throw new ProjectTaskPlanBasisException(bytes < 0
                ? ProjectTaskPlanBasisRefusal.InvalidSource : ProjectTaskPlanBasisRefusal.LimitExceeded);
        }
    }

    internal static void RequireUtc(DateTimeOffset? instant) {
        if (instant is { Offset: var offset } && offset != TimeSpan.Zero) {
            throw new ProjectTaskPlanBasisException(ProjectTaskPlanBasisRefusal.InvalidSource);
        }
    }

    internal static int ScalarBytes(ProjectDefinitionScalars scalars) {
        if (scalars is null || !Enum.IsDefined(scalars.Status) || scalars.TargetDateUtc is { Kind: not DateTimeKind.Utc }) {
            throw new ProjectTaskPlanBasisException(ProjectTaskPlanBasisRefusal.InvalidSource);
        }
        return TextBytes(scalars.Name) + TextBytes(scalars.Description) + TextBytes(scalars.Objective) + TextBytes(scalars.CurrentPhase);
    }

    internal static long TaskFactBytes(IReadOnlyList<ProjectTaskPlanTaskFact> tasks) {
        ArgumentNullException.ThrowIfNull(tasks);
        RequireBytes(tasks.Count, MaximumCandidates);
        long bytes = 0;
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var occurrences = new HashSet<Guid>();
        foreach (var task in tasks) {
            if (task is null || task.RecordId == Guid.Empty || string.IsNullOrWhiteSpace(task.NodeKey) ||
                    !keys.Add(task.NodeKey) || !occurrences.Add(task.RecordId) || task.Execution is null) {
                throw new ProjectTaskPlanBasisException(ProjectTaskPlanBasisRefusal.InvalidSource);
            }
            foreach (var value in new[] { task.NodeKey, task.Title, task.Subtitle, task.Notes, task.Status,
                    task.ProgressMode, task.Description }) {
                bytes += TextBytes(value);
            }
            if (task.ParentNodeKey is not null) {
                bytes += TextBytes(task.ParentNodeKey);
            }
            foreach (var instant in new[] { task.StartUtc, task.EndUtc, task.DueUtc,
                    task.Execution.ActualStartedAtUtc, task.Execution.ActualEndedAtUtc }) {
                RequireUtc(instant);
            }
            try {
                ProjectTaskExecutionStatePolicy.Validate(task.Execution.State,
                    task.Execution.ActualStartedAtUtc, task.Execution.ActualEndedAtUtc);
            } catch (InvalidOperationException) {
                throw new ProjectTaskPlanBasisException(ProjectTaskPlanBasisRefusal.InvalidSource);
            }
        }
        RequireBytes(bytes, MaximumSourceBytes);
        return bytes;
    }
}

internal sealed record ProjectTaskPlanTaskFact(
    Guid RecordId, string NodeKey, string? ParentNodeKey, string Title, string Subtitle, string Notes,
    string Status, int Priority, string ProgressMode, int ProgressPercent, DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc, int? DurationSeconds, string Description, DateTimeOffset? DueUtc,
    ProjectTaskExecutionSnapshot Execution);

internal sealed class ProjectTaskPlanBasis {
    internal ProjectTaskPlanBasis(DatasetNamespaceId datasetNamespace, ProjectDefinitionScalarSnapshot project,
        DateTimeOffset capturedAtUtc, IReadOnlyList<ProjectTaskPlanTaskFact> tasks) {
        ArgumentNullException.ThrowIfNull(datasetNamespace);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(tasks);
        if (capturedAtUtc == default) {
            throw new ProjectTaskPlanBasisException(ProjectTaskPlanBasisRefusal.InvalidSource);
        }
        ProjectTaskPlanBasisLimits.RequireUtc(capturedAtUtc);
        ProjectTaskPlanBasisLimits.RequireBytes(ProjectTaskPlanBasisLimits.ScalarBytes(project.Scalars) +
            ProjectTaskPlanBasisLimits.TaskFactBytes(tasks), ProjectTaskPlanBasisLimits.MaximumSourceBytes);
        Namespace = datasetNamespace;
        Project = project;
        CapturedAtUtc = capturedAtUtc;
        Tasks = Array.AsReadOnly(tasks.OrderBy(task => task.NodeKey, StringComparer.Ordinal).ToArray());
    }

    internal ProjectTaskPlanBasisDescriptor Descriptor => ProjectTaskPlanBasisDescriptor.CanonicalManualTaskPlanV1;
    internal int Version => ProjectTaskPlanBasisLimits.Version;
    internal DatasetNamespaceId Namespace { get; }
    internal ProjectDefinitionScalarSnapshot Project { get; }
    internal DateTimeOffset CapturedAtUtc { get; }
    internal IReadOnlyList<ProjectTaskPlanTaskFact> Tasks { get; }
}
