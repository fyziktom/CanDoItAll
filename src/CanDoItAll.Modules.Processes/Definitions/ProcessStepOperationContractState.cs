namespace CanDoItAll.Modules.Processes;

internal static class ProcessStepOperationContractState
{
    public static List<ProcessStepOperation> NormalizeAllowedOperations(IEnumerable<ProcessStepOperation>? operations)
    {
        return operations is null
            ? []
            : operations
                .Distinct()
                .OrderBy(operation => operation)
                .ToList();
    }
}
