using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectCreationReceipt {
    internal ProjectCreationReceipt(ProjectWriteAdmission project, ProjectCreationReservation? reservation, string fingerprint, ProjectMutationAuthorization? authorization = null) {
        Project = project;
        Reservation = reservation;
        Fingerprint = fingerprint;
        Authorization = authorization;
    }

    public ProjectWriteAdmission Project { get; }
    public ProjectCreationReservation? Reservation { get; }
    public string Fingerprint { get; }
    internal ProjectMutationAuthorization? Authorization { get; }
}

public interface IProjectCreationCompensationGuard {
    Task<bool> CanCompensateForMutationAsync(ProjectWriteAdmission project, CancellationToken cancellationToken = default);
}

public interface IProjectPartyDeletionStateQuery {
    Task<bool> HasProjectDeletionStateForMutationAsync(ProjectWriteAdmission project, CancellationToken cancellationToken = default);
}

public sealed partial class ProjectsService {
    private const string CreationFingerprintDomain = "projects-owner-creation-v1";

    private sealed record ProjectSaveOutcome(Guid Id, ProjectCreationReceipt? CreationReceipt);

    public async Task<Result<ProjectCreationReceipt>> CreateWithReceiptAsync(Guid newProjectId, ProjectEditorModel model,
        Guid? parentProjectId = null, CancellationToken cancellationToken = default,
        ProjectMutationAuthorization? authorization = null) {
        if (newProjectId == Guid.Empty || parentProjectId == Guid.Empty || model.Id.HasValue) {
            return Result<ProjectCreationReceipt>.Failure(Error.Validation("A new project requires a nonempty reserved id and cannot edit an existing project."));
        }
        var result = await SaveWithReceiptCoreAsync(model, parentProjectId, newProjectId, cancellationToken, authorization: authorization);
        return CreationResult(result);
    }

    public async Task<Result<ProjectCreationReceipt>> CreateWithReceiptAsync(ProjectCreationReservation reservation, ProjectEditorModel model,
        CancellationToken cancellationToken = default, ProjectMutationAuthorization? authorization = null) {
        ArgumentNullException.ThrowIfNull(reservation);
        if (model.Id.HasValue) {
            return Result<ProjectCreationReceipt>.Failure(Error.Validation("A new project cannot edit an existing project."));
        }
        var result = await SaveWithReceiptCoreAsync(model, reservation.ParentProjectId, reservation.ProjectId,
            cancellationToken, reservation, authorization);
        return CreationResult(result);
    }

    public async Task<bool> TryCompensateCreationAsync(ProjectCreationReceipt receipt, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(receipt);
        return await DeleteCoreAsync(receipt.Project.ProjectId, cancellationToken, receipt) is not null;
    }

    private static Result<ProjectCreationReceipt> CreationResult(Result<ProjectSaveOutcome> result) {
        if (result.IsFailure) {
            return Result<ProjectCreationReceipt>.Failure(result.Errors);
        }
        return Result<ProjectCreationReceipt>.Success(result.Value?.CreationReceipt
            ?? throw new InvalidOperationException("The owner creation completed without its original creation receipt."));
    }

    private async Task<bool> CanCompensateCreationAsync(ProjectsDbContext context, ProjectCreationReceipt receipt,
        CancellationToken cancellationToken) {
        if (receipt.Project.DatabaseProfileId != writeAdmissionService.DatabaseProfileId) {
            throw new InvalidOperationException("The creation receipt belongs to another database profile.");
        }
        var current = await context.Set<Project>().AsNoTracking()
            .SingleOrDefaultAsync(project => project.Id == receipt.Project.ProjectId, cancellationToken);
        if (current is null || current.LifetimeId != receipt.Project.LifetimeId) {
            return false;
        }
        await writeAdmissionService.RequireAsync(context, receipt.Project, cancellationToken);
        if (receipt.Reservation is { } reservation && !await writeAdmissionService.IsConsumedCreationAsync(context, reservation, cancellationToken)) {
            return false;
        }
        var fingerprint = await ReadCreationFingerprintAsync(context, receipt.Project, cancellationToken);
        if (!string.Equals(fingerprint, receipt.Fingerprint, StringComparison.Ordinal)) {
            return false;
        }
        var guard = creationCompensationGuard
            ?? throw new InvalidOperationException("Native project creation compensation evidence is not installed.");
        using var coordination = coordinatedTransaction.Enter(context);
        return await guard.CanCompensateForMutationAsync(receipt.Project, cancellationToken);
    }

    private static async Task<string> ReadCreationFingerprintAsync(ProjectsDbContext context, ProjectWriteAdmission admission,
        CancellationToken cancellationToken) {
        var project = await context.Set<Project>().AsNoTracking().SingleAsync(row =>
            row.Id == admission.ProjectId && row.LifetimeId == admission.LifetimeId, cancellationToken);
        var phases = await context.Set<ProjectPhase>().AsNoTracking().Where(row => row.ProjectId == admission.ProjectId)
            .OrderBy(row => row.Id).ToArrayAsync(cancellationToken);
        var options = await context.Set<ProjectOptionSelection>().AsNoTracking().Where(row => row.ProjectId == admission.ProjectId)
            .OrderBy(row => row.Id).ToArrayAsync(cancellationToken);
        var hierarchy = await context.Set<ProjectHierarchyLink>().AsNoTracking().Where(row =>
            row.ChildProjectId == admission.ProjectId || row.ParentProjectId == admission.ProjectId)
            .OrderBy(row => row.Id).ToArrayAsync(cancellationToken);
        var payload = JsonSerializer.SerializeToUtf8Bytes(new {
            Domain = CreationFingerprintDomain,
            ProfileId = admission.DatabaseProfileId,
            Project = new {
                project.Id, project.LifetimeId, project.LegacyAgentAccessBindingEligible, project.Name, project.Slug,
                project.Description, project.Objective, project.Status, project.CurrentPhase, project.TargetDateUtc,
                project.CreatedAtUtc, project.UpdatedAtUtc
            },
            Phases = phases,
            Options = options,
            Hierarchy = hierarchy
        });
        return Convert.ToHexStringLower(SHA256.HashData(payload));
    }
}
