using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessDecisionExecutionRegistration {
    public ProcessDecisionExecutionRegistration(ProcessDecisionExecutionContract execution, ProcessDecisionContentContract? content) {
        ArgumentNullException.ThrowIfNull(execution);
        execution.Validate();
        content?.Validate();
        Execution = execution;
        Content = content;
    }

    public ProcessDecisionExecutionContract Execution { get; }
    public ProcessDecisionContentContract? Content { get; }
}
