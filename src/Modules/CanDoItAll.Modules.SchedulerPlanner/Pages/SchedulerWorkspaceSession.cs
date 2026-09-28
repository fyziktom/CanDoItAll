using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.SchedulerPlanner.Presentation;
using CanDoItAll.SchedulerPlanner.UI;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.SchedulerPlanner.Pages;

public sealed class SchedulerWorkspaceSession(
    ISchedulerPlannerService service, ISchedulerWorkflowInputSchemaService schemas,
    ISchedulerWorkflowInputOptionService options, ICronDescriptionService descriptions,
    IWorkflowStructureAuthorityFactory authority, ICanonicalRuntimeDatabase database,
    ILogger<SchedulerWorkspaceSession> logger) : ISchedulerWorkspaceOwner {
    private readonly Guid profileId = database.Profile.Profile.Id;
    private readonly long profileGeneration = database.Generation;

    public async Task<SchedulerWorkspaceData> ReadAsync(SchedulerHistoryQuery query, CancellationToken token) {
        RequireProfile();
        var workspace = await service.GetWorkspaceAsync(query, token);
        return new(workspace.Plans, workspace.History, workspace.TargetOptions, workspace.CalendarSurface);
    }
    public async Task<SchedulerDraftValues> DefaultAsync(CancellationToken token) {
        RequireProfile();
        return ToValues(await service.CreateDefaultEditorAsync(token));
    }
    public async Task<SchedulerDraftValues> EditorAsync(Guid id, CancellationToken token) {
        RequireProfile();
        return ToValues(await service.GetPlanEditorAsync(id, token));
    }
    public Task<SchedulerWorkflowInputSchema> SchemaAsync(SchedulerDraftValues values, CancellationToken token) {
        RequireProfile();
        return schemas.ResolveSchemaAsync(new(values.TargetId), values.TargetVersionId is { } version ? new(version) : null, token);
    }
    public Task<IReadOnlyList<WorkflowInputParameterOption>> OptionsAsync(WorkflowInputParameterDescriptor parameter,
        IReadOnlyDictionary<string, string> values, CancellationToken token) {
        RequireProfile();
        return options.ListOptionsAsync(parameter, values, token);
    }
    public Task<SchedulerWorkflowInputValidationResult> ValidateAsync(SchedulerDraftValues values, CancellationToken token) {
        RequireProfile();
        return schemas.ValidateInputAsync(new(values.TargetId), values.TargetVersionId is { } version ? new(version) : null, values.InputJson, token);
    }
    public string DescribeCron(string expression, string timeZoneId) => descriptions.Describe(expression, timeZoneId);

    public async Task<SchedulerMutationReceipt> SaveAsync(SchedulerDraftValues values) {
        var ownerStarted = false;
        try {
            RequireProfile();
            var editor = ToEditor(values);
            editor.StructureAuthority = await authority.CaptureLocalOperatorAsync(WorkflowStructureOperatorSurface.UserInterface);
            RequireProfile();
            ownerStarted = true;
            var saved = await service.SavePlanAsync(editor);
            return new(SchedulerMutationKind.Save, SchedulerMutationStatus.Committed, saved.Id,
                SchedulerMutationStage.ProjectionSynchronized, "Schedule saved and trigger projection synchronized.", FromSaved(saved, editor.InputJson));
        } catch (SchedulerPlanCommittedException exception) {
            return Known(exception);
        } catch (SchedulerPlanValidationException exception) {
            return new(SchedulerMutationKind.Save, SchedulerMutationStatus.Refused, values.Id, SchedulerMutationStage.None, exception.Message);
        } catch (Exception exception) {
            Report(SchedulerMutationKind.Save, values.Id, exception);
            return new(SchedulerMutationKind.Save, ownerStarted ? SchedulerMutationStatus.Unknown : SchedulerMutationStatus.Refused,
                values.Id, SchedulerMutationStage.None, ownerStarted ? "Save outcome unknown. Review the exact plan before retrying." : "Authority could not be captured for this submission.");
        }
    }
    public Task<SchedulerMutationReceipt> ToggleAsync(Guid id, bool enabled) => ExecuteAsync(id,
        enabled ? SchedulerMutationKind.Enable : SchedulerMutationKind.Disable, () => service.SetPlanEnabledAsync(id, enabled));
    public Task<SchedulerMutationReceipt> DeleteAsync(Guid id) => ExecuteAsync(id, SchedulerMutationKind.Delete, () => service.DeletePlanAsync(id));
    private async Task<SchedulerMutationReceipt> ExecuteAsync(Guid id, SchedulerMutationKind kind, Func<Task> action) {
        try {
            RequireProfile();
            await action();
            return new(kind, SchedulerMutationStatus.Committed, id,
                kind == SchedulerMutationKind.Delete ? SchedulerMutationStage.ProjectionSynchronized : SchedulerMutationStage.Persisted,
                kind == SchedulerMutationKind.Delete ? "Deletion and trigger projection completed." : "Enabled state request completed. An already matching state does not resynchronize projection.");
        } catch (SchedulerPlanCommittedException exception) {
            return Known(exception);
        } catch (Exception exception) {
            Report(kind, id, exception);
            return new(kind, SchedulerMutationStatus.Unknown, id, SchedulerMutationStage.None, "Outcome unknown; review persistence and projection before another mutation.");
        }
    }
    private SchedulerMutationReceipt Known(SchedulerPlanCommittedException exception) {
        var fact = exception.Fact;
        var diagnostic = string.Empty;
        try {
            Report(fact.Kind, fact.PlanId, exception.InnerException ?? exception);
        } catch (Exception loggingFailure) {
            diagnostic = $" The diagnostic sink also failed ({loggingFailure.GetType().Name}).";
        }
        return new(fact.Kind, SchedulerMutationStatus.CommittedWithWarning, fact.PlanId, fact.Stage,
            "Plan mutation persisted; follow-up is incomplete. Refresh only reads current state and does not resubmit or repair projection." + diagnostic, FromSaved(fact.Plan, fact.InputJson));
    }
    private void Report(SchedulerMutationKind kind, Guid? id, Exception exception)
        => logger.LogWarning("Scheduler {Kind} for plan {PlanId} in profile {ProfileId} failed. ExceptionType={ExceptionType}.", kind, id, profileId, exception.GetType().Name);
    private void RequireProfile() {
        if (profileId != database.Profile.Profile.Id || profileGeneration != database.Generation) {
            throw new InvalidOperationException("The Scheduler session no longer belongs to the active database profile.");
        }
    }
    private static SchedulerDraftValues FromSaved(SchedulerPlanSummary plan, string input) => new() {
        Id = plan.Id, Name = plan.Name, Description = plan.Description, TargetKind = plan.TargetKind, TargetId = plan.TargetId,
        TargetVersionId = plan.TargetVersionId, CronExpression = plan.CronExpression, TimeZoneId = plan.TimeZoneId,
        MisfirePolicy = plan.MisfirePolicy, IsEnabled = plan.IsEnabled, StartAtUtc = plan.StartAtUtc, EndAtUtc = plan.EndAtUtc, InputJson = input
    };
    internal static SchedulerDraftValues ToValues(SchedulerPlanEditorModel editor) => new() {
        Id = editor.Id, Name = editor.Name, Description = editor.Description, TargetKind = editor.TargetKind, TargetId = editor.TargetId,
        TargetVersionId = editor.TargetVersionId, CronExpression = editor.CronExpression, TimeZoneId = editor.TimeZoneId,
        MisfirePolicy = editor.MisfirePolicy, IsEnabled = editor.IsEnabled, StartAtUtc = editor.StartAtUtc, EndAtUtc = editor.EndAtUtc, InputJson = editor.InputJson
    };
    private static SchedulerPlanEditorModel ToEditor(SchedulerDraftValues values) => new() {
        Id = values.Id, Name = values.Name, Description = values.Description, TargetKind = values.TargetKind, TargetId = values.TargetId,
        TargetVersionId = values.TargetVersionId, CronExpression = values.CronExpression, TimeZoneId = values.TimeZoneId,
        MisfirePolicy = values.MisfirePolicy, IsEnabled = values.IsEnabled, StartAtUtc = values.StartAtUtc, EndAtUtc = values.EndAtUtc, InputJson = values.InputJson
    };
}
