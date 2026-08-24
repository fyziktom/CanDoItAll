namespace CanDoItAll.Modules.Processes;

internal enum ProcessAgentExecutionStage
{
    RuntimeToolPreflight,
    ExecutionMetadataComposition,
    ParentArtifactContextHydration,
    ProcessStepPromptComposition,
    AgentExecution,
    AgentOutputValidation,
    ExecutionDetailLoading,
    OutcomeMaterialization,
    CompletionReceiptLoading,
    Completion
}
