using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Workbench.Insights.UI;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectManagerSummarySource(
    Guid projectId, ProjectWriteAdmission? expectedAdmission, string actorStamp,
    ProjectManagerSummaryViewState retained, ProjectManagerSummaryScopeResolver scopes,
    ProjectManagerSummaryQueryService reports, ProjectWriteAdmissionService admissions,
    IDatabaseProfileRuntimeAccessor runtime, Func<bool> isCurrent,
    ILogger<ProjectManagerSummarySource> logger) : IManagerSummarySource {
    private ProjectWriteAdmission? admission = expectedAdmission;
    private (ManagerScopeId Id, ProjectManagerSummaryOptions Options, ProjectManagerSummaryScopeResolution Scope, IReadOnlyList<ProjectWriteAdmission> Admissions)? candidate;
    private (ManagerReportId Id, ProjectManagerSummarySnapshot Snapshot, IReadOnlyList<ProjectWriteAdmission> Admissions)? accepted;
    private (ManagerReportId Id, ProjectManagerSummarySnapshot Snapshot, IReadOnlyList<ProjectWriteAdmission> Admissions)? prepared;

    internal static string ActorStamp(ClaimsPrincipal? actor) => actor is null ? string.Empty :
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            Authentication = actor.Identity?.AuthenticationType,
            Claims = actor.Claims.OrderBy(claim => claim.Type, StringComparer.Ordinal)
                .ThenBy(claim => claim.Value, StringComparer.Ordinal).Select(claim => new { claim.Type, claim.Value })
        }))));

    public ManagerReportPresentation? Restore() {
        RequireOrigin();
        if (retained.Snapshot is not { } snapshot || admission is null ||
            retained.LifetimeId != admission.LifetimeId || retained.ActorStamp != actorStamp) {
            return null;
        }
        var id = new ManagerReportId(Guid.NewGuid());
        accepted = (id, snapshot, retained.ScopeAdmissions);
        return Present(id, snapshot);
    }

    public async Task<ManagerScopePresentation> ResolveScopeAsync(ProjectManagerSummaryOptions options, CancellationToken cancellationToken) {
        await RequireCurrentAsync(cancellationToken);
        var scope = await scopes.ResolveAsync(projectId, options.Scope, options.ContentMode, cancellationToken);
        var scopeAdmissions = await admissions.CaptureManyAsync(scope.ProjectIds, cancellationToken);
        if (scopeAdmissions.Count != scope.ProjectIds.Count) {
            throw new InvalidOperationException("The report scope changed while its project lifetimes were being read.");
        }
        await RequireCurrentAsync(cancellationToken);
        var id = new ManagerScopeId(Guid.NewGuid());
        candidate = (id, options, scope, scopeAdmissions);
        return new(id, scope.RootProjectName, scope.ProjectIds.Count, scope.DescendantCount, scope.RequiresConfirmation,
            scope.PlanPreflight?.PlanNodeCount, scope.PlanPreflight?.PlanLinkCount, scope.PlanPreflight?.Warnings.ToArray() ?? []);
    }

    public async Task<ManagerReportPresentation> LoadAsync(ManagerScopeId scopeId, ProjectManagerSummaryOptions options,
        Func<ProjectManagerSummaryLoadProgress, ValueTask> progress, CancellationToken cancellationToken) {
        var submitted = candidate is { } value && value.Id == scopeId && value.Options == options
            ? value : throw new InvalidOperationException("The report scope confirmation has expired.");
        await RequireScopeAsync(submitted.Admissions, cancellationToken);
        var snapshot = await reports.LoadAsync(submitted.Scope, options, progress, cancellationToken);
        await RequireScopeAsync(submitted.Admissions, cancellationToken);
        var id = new ManagerReportId(Guid.NewGuid());
        prepared = (id, snapshot, submitted.Admissions);
        candidate = null;
        return Present(id, snapshot);
    }

    public IManagerActivitySource OpenActivity(ManagerReportId report) {
        RequireOrigin();
        if (accepted is not { } current || current.Id != report) {
            throw new InvalidOperationException("The accepted report is no longer available.");
        }
        return new ProjectManagerActivitySource(current.Snapshot, reports, token => RequireScopeAsync(current.Admissions, token), logger);
    }

    public void Retain(ProjectManagerSummaryOptions options, ManagerReportPresentation? report) {
        RequireOrigin();
        if (report is not null && prepared is { } completed && completed.Id == report.Id) {
            accepted = completed;
            prepared = null;
        }
        retained.Options = options;
        retained.ActorStamp = actorStamp;
        retained.LifetimeId = admission?.LifetimeId;
        if (report is not null && accepted is { } current && current.Id == report.Id) {
            retained.Snapshot = current.Snapshot;
            retained.ScopeAdmissions = current.Admissions;
        } else if (report is null) {
            retained.Snapshot = null;
            retained.ScopeAdmissions = [];
        }
    }

    public string DescribeFailure(Exception exception) {
        logger.LogError(exception, "Manager summary read failed for original project {ProjectId} and runtime profile {ProfileId}.", projectId, retained.ProfileId);
        return exception is ProjectStructureAgentException { StatusCode: 413,
            ErrorCode: ProjectPlanAnalyticsErrorCodes.ScopeLimitExceeded or ProjectPlanAnalyticsErrorCodes.PayloadLimitExceeded }
            ? exception.Message : "Manager Summary could not be loaded. The previous successful snapshot, if any, is still shown.";
    }

    private void RequireOrigin() {
        if (!isCurrent() || runtime.ResolveCurrentProfile().Profile.Id != retained.ProfileId ||
            admission is { } captured && (captured.ProjectId != projectId || captured.DatabaseProfileId != retained.ProfileId)) {
            throw new InvalidOperationException("The original reporting context is no longer current.");
        }
    }

    private async Task RequireCurrentAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        RequireOrigin();
        admission ??= await admissions.CaptureAsync(projectId, cancellationToken)
            ?? throw new InvalidOperationException("The original project is unavailable.");
        await admissions.RequireCurrentAsync(admission, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        RequireOrigin();
    }

    private async Task RequireScopeAsync(IReadOnlyList<ProjectWriteAdmission> expected, CancellationToken cancellationToken) {
        await RequireCurrentAsync(cancellationToken);
        await admissions.RequireManyCurrentAsync(expected, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        RequireOrigin();
    }

    private static ManagerReportPresentation Present(ManagerReportId id, ProjectManagerSummarySnapshot report) => new(id,
        report.ProjectId, report.ProjectName, report.Options, report.HistoryFromUtc, report.AsOfUtc, report.GeneratedAtUtc,
        report.Schedule, report.Costs, report.CostBreakdown.ToArray(), report.OtherCurrencyFutureCosts.ToArray(),
        report.ExpenseTrend.ToArray(), report.LatestActivities.Select(item => item with { Tags = item.Tags.ToArray() }).ToArray(), report.Warnings.ToArray());
}
