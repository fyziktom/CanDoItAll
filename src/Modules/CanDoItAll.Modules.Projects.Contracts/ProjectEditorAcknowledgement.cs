using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects;

public sealed record ProjectEditorAcknowledgement(
    ProjectWriteAdmission Project,
    string Name,
    string Description,
    string Objective,
    ProjectStatus Status,
    string CurrentPhase,
    DateTime? TargetDateUtc,
    IReadOnlyList<ProjectPhaseAcknowledgement> Phases,
    IReadOnlyList<ProjectOptionAcknowledgement> Options);

public sealed record ProjectPhaseAcknowledgement(
    int SubmittedIndex, Guid Id, string Name, string Goal, ProjectPhaseStatus Status,
    DateTime? StartDateUtc, DateTime? EndDateUtc);

public sealed record ProjectOptionAcknowledgement(
    int SubmittedIndex, Guid Id, ProjectOptionCategory Category, string OptionName, string Notes);

public interface IAdmittedProjectWorkbenchSeedService {
    Task SeedProjectObjectsAsync(ProjectWriteAdmission project, IReadOnlyCollection<ProjectObjectSeedDraft> seeds,
        CancellationToken cancellationToken = default);
}
