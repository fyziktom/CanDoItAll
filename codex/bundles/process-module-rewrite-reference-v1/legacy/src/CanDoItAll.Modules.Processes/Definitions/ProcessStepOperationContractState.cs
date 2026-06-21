namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessStepOperationContractNormalizationIssue(
    string Code,
    string Message);

internal sealed record ProcessStepOperationContractNormalizationResult(
    List<ProcessStepOperation> AllowedOperations,
    ProcessStepTargetScope? OperationTargetScope,
    IReadOnlyList<ProcessStepOperationContractNormalizationIssue> Issues);

internal static class ProcessStepOperationContractState
{
    public const string MissingAllowedOperationsCode = "processes.operation-contract.allowed-operations-missing";
    public const string MissingTargetScopeCode = "processes.operation-contract.target-scope-missing";
    public const string InvalidCombinationCode = "processes.operation-contract.invalid-combination";

    public static List<ProcessStepOperation> NormalizeAllowedOperations(IEnumerable<ProcessStepOperation>? operations)
    {
        return operations is null
            ? []
            : operations
                .Distinct()
                .OrderBy(operation => operation)
                .ToList();
    }

    public static ProcessStepOperationContractNormalizationResult NormalizeDeclaredContract(
        ProcessStepKind stepKind,
        IEnumerable<ProcessStepOperation>? operations,
        ProcessStepTargetScope? targetScope,
        bool inferMissingTargetScope = false)
    {
        var normalizedOperations = NormalizeAllowedOperations(operations);
        var hasDeclaredOperations = normalizedOperations.Count > 0;
        var hasDeclaredTargetScope = targetScope.HasValue;
        var issues = new List<ProcessStepOperationContractNormalizationIssue>();

        if (!hasDeclaredOperations && !hasDeclaredTargetScope)
        {
            return new ProcessStepOperationContractNormalizationResult([], null, issues);
        }

        if (!hasDeclaredOperations)
        {
            issues.Add(new ProcessStepOperationContractNormalizationIssue(
                MissingAllowedOperationsCode,
                "The step declares an operation target scope but no allowed operations."));
        }

        ProcessStepTargetScope? resolvedTargetScope = targetScope;
        if (!hasDeclaredTargetScope)
        {
            issues.Add(new ProcessStepOperationContractNormalizationIssue(
                MissingTargetScopeCode,
                "The step declares allowed operations but no operation target scope."));

            if (inferMissingTargetScope)
            {
                resolvedTargetScope = InferTargetScope(stepKind, normalizedOperations);
            }
        }

        var operationSet = new SortedSet<ProcessStepOperation>(normalizedOperations);
        operationSet.Add(ProcessStepOperation.ReadProcessContext);

        if (!hasDeclaredOperations)
        {
            AddDefaultOperationsByStepKind(operationSet, stepKind);
        }

        if (resolvedTargetScope.HasValue)
        {
            AddOperationsImpliedByTargetScope(operationSet, resolvedTargetScope.Value);
            AddInvalidCombinationIssues(operationSet, resolvedTargetScope.Value, issues);
        }

        if (resolvedTargetScope == ProcessStepTargetScope.ManagedProcessArtifactsOnly &&
            operationSet.SetEquals([ProcessStepOperation.ReadProcessContext]))
        {
            operationSet.Add(ProcessStepOperation.WriteManagedProcessArtifacts);
        }

        return new ProcessStepOperationContractNormalizationResult(
            operationSet.ToList(),
            resolvedTargetScope,
            issues);
    }

    public static List<ProcessStepOperation> NormalizeDeclaredAllowedOperations(
        ProcessStepKind stepKind,
        IEnumerable<ProcessStepOperation>? operations,
        ProcessStepTargetScope? targetScope)
        => NormalizeDeclaredContract(stepKind, operations, targetScope).AllowedOperations;

    public static ProcessStepOperationContractNormalizationResult NormalizeResolvedContract(
        ProcessStepKind stepKind,
        IEnumerable<ProcessStepOperation>? operations,
        ProcessStepTargetScope targetScope)
        => NormalizeDeclaredContract(
            stepKind,
            operations,
            targetScope,
            inferMissingTargetScope: true);

    public static ProcessStepTargetScope ResolveExplicitTargetScope(
        string? contractText,
        IReadOnlyCollection<ProcessStepOperation> operations,
        ProcessStepKind stepKind)
    {
        if (!string.IsNullOrWhiteSpace(contractText))
        {
            if (ContainsAny(contractText, "externalproducttargetmutable", "external product target mutable"))
            {
                return ProcessStepTargetScope.ExternalProductTargetMutable;
            }

            if (ContainsAny(contractText, "managedoutputproduct", "managed output product"))
            {
                return ProcessStepTargetScope.ManagedOutputProduct;
            }

            if (ContainsAny(contractText, "externalartifactdestination", "external artifact destination"))
            {
                return ProcessStepTargetScope.ExternalArtifactDestination;
            }

            if (ContainsAny(
                    contractText,
                    "externalproducttargetreadonly",
                    "external product target read only",
                    "external product target readonly"))
            {
                return ProcessStepTargetScope.ExternalProductTargetReadOnly;
            }
        }

        return InferTargetScope(stepKind, operations);
    }

    public static ProcessStepTargetScope InferTargetScope(
        ProcessStepKind stepKind,
        IReadOnlyCollection<ProcessStepOperation> operations)
    {
        if (operations.Contains(ProcessStepOperation.MutateProductTarget))
        {
            return ProcessStepTargetScope.ExternalProductTargetMutable;
        }

        if (operations.Contains(ProcessStepOperation.WriteExternalArtifactDestination))
        {
            return ProcessStepTargetScope.ExternalArtifactDestination;
        }

        if (operations.Contains(ProcessStepOperation.ExecuteExternalAction) ||
            stepKind == ProcessStepKind.Subprocess)
        {
            return ProcessStepTargetScope.ExternalActionControlled;
        }

        if (operations.Contains(ProcessStepOperation.ReadProjectStructure) &&
            !operations.Contains(ProcessStepOperation.MutateProductTarget))
        {
            return ProcessStepTargetScope.ExternalProductTargetReadOnly;
        }

        return ProcessStepTargetScope.ManagedProcessArtifactsOnly;
    }

    public static void AddOperationsImpliedByTargetScope(
        ISet<ProcessStepOperation> operations,
        ProcessStepTargetScope targetScope)
    {
        switch (targetScope)
        {
            case ProcessStepTargetScope.ExternalArtifactDestination:
                operations.Add(ProcessStepOperation.WriteExternalArtifactDestination);
                break;
            case ProcessStepTargetScope.ManagedOutputProduct:
            case ProcessStepTargetScope.ExternalProductTargetMutable:
                operations.Add(ProcessStepOperation.MutateProductTarget);
                break;
            case ProcessStepTargetScope.ExternalProductTargetReadOnly:
                operations.Add(ProcessStepOperation.ReadProjectStructure);
                break;
            case ProcessStepTargetScope.ExternalActionControlled:
                operations.Add(ProcessStepOperation.ExecuteExternalAction);
                break;
        }
    }

    private static void AddDefaultOperationsByStepKind(
        ISet<ProcessStepOperation> operations,
        ProcessStepKind stepKind)
    {
        switch (stepKind)
        {
            case ProcessStepKind.Decision:
            case ProcessStepKind.Approval:
            case ProcessStepKind.Review:
                operations.Add(ProcessStepOperation.WriteManagedProcessArtifacts);
                operations.Add(ProcessStepOperation.EscalateOrDecide);
                break;
            case ProcessStepKind.Subprocess:
                operations.Add(ProcessStepOperation.ExecuteExternalAction);
                break;
            case ProcessStepKind.Start:
            case ProcessStepKind.Work:
            case ProcessStepKind.Delivery:
            case ProcessStepKind.End:
                operations.Add(ProcessStepOperation.WriteManagedProcessArtifacts);
                break;
        }
    }

    private static void AddInvalidCombinationIssues(
        IReadOnlySet<ProcessStepOperation> operations,
        ProcessStepTargetScope targetScope,
        List<ProcessStepOperationContractNormalizationIssue> issues)
    {
        if (operations.Contains(ProcessStepOperation.MutateProductTarget) &&
            targetScope is ProcessStepTargetScope.ManagedProcessArtifactsOnly
                or ProcessStepTargetScope.ExternalArtifactDestination
                or ProcessStepTargetScope.ExternalProductTargetReadOnly
                or ProcessStepTargetScope.ExternalActionControlled)
        {
            issues.Add(new ProcessStepOperationContractNormalizationIssue(
                InvalidCombinationCode,
                $"Operation {ProcessStepOperation.MutateProductTarget} requires a mutable product target scope, not {targetScope}."));
        }

        if (operations.Contains(ProcessStepOperation.WriteExternalArtifactDestination) &&
            targetScope != ProcessStepTargetScope.ExternalArtifactDestination)
        {
            issues.Add(new ProcessStepOperationContractNormalizationIssue(
                InvalidCombinationCode,
                $"Operation {ProcessStepOperation.WriteExternalArtifactDestination} requires target scope {ProcessStepTargetScope.ExternalArtifactDestination}, not {targetScope}."));
        }

        if (operations.Contains(ProcessStepOperation.ExecuteExternalAction) &&
            targetScope != ProcessStepTargetScope.ExternalActionControlled)
        {
            issues.Add(new ProcessStepOperationContractNormalizationIssue(
                InvalidCombinationCode,
                $"Operation {ProcessStepOperation.ExecuteExternalAction} requires target scope {ProcessStepTargetScope.ExternalActionControlled}, not {targetScope}."));
        }

        if (operations.Contains(ProcessStepOperation.RecoverArtifactsOnly) &&
            operations.Contains(ProcessStepOperation.MutateProductTarget))
        {
            issues.Add(new ProcessStepOperationContractNormalizationIssue(
                InvalidCombinationCode,
                $"Operation {ProcessStepOperation.RecoverArtifactsOnly} cannot be combined with {ProcessStepOperation.MutateProductTarget}."));
        }
    }

    private static bool ContainsAny(string value, params string[] tokens)
        => tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
}
