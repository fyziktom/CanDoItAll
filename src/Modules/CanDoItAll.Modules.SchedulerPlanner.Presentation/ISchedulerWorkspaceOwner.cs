using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SchedulerPlanner.UI;

namespace CanDoItAll.Modules.SchedulerPlanner.Presentation;

public interface ISchedulerWorkspaceOwner {
    Task<SchedulerWorkspaceData> ReadAsync(SchedulerWorkspaceQuery query, CancellationToken cancellationToken);
    Task<SchedulerDraftValues> DefaultAsync(CancellationToken cancellationToken);
    Task<SchedulerDraftValues> EditorAsync(Guid id, CancellationToken cancellationToken);
    Task<SchedulerWorkflowInputSchema> SchemaAsync(SchedulerDraftValues values, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkflowInputParameterOption>> OptionsAsync(WorkflowInputParameterDescriptor parameter,
        IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken);
    Task<SchedulerWorkflowInputValidationResult> ValidateAsync(SchedulerDraftValues values, CancellationToken cancellationToken);
    Task<SchedulerMutationReceipt> SaveAsync(SchedulerDraftValues values);
    Task<SchedulerMutationReceipt> ToggleAsync(SchedulerPlanCondition expected, bool enabled);
    Task<SchedulerMutationReceipt> DeleteAsync(SchedulerPlanCondition expected);
    string DescribeCron(string expression, string timeZoneId);
}
