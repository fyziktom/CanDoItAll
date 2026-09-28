using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SchedulerPlanner.UI;

namespace CanDoItAll.Modules.SchedulerPlanner.Presentation;

public interface ISchedulerWorkspaceOwner {
    Task<SchedulerWorkspaceData> ReadAsync(SchedulerHistoryQuery query, CancellationToken cancellationToken);
    Task<SchedulerDraftValues> DefaultAsync(CancellationToken cancellationToken);
    Task<SchedulerDraftValues> EditorAsync(Guid id, CancellationToken cancellationToken);
    Task<SchedulerWorkflowInputSchema> SchemaAsync(SchedulerDraftValues values, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkflowInputParameterOption>> OptionsAsync(WorkflowInputParameterDescriptor parameter,
        IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken);
    Task<SchedulerWorkflowInputValidationResult> ValidateAsync(SchedulerDraftValues values, CancellationToken cancellationToken);
    Task<SchedulerMutationReceipt> SaveAsync(SchedulerDraftValues values);
    Task<SchedulerMutationReceipt> ToggleAsync(Guid id, bool enabled);
    Task<SchedulerMutationReceipt> DeleteAsync(Guid id);
    string DescribeCron(string expression, string timeZoneId);
}
