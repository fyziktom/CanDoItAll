using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    internal enum ProcessStepExecutionBoundary
    {
        ArtifactOnly,
        AnalysisDesign,
        DecisionReview,
        ProductMutation,
        RuntimeValidation,
        ExternalAction,
        Recovery
    }

    internal sealed record ProcessStepOperationContract(
        IReadOnlyList<ProcessStepOperation> AllowedOperations,
        ProcessStepTargetScope TargetScope,
        bool IsExplicit)
    {
        public bool AllowsProductMutation =>
            AllowedOperations.Contains(ProcessStepOperation.MutateProductTarget) ||
            TargetScope is ProcessStepTargetScope.ManagedOutputProduct or ProcessStepTargetScope.ExternalProductTargetMutable;
    }

    internal sealed record ProcessStepExecutionBoundaryDescriptor(
        ProcessStepExecutionBoundary Boundary,
        AgentWorkspaceToolProfileKind WorkspaceToolProfile,
        bool AllowsProductMutation,
        string Summary);

    internal enum ProcessTargetGroundingSourceKind
    {
        TextMention,
        LaunchPlan,
        ProjectStructureContext,
        ProjectStructureCurrentRun,
        ExplicitStepContract,
        UpstreamArtifact,
        UpstreamArtifactProvenance
    }

    internal enum ProcessTargetGroundingAuthority
    {
        ReadOnly,
        Writable
    }

    internal sealed record ProcessTargetGroundingRecord(
        string Alias,
        ProcessTargetGroundingSourceKind SourceKind,
        ProcessTargetGroundingAuthority Authority,
        string IntendedUse,
        string TrustLevel,
        decimal Confidence,
        string Scope);

    private const string ProjectStructureCurrentRunMarker = "current-run";
    private static readonly string[] StaleProjectStructureGroundingMarkers =
    [
        "old-run",
        "old run",
        "previous run",
        "prior run",
        "stale",
        "sibling",
        "out-of-scope",
        "no-go",
        "prohibited"
    ];

    private static string BuildProcessInvocationMetadataJson(
        DispatchCandidate candidate,
        ExecutionInvocationPolicy processInvocationPolicy,
        string? projectStructureGroundingSummary,
        string? artifactInspectionGroundingSummary)
        => ProcessInvocationMetadataBuilder.Build(
            candidate,
            processInvocationPolicy,
            projectStructureGroundingSummary,
            artifactInspectionGroundingSummary);

    private static AgentProcessCooperationMetadata ResolveBoundaryAwareCooperationMetadata(
        AgentProcessCooperationMetadata cooperationMetadata,
        ProcessStepExecutionBoundaryDescriptor executionBoundary)
    {
        var summary = $"{cooperationMetadata.Summary.Trim()} Execution boundary: {executionBoundary.Summary}";
        return cooperationMetadata with
        {
            WorkspaceToolProfile = executionBoundary.WorkspaceToolProfile,
            Summary = summary.Trim()
        };
    }

    private static WorkspaceScopeDescriptor? ResolveContextWorkspaceScope(DispatchCandidate candidate)
    {
        ProcessProjectStructureContextFormatter.TryParse(candidate.Run.TriggerReason, out var projectStructureContext);
        var projectId = projectStructureContext?.ProjectId is { } contextProjectId && contextProjectId != Guid.Empty
            ? contextProjectId
            : candidate.Run.ProjectId;

        return projectId is { } resolvedProjectId && resolvedProjectId != Guid.Empty
            ? WorkspaceScopeDescriptor.Project(resolvedProjectId.ToString("D"))
            : null;
    }

    private static IReadOnlyList<string> ResolveReadOnlyExternalTargetAliases(
        DispatchCandidate candidate,
        IReadOnlyList<ProcessTargetGroundingRecord> targetGroundings,
        IReadOnlyList<string> allowedExternalTargetAliases,
        bool allowExternalTargetMutation,
        ProcessStepOperationContract operationContract)
    {
        var resolvedExternalTargetAliases = PruneAllowedExternalTargetAliasesForCurrentRun(
            targetGroundings.Select(grounding => grounding.Alias));
        if (resolvedExternalTargetAliases.Count == 0)
        {
            return [];
        }

        if (allowExternalTargetMutation)
        {
            var scopedExternalTargetAliases = PreferCurrentRunExternalTargetAliases(candidate, resolvedExternalTargetAliases);
            return scopedExternalTargetAliases
                .Where(alias => !IsAliasCoveredByAny(alias, allowedExternalTargetAliases))
                .ToArray();
        }

        if (!AllowsReadOnlyExternalTargetAccess(candidate, operationContract))
        {
            return [];
        }

        var trustedCurrentTargetAliases = ResolveTrustedCurrentRunProductTargetAliases(candidate, targetGroundings);
        return trustedCurrentTargetAliases.Count > 0
            ? trustedCurrentTargetAliases
            : PreferCurrentRunExternalTargetAliases(candidate, resolvedExternalTargetAliases);
    }

    private static IReadOnlyList<string> ResolveTrustedCurrentRunProductTargetAliases(
        DispatchCandidate candidate,
        IReadOnlyList<ProcessTargetGroundingRecord> targetGroundings)
    {
        var trustedAliases = PruneAllowedExternalTargetAliasesForCurrentRun(
            targetGroundings
                .Where(grounding => grounding.Authority == ProcessTargetGroundingAuthority.Writable)
                .Where(grounding => grounding.SourceKind is
                    ProcessTargetGroundingSourceKind.LaunchPlan or
                    ProcessTargetGroundingSourceKind.ProjectStructureCurrentRun or
                    ProcessTargetGroundingSourceKind.ExplicitStepContract)
                .Select(grounding => grounding.Alias));

        var productAliases = trustedAliases
            .Where(alias => !IsNonProductExternalTargetAlias(alias))
            .Where(alias => !IsLikelyExternalTargetFileAlias(alias))
            .ToArray();

        return PreferCurrentRunExternalTargetAliases(candidate, productAliases);
    }

    private static bool AllowsReadOnlyExternalTargetAccess(
        DispatchCandidate candidate,
        ProcessStepOperationContract operationContract)
    {
        if (operationContract.AllowsProductMutation)
        {
            return false;
        }

        if (operationContract.TargetScope == ProcessStepTargetScope.ExternalProductTargetReadOnly)
        {
            return true;
        }

        return operationContract.AllowedOperations.Contains(ProcessStepOperation.RunValidation) ||
               operationContract.AllowedOperations.Contains(ProcessStepOperation.LaunchRuntime) ||
               operationContract.AllowedOperations.Contains(ProcessStepOperation.CaptureRuntimeProof) ||
               IsProductReadOnlyValidationStep(candidate);
    }

    private static bool AllowsExternalTargetMutation(
        DispatchCandidate candidate,
        ProcessStepExecutionBoundaryDescriptor executionBoundary,
        ProcessStepOperationContract operationContract,
        string? projectStructureGroundingSummary)
    {
        if (operationContract.TargetScope == ProcessStepTargetScope.ExternalArtifactDestination)
        {
            return true;
        }

        if ((executionBoundary.Boundary is ProcessStepExecutionBoundary.ArtifactOnly or
                ProcessStepExecutionBoundary.AnalysisDesign or
                ProcessStepExecutionBoundary.ProductMutation) &&
            LooksLikeExternalArtifactDestination(candidate, projectStructureGroundingSummary))
        {
            return true;
        }

        return executionBoundary.AllowsProductMutation &&
               operationContract.AllowsProductMutation &&
               (RequiresConcreteImplementationProof(candidate) ||
                ContainsProductRepairIntent(candidate) ||
                IsDotNetSolutionSetupScaffoldMutationStep(candidate) ||
                executionBoundary.Boundary == ProcessStepExecutionBoundary.ProductMutation);
    }

    private static ProcessStepExecutionBoundaryDescriptor ResolveProcessStepExecutionBoundary(DispatchCandidate candidate)
        => ResolveProcessStepExecutionBoundary(candidate, ResolveProcessStepOperationContract(candidate));

    private static ProcessStepExecutionBoundaryDescriptor ResolveProcessStepExecutionBoundary(
        DispatchCandidate candidate,
        ProcessStepOperationContract operationContract)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var stepText = CollapsePromptWhitespace(string.Join(
                ' ',
                candidate.StepRun.Title,
                candidate.StepRun.CurrentExecutorName,
                candidate.StepDefinition.Key,
                candidate.StepDefinition.Title,
                candidate.StepDefinition.InputContractSummary,
                candidate.StepDefinition.OutputContractSummary,
                candidate.StepDefinition.EvidenceContractSummary,
                candidate.WorkBrief?.Title,
                candidate.WorkBrief?.WorkBriefText,
                candidate.WorkBrief?.HandoffSummary,
                candidate.WorkBrief?.AssignmentReason,
                candidate.WorkBrief?.ExpectedOutcome,
                candidate.WorkBrief?.EvidenceExpectationSummary,
                string.Join(" ", candidate.ExpectedArtifacts.Select(item => $"{item.ArtifactKind} {item.Title} {item.ValidationRequirementSummary} {item.AllowedFutureUsageSummary}"))))
            .ToLowerInvariant();

        if (operationContract.IsExplicit)
        {
            return ResolveExplicitOperationContractBoundary(operationContract);
        }

        if (candidate.StepRun.StepKind == ProcessStepKind.Subprocess)
        {
            return new ProcessStepExecutionBoundaryDescriptor(
                ProcessStepExecutionBoundary.ExternalAction,
                AgentWorkspaceToolProfileKind.ReadOnly,
                AllowsProductMutation: false,
                "Subprocess parent step observes child process state and records process artifacts.");
        }

        if (IsDotNetSolutionSetupScaffoldMutationStep(candidate))
        {
            return new ProcessStepExecutionBoundaryDescriptor(
                ProcessStepExecutionBoundary.ProductMutation,
                AgentWorkspaceToolProfileKind.SoftwareDevelopment,
                AllowsProductMutation: true,
                "Step is an explicit product scaffold or setup mutation step.");
        }

        if (LooksLikeAnalysisOrDesignBoundary(candidate, stepText))
        {
            return new ProcessStepExecutionBoundaryDescriptor(
                ProcessStepExecutionBoundary.AnalysisDesign,
                AgentWorkspaceToolProfileKind.ArchitectureReview,
                AllowsProductMutation: false,
                "Architecture, planning, scope, or analysis step may create managed process artifacts but not mutate product targets.");
        }

        if (RequiresConcreteBrowserProof(candidate, stepText) || LooksLikeRuntimeValidationBoundary(candidate, stepText))
        {
            return new ProcessStepExecutionBoundaryDescriptor(
                ProcessStepExecutionBoundary.RuntimeValidation,
                AgentWorkspaceToolProfileKind.QualityValidation,
                AllowsProductMutation: false,
                "Validation step may inspect, run, and record evidence but must route defects instead of mutating product targets.");
        }

        if (candidate.StepRun.StepKind is ProcessStepKind.Decision or ProcessStepKind.Approval or ProcessStepKind.Review)
        {
            return new ProcessStepExecutionBoundaryDescriptor(
                ProcessStepExecutionBoundary.DecisionReview,
                AgentWorkspaceToolProfileKind.BusinessAnalysis,
                AllowsProductMutation: false,
                "Decision, approval, or review step may record governed dispositions without product mutation.");
        }

        if (LooksLikeRecoveryBoundary(stepText))
        {
            return new ProcessStepExecutionBoundaryDescriptor(
                ProcessStepExecutionBoundary.Recovery,
                AgentWorkspaceToolProfileKind.SoftwareDevelopment,
                AllowsProductMutation: true,
                "Recovery or repair execution step may perform scoped product mutation.");
        }

        if (RequiresConcreteImplementationProof(candidate) ||
            LooksLikeProductMutationBoundary(candidate, stepText))
        {
            return new ProcessStepExecutionBoundaryDescriptor(
                ProcessStepExecutionBoundary.ProductMutation,
                AgentWorkspaceToolProfileKind.SoftwareDevelopment,
                AllowsProductMutation: true,
                "Implementation step is allowed to mutate grounded product targets.");
        }

        return new ProcessStepExecutionBoundaryDescriptor(
            ProcessStepExecutionBoundary.ArtifactOnly,
            AgentWorkspaceToolProfileKind.BusinessAnalysis,
            AllowsProductMutation: false,
            "Step may create managed process artifacts only.");
    }

    private static ProcessStepOperationContract ResolveProcessStepOperationContract(DispatchCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (TryResolvePersistedOperationContract(candidate.StepDefinition, out var persistedContract))
        {
            return persistedContract;
        }

        // Legacy prose inference remains only for definitions that have not declared persisted operation contracts.
        var contractText = CollapsePromptWhitespace(string.Join(
                ' ',
                candidate.StepDefinition.Notes,
                candidate.StepDefinition.InputContractSummary,
                candidate.StepDefinition.OutputContractSummary,
                candidate.StepDefinition.EvidenceContractSummary,
                candidate.StepDefinition.ExceptionPolicySummary,
                candidate.WorkBrief?.WorkBriefText,
                candidate.WorkBrief?.ExpectedOutcome,
                candidate.WorkBrief?.EvidenceExpectationSummary,
                string.Join(" ", candidate.ExpectedArtifacts.Select(item => $"{item.Title} {item.ValidationRequirementSummary} {item.AllowedFutureUsageSummary}"))))
            .ToLowerInvariant();

        if (TryResolveExplicitOperationContract(candidate.StepRun.StepKind, contractText, out var explicitContract))
        {
            return explicitContract;
        }

        var operations = new SortedSet<ProcessStepOperation>();
        operations.Add(ProcessStepOperation.ReadProcessContext);
        if (candidate.ArtifactInputs.Count > 0)
        {
            operations.Add(ProcessStepOperation.ReadUpstreamArtifacts);
        }

        if (candidate.ExpectedArtifacts.Count > 0)
        {
            operations.Add(ProcessStepOperation.WriteManagedProcessArtifacts);
        }

        if (candidate.StepRun.StepKind is ProcessStepKind.Decision or ProcessStepKind.Approval or ProcessStepKind.Review)
        {
            operations.Add(ProcessStepOperation.EscalateOrDecide);
        }

        if (RequiresConcreteBrowserProof(candidate, contractText) || LooksLikeRuntimeValidationBoundary(candidate, contractText))
        {
            operations.Add(ProcessStepOperation.RunValidation);
            operations.Add(ProcessStepOperation.CaptureRuntimeProof);
        }

        if (candidate.StepRun.StepKind == ProcessStepKind.Subprocess)
        {
            operations.Add(ProcessStepOperation.ExecuteExternalAction);
            return CreateOperationContract(
                candidate.StepRun.StepKind,
                operations,
                ProcessStepTargetScope.ExternalActionControlled,
                isExplicit: false);
        }

        if (LooksLikeExternalArtifactDestination(candidate, contractText))
        {
            operations.Add(ProcessStepOperation.WriteExternalArtifactDestination);
            return CreateOperationContract(
                candidate.StepRun.StepKind,
                operations,
                ProcessStepTargetScope.ExternalArtifactDestination,
                isExplicit: false);
        }

        if (RequiresConcreteImplementationProof(candidate) ||
            ContainsProductRepairIntent(candidate) ||
            IsDotNetSolutionSetupScaffoldMutationStep(candidate) ||
            LooksLikeProductMutationBoundary(candidate, contractText, requireStrongSignal: true))
        {
            operations.Add(ProcessStepOperation.MutateProductTarget);
            return CreateOperationContract(
                candidate.StepRun.StepKind,
                operations,
                ProcessStepTargetScope.ExternalProductTargetMutable,
                isExplicit: false);
        }

        if (IsProductReadOnlyValidationStep(candidate))
        {
            return CreateOperationContract(
                candidate.StepRun.StepKind,
                operations,
                ProcessStepTargetScope.ExternalProductTargetReadOnly,
                isExplicit: false);
        }

        return CreateOperationContract(
            candidate.StepRun.StepKind,
            operations,
            ProcessStepTargetScope.ManagedProcessArtifactsOnly,
            isExplicit: false);
    }

    private static bool TryResolvePersistedOperationContract(
        ProcessStepDefinition stepDefinition,
        out ProcessStepOperationContract contract)
    {
        contract = new ProcessStepOperationContract(
            [ProcessStepOperation.ReadProcessContext, ProcessStepOperation.WriteManagedProcessArtifacts],
            ProcessStepTargetScope.ManagedProcessArtifactsOnly,
            IsExplicit: false);

        if (stepDefinition.AllowedOperations.Count == 0 &&
            !stepDefinition.OperationTargetScope.HasValue)
        {
            return false;
        }

        var normalizedContract = ProcessStepOperationContractState.NormalizeDeclaredContract(
            stepDefinition.StepKind,
            stepDefinition.AllowedOperations,
            stepDefinition.OperationTargetScope,
            inferMissingTargetScope: true);
        ThrowIfInvalidOperationContract(normalizedContract);
        var targetScope = normalizedContract.OperationTargetScope ??
            ProcessStepTargetScope.ManagedProcessArtifactsOnly;

        contract = new ProcessStepOperationContract(
            normalizedContract.AllowedOperations,
            targetScope,
            IsExplicit: true);
        return true;
    }

    private static bool TryResolveExplicitOperationContract(
        ProcessStepKind stepKind,
        string contractText,
        out ProcessStepOperationContract contract)
    {
        contract = new ProcessStepOperationContract(
            [ProcessStepOperation.ReadProcessContext, ProcessStepOperation.WriteManagedProcessArtifacts],
            ProcessStepTargetScope.ManagedProcessArtifactsOnly,
            IsExplicit: false);
        if (string.IsNullOrWhiteSpace(contractText) ||
            !ContainsAnyToken(contractText, "operation contract", "allowed operations", "target scope", "step contract"))
        {
            return false;
        }

        var operations = new SortedSet<ProcessStepOperation>();
        operations.Add(ProcessStepOperation.ReadProcessContext);
        AddExplicitOperationIfMentioned(contractText, operations, ProcessStepOperation.ReadProjectStructure, "readprojectstructure", "read project structure");
        AddExplicitOperationIfMentioned(contractText, operations, ProcessStepOperation.ReadUpstreamArtifacts, "readupstreamartifacts", "read upstream artifacts");
        AddExplicitOperationIfMentioned(contractText, operations, ProcessStepOperation.WriteManagedProcessArtifacts, "writemanagedprocessartifacts", "write managed process artifacts", "artifact-only", "artifact only");
        AddExplicitOperationIfMentioned(contractText, operations, ProcessStepOperation.WriteExternalArtifactDestination, "writeexternalartifactdestination", "external artifact destination");
        AddExplicitOperationIfMentioned(contractText, operations, ProcessStepOperation.MutateProductTarget, "mutateproducttarget", "mutate product target", "product mutation");
        AddExplicitOperationIfMentioned(contractText, operations, ProcessStepOperation.RunValidation, "runvalidation", "run validation");
        AddExplicitOperationIfMentioned(contractText, operations, ProcessStepOperation.LaunchRuntime, "launchruntime", "launch runtime");
        AddExplicitOperationIfMentioned(contractText, operations, ProcessStepOperation.CaptureRuntimeProof, "captureruntimeproof", "capture runtime proof");
        AddExplicitOperationIfMentioned(contractText, operations, ProcessStepOperation.ExecuteExternalAction, "executeexternalaction", "execute external action");
        AddExplicitOperationIfMentioned(contractText, operations, ProcessStepOperation.RecoverArtifactsOnly, "recoverartifactsonly", "recover artifacts only");
        AddExplicitOperationIfMentioned(contractText, operations, ProcessStepOperation.EscalateOrDecide, "escalateordecide", "escalate or decide");
        if (operations.Count == 1)
        {
            operations.Add(ProcessStepOperation.WriteManagedProcessArtifacts);
        }

        var targetScope = ProcessStepOperationContractState.ResolveExplicitTargetScope(
            contractText,
            operations,
            stepKind);
        contract = CreateOperationContract(
            stepKind,
            operations,
            targetScope,
            isExplicit: true);
        return true;
    }

    private static void AddExplicitOperationIfMentioned(
        string contractText,
        ISet<ProcessStepOperation> operations,
        ProcessStepOperation operation,
        params string[] tokens)
    {
        if (tokens.Any(token => contractText.Contains(token, StringComparison.OrdinalIgnoreCase)))
        {
            operations.Add(operation);
        }
    }

    private static ProcessStepOperationContract CreateOperationContract(
        ProcessStepKind stepKind,
        IEnumerable<ProcessStepOperation> operations,
        ProcessStepTargetScope targetScope,
        bool isExplicit)
    {
        var normalizedContract = ProcessStepOperationContractState.NormalizeResolvedContract(
            stepKind,
            operations,
            targetScope);
        ThrowIfInvalidOperationContract(normalizedContract);

        return new ProcessStepOperationContract(
            normalizedContract.AllowedOperations,
            normalizedContract.OperationTargetScope ?? targetScope,
            isExplicit);
    }

    private static void ThrowIfInvalidOperationContract(ProcessStepOperationContractNormalizationResult normalizedContract)
    {
        var invalidIssue = normalizedContract.Issues.FirstOrDefault(issue =>
            string.Equals(
                issue.Code,
                ProcessStepOperationContractState.InvalidCombinationCode,
                StringComparison.Ordinal));
        if (invalidIssue is null)
        {
            return;
        }

        throw new InvalidOperationException($"Invalid process step operation contract: {invalidIssue.Message}");
    }

    private static ProcessStepExecutionBoundaryDescriptor ResolveExplicitOperationContractBoundary(
        ProcessStepOperationContract operationContract)
    {
        if (operationContract.AllowedOperations.Contains(ProcessStepOperation.ExecuteExternalAction))
        {
            if (operationContract.AllowedOperations.Contains(ProcessStepOperation.WriteManagedProcessArtifacts) ||
                operationContract.AllowedOperations.Contains(ProcessStepOperation.WriteExternalArtifactDestination))
            {
                return new ProcessStepExecutionBoundaryDescriptor(
                    ProcessStepExecutionBoundary.ExternalAction,
                    AgentWorkspaceToolProfileKind.BusinessAnalysis,
                    AllowsProductMutation: false,
                    "Explicit operation contract allows controlled external action and managed artifact writeback without product mutation.");
            }

            return new ProcessStepExecutionBoundaryDescriptor(
                ProcessStepExecutionBoundary.ExternalAction,
                AgentWorkspaceToolProfileKind.ReadOnly,
                AllowsProductMutation: false,
                "Explicit operation contract allows controlled external action without product mutation.");
        }

        if (operationContract.AllowedOperations.Contains(ProcessStepOperation.RecoverArtifactsOnly))
        {
            return new ProcessStepExecutionBoundaryDescriptor(
                ProcessStepExecutionBoundary.Recovery,
                AgentWorkspaceToolProfileKind.BusinessAnalysis,
                AllowsProductMutation: false,
                "Explicit operation contract allows artifact recovery only.");
        }

        if (operationContract.AllowsProductMutation)
        {
            return new ProcessStepExecutionBoundaryDescriptor(
                ProcessStepExecutionBoundary.ProductMutation,
                AgentWorkspaceToolProfileKind.SoftwareDevelopment,
                AllowsProductMutation: true,
                "Explicit operation contract allows scoped product mutation.");
        }

        if (operationContract.AllowedOperations.Contains(ProcessStepOperation.RunValidation) ||
            operationContract.AllowedOperations.Contains(ProcessStepOperation.CaptureRuntimeProof))
        {
            return new ProcessStepExecutionBoundaryDescriptor(
                ProcessStepExecutionBoundary.RuntimeValidation,
                AgentWorkspaceToolProfileKind.QualityValidation,
                AllowsProductMutation: false,
                "Explicit operation contract allows validation and evidence capture without product mutation.");
        }

        if (operationContract.AllowedOperations.Contains(ProcessStepOperation.EscalateOrDecide))
        {
            return new ProcessStepExecutionBoundaryDescriptor(
                ProcessStepExecutionBoundary.DecisionReview,
                AgentWorkspaceToolProfileKind.BusinessAnalysis,
                AllowsProductMutation: false,
                "Explicit operation contract allows governed disposition without product mutation.");
        }

        return new ProcessStepExecutionBoundaryDescriptor(
            ProcessStepExecutionBoundary.ArtifactOnly,
            AgentWorkspaceToolProfileKind.BusinessAnalysis,
            AllowsProductMutation: false,
            "Explicit operation contract allows managed process artifact writes only.");
    }

    private static bool LooksLikeAnalysisOrDesignBoundary(
        DispatchCandidate candidate,
        string stepText)
    {
        if (candidate.StepRun.StepKind == ProcessStepKind.Start)
        {
            return true;
        }

        if (candidate.StepRun.StepKind == ProcessStepKind.Work &&
            LooksLikeProductMutationBoundary(candidate, stepText))
        {
            return false;
        }

        if (candidate.ExpectedArtifacts.Any(item => item.ArtifactKind is ProcessArtifactKind.Decision or ProcessArtifactKind.DecisionRecord or ProcessArtifactKind.Brief) &&
            ContainsAnyToken(
                stepText,
                "architecture",
                "architect",
                "adr",
                "design",
                "scope",
                "planning",
                "plan",
                "intake",
                "analysis",
                "source-of-truth",
                "source of truth",
                "canonical",
                "boundary",
                "strategy"))
        {
            return true;
        }

        return ContainsAnyToken(
                   stepText,
                   "architecture review",
                   "architecture decision",
                   "design review",
                   "scope packet",
                   "slice boundary",
                   "planning packet",
                   "intake packet",
                   "source-of-truth",
                   "source of truth") &&
               !LooksLikeProductMutationBoundary(candidate, stepText, requireStrongSignal: true);
    }

    private static bool LooksLikeRuntimeValidationBoundary(
        DispatchCandidate candidate,
        string stepText)
    {
        return candidate.StepRun.StepKind == ProcessStepKind.Review &&
               ContainsAnyToken(
                   stepText,
                   "qa",
                   "quality",
                   "validation",
                   "validate",
                   "test",
                   "proof",
                   "browser",
                   "runtime",
                   "smoke",
                   "inspection",
                   "review");
    }

    private static bool LooksLikeRecoveryBoundary(string stepText)
    {
        return ContainsAnyToken(
            stepText,
            "repair implementation",
            "repair step",
            "rework implementation",
            "remediation implementation",
            "fix implementation");
    }

    private static bool LooksLikeProductMutationBoundary(
        DispatchCandidate candidate,
        string stepText,
        bool requireStrongSignal = false)
    {
        var hasStrongMutationVerb = ContainsAnyToken(
            stepText,
            "implement",
            "implementation",
            "scaffold",
            "code",
            "write product",
            "change product",
            "modify product",
            "repair",
            "fix",
            "rework");
        var hasBroadCreationVerb = ContainsAnyToken(stepText, "build", "create", "generate", "write");
        var hasProductTargetSignal = ContainsAnyToken(
            stepText,
            "product file",
            "product files",
            "product root",
            "source file",
            "source files",
            "source root",
            "target app",
            "requested app",
            "web app",
            "console app",
            "application",
            "feature",
            "component",
            "app project",
            "project file",
            "solution file",
            "runnable",
            "change set",
            "implementation change",
            "deliverable files");
        if (!hasStrongMutationVerb && !(hasBroadCreationVerb && hasProductTargetSignal))
        {
            return false;
        }

        if (candidate.ExpectedArtifacts.Any(item => item.ArtifactKind == ProcessArtifactKind.Deliverable) &&
            (hasStrongMutationVerb || hasProductTargetSignal))
        {
            return true;
        }

        if (requireStrongSignal)
        {
            return false;
        }

        return hasStrongMutationVerb &&
               hasProductTargetSignal &&
               candidate.StepRun.StepKind is (ProcessStepKind.Work or ProcessStepKind.Delivery);
    }

    private static bool ContainsAnyToken(string text, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return tokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsProductReadOnlyValidationStep(DispatchCandidate candidate)
    {
        if (RequiresConcreteImplementationProof(candidate) ||
            ContainsProductRepairIntent(candidate))
        {
            return false;
        }

        var stepText = CollapsePromptWhitespace(string.Join(
                ' ',
                candidate.StepRun.Title,
                candidate.StepRun.CurrentExecutorName,
                candidate.WorkBrief?.Title,
                candidate.WorkBrief?.WorkBriefText,
                candidate.WorkBrief?.ExpectedOutcome))
            .ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(stepText))
        {
            return false;
        }

        return RequiresConcreteBrowserProof(candidate) ||
               stepText.Contains("qa", StringComparison.Ordinal) ||
               stepText.Contains("quality", StringComparison.Ordinal) ||
               stepText.Contains("proof", StringComparison.Ordinal) ||
               stepText.Contains("review", StringComparison.Ordinal) ||
               stepText.Contains("scope", StringComparison.Ordinal) ||
               stepText.Contains("intake", StringComparison.Ordinal) ||
               stepText.Contains("boundary", StringComparison.Ordinal) ||
               stepText.Contains("planning", StringComparison.Ordinal) ||
               stepText.Contains("architecture", StringComparison.Ordinal) ||
               stepText.Contains("architect", StringComparison.Ordinal) ||
               stepText.Contains("source-of-truth", StringComparison.Ordinal) ||
               stepText.Contains("canonical", StringComparison.Ordinal) ||
               stepText.Contains("security", StringComparison.Ordinal) ||
               stepText.Contains("readiness", StringComparison.Ordinal) ||
               stepText.Contains("approval", StringComparison.Ordinal);
    }

    private static bool ContainsProductRepairIntent(DispatchCandidate candidate)
    {
        var stepText = CollapsePromptWhitespace(string.Join(
                ' ',
                candidate.StepRun.Title,
                candidate.WorkBrief?.Title,
                candidate.WorkBrief?.WorkBriefText,
                candidate.WorkBrief?.ExpectedOutcome,
                candidate.WorkBrief?.AssignmentReason))
            .ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(stepText))
        {
            return false;
        }

        var boundedStepText = $" {stepText} ";
        return boundedStepText.Contains(" repair ", StringComparison.Ordinal) ||
               boundedStepText.Contains(" repairs ", StringComparison.Ordinal) ||
               boundedStepText.Contains(" repaired ", StringComparison.Ordinal) ||
               boundedStepText.Contains(" repairing ", StringComparison.Ordinal) ||
               boundedStepText.Contains(" fix ", StringComparison.Ordinal) ||
               boundedStepText.Contains(" fixes ", StringComparison.Ordinal) ||
               boundedStepText.Contains(" fixed ", StringComparison.Ordinal) ||
               boundedStepText.Contains(" fixing ", StringComparison.Ordinal) ||
               boundedStepText.Contains(" rework ", StringComparison.Ordinal) ||
               stepText.Contains("change requested", StringComparison.Ordinal) ||
               stepText.Contains("changes requested", StringComparison.Ordinal);
    }

    private static IReadOnlyList<ProcessTargetGroundingRecord> ResolveExternalTargetGroundings(
        DispatchCandidate candidate,
        string? projectStructureGroundingSummary,
        string? artifactInspectionGroundingSummary)
    {
        var groundings = new List<ProcessTargetGroundingRecord>();
        AddExternalTargetGroundings(
            groundings,
            candidate.Run.TriggerReason,
            ProcessTargetGroundingSourceKind.LaunchPlan,
            ProcessTargetGroundingAuthority.Writable);
        AddProjectStructureGroundings(groundings, candidate, projectStructureGroundingSummary);
        AddExplicitStepContractGroundings(groundings, candidate);
        AddExternalTargetGroundings(
            groundings,
            candidate.Run.Name,
            ProcessTargetGroundingSourceKind.TextMention,
            ProcessTargetGroundingAuthority.ReadOnly);
        AddExternalTargetGroundings(
            groundings,
            artifactInspectionGroundingSummary,
            ProcessTargetGroundingSourceKind.UpstreamArtifact,
            ProcessTargetGroundingAuthority.ReadOnly);
        AddWorkBriefTextMentionGroundings(groundings, candidate);
        AddArtifactInputGroundings(groundings, candidate);

        return groundings
            .Where(grounding => !string.IsNullOrWhiteSpace(grounding.Alias))
            .DistinctBy(grounding => (
                Alias: grounding.Alias.ToUpperInvariant(),
                grounding.SourceKind,
                grounding.Authority))
            .ToArray();
    }

    private static void AddProjectStructureGroundings(
        List<ProcessTargetGroundingRecord> groundings,
        DispatchCandidate candidate,
        string? projectStructureGroundingSummary)
    {
        AddExternalTargetGroundings(
            groundings,
            projectStructureGroundingSummary,
            ProcessTargetGroundingSourceKind.ProjectStructureContext,
            ProcessTargetGroundingAuthority.ReadOnly);
        foreach (var currentRunGroundingLine in EnumerateProjectStructureWritableGroundingLines(candidate, projectStructureGroundingSummary))
        {
            AddExternalTargetGroundings(
                groundings,
                currentRunGroundingLine,
                ProcessTargetGroundingSourceKind.ProjectStructureCurrentRun,
                ProcessTargetGroundingAuthority.Writable);
        }
    }

    private static IEnumerable<string> EnumerateProjectStructureWritableGroundingLines(
        DispatchCandidate candidate,
        string? projectStructureGroundingSummary)
    {
        if (string.IsNullOrWhiteSpace(projectStructureGroundingSummary))
        {
            yield break;
        }

        var currentRunTokens = ResolveCurrentRunExternalTargetAliasTokens(candidate);
        foreach (var line in projectStructureGroundingSummary.Split(
                     ['\r', '\n'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (IsProjectStructureWritableGroundingLine(line, currentRunTokens))
            {
                yield return line;
            }
        }
    }

    private static bool IsProjectStructureWritableGroundingLine(
        string line,
        IReadOnlyList<string> currentRunTokens)
    {
        if (StaleProjectStructureGroundingMarkers.Any(marker => line.Contains(marker, StringComparison.OrdinalIgnoreCase)) ||
            ExtractExternalTargetAliasesFromText(line).Count == 0)
        {
            return false;
        }

        return line.Contains(ProjectStructureCurrentRunMarker, StringComparison.OrdinalIgnoreCase) ||
               currentRunTokens.Any(token => line.Contains(token, StringComparison.OrdinalIgnoreCase)) ||
               line.Contains(" mapped to ", StringComparison.OrdinalIgnoreCase) ||
               line.Contains(" must be written at ", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddExplicitStepContractGroundings(
        List<ProcessTargetGroundingRecord> groundings,
        DispatchCandidate candidate)
    {
        AddExternalTargetGroundings(
            groundings,
            candidate.StepDefinition.InputContractSummary,
            ProcessTargetGroundingSourceKind.ExplicitStepContract,
            ProcessTargetGroundingAuthority.Writable);
        AddExternalTargetGroundings(
            groundings,
            candidate.StepDefinition.OutputContractSummary,
            ProcessTargetGroundingSourceKind.ExplicitStepContract,
            ProcessTargetGroundingAuthority.Writable);
        AddExternalTargetGroundings(
            groundings,
            candidate.StepDefinition.EvidenceContractSummary,
            ProcessTargetGroundingSourceKind.ExplicitStepContract,
            ProcessTargetGroundingAuthority.Writable);
        foreach (var expectedArtifact in candidate.ExpectedArtifacts)
        {
            AddExternalTargetGroundings(
                groundings,
                string.Join(
                    ' ',
                    expectedArtifact.Title,
                    expectedArtifact.ValidationRequirementSummary,
                    expectedArtifact.AllowedFutureUsageSummary),
                ProcessTargetGroundingSourceKind.ExplicitStepContract,
                ProcessTargetGroundingAuthority.Writable);
        }
    }

    private static void AddWorkBriefTextMentionGroundings(
        List<ProcessTargetGroundingRecord> groundings,
        DispatchCandidate candidate)
    {
        if (candidate.WorkBrief is null)
        {
            return;
        }

        foreach (var source in new[]
                 {
                     candidate.WorkBrief.Title,
                     candidate.WorkBrief.WorkBriefText,
                     candidate.WorkBrief.HandoffSummary,
                     candidate.WorkBrief.AssignmentReason,
                     candidate.WorkBrief.ExpectedOutcome,
                     candidate.WorkBrief.EvidenceExpectationSummary
                 })
        {
            AddExternalTargetGroundings(
                groundings,
                source,
                ProcessTargetGroundingSourceKind.TextMention,
                ProcessTargetGroundingAuthority.ReadOnly);
        }
    }

    private static void AddArtifactInputGroundings(
        List<ProcessTargetGroundingRecord> groundings,
        DispatchCandidate candidate)
    {
        foreach (var artifactInput in candidate.ArtifactInputs)
        {
            AddExternalTargetGroundings(
                groundings,
                artifactInput.SourceStepTitle,
                ProcessTargetGroundingSourceKind.UpstreamArtifact,
                ProcessTargetGroundingAuthority.ReadOnly);
            AddExternalTargetGroundings(
                groundings,
                artifactInput.ExpectedArtifactTitle,
                ProcessTargetGroundingSourceKind.UpstreamArtifact,
                ProcessTargetGroundingAuthority.ReadOnly);
            foreach (var artifact in artifactInput.Artifacts)
            {
                AddExternalTargetGroundings(
                    groundings,
                    string.Join(' ', artifact.Title, artifact.ArtifactKind, artifact.ManagedStoragePath, artifact.ReviewSummary),
                    ProcessTargetGroundingSourceKind.UpstreamArtifact,
                    ProcessTargetGroundingAuthority.ReadOnly);
                AddExternalTargetGroundings(
                    groundings,
                    artifact.ProvenanceSummary,
                    ProcessTargetGroundingSourceKind.UpstreamArtifactProvenance,
                    ProcessTargetGroundingAuthority.ReadOnly);
            }
        }
    }

    private static void AddExternalTargetGroundings(
        List<ProcessTargetGroundingRecord> groundings,
        string? text,
        ProcessTargetGroundingSourceKind sourceKind,
        ProcessTargetGroundingAuthority authority)
    {
        foreach (var alias in ExtractExternalTargetAliasesFromText(text))
        {
            groundings.Add(new ProcessTargetGroundingRecord(
                alias,
                sourceKind,
                authority,
                ResolveGroundingIntendedUse(sourceKind, authority),
                ResolveGroundingTrustLevel(sourceKind, authority),
                ResolveGroundingConfidence(sourceKind, authority),
                ResolveGroundingScope(alias)));
        }
    }

    private static IReadOnlyList<object> BuildGroundedTargetAliasLedger(
        IReadOnlyList<ProcessTargetGroundingRecord> targetGroundings,
        IReadOnlyList<string> writableAliases)
    {
        var writableAliasSet = writableAliases.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return targetGroundings
            .OrderBy(grounding => grounding.Alias, StringComparer.OrdinalIgnoreCase)
            .ThenBy(grounding => grounding.SourceKind.ToString(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(grounding => grounding.Authority.ToString(), StringComparer.OrdinalIgnoreCase)
            .Select(grounding => new
            {
                alias = grounding.Alias,
                sourceKind = grounding.SourceKind.ToString(),
                authority = grounding.Authority.ToString(),
                effectiveAccess = writableAliasSet.Any(root => IsAliasCoveredByAny(grounding.Alias, [root]))
                    ? ProcessTargetGroundingAuthority.Writable.ToString()
                    : ProcessTargetGroundingAuthority.ReadOnly.ToString(),
                intendedUse = grounding.IntendedUse,
                trustLevel = grounding.TrustLevel,
                confidence = grounding.Confidence,
                scope = grounding.Scope
            })
            .ToArray();
    }

    private static string ResolveGroundingIntendedUse(
        ProcessTargetGroundingSourceKind sourceKind,
        ProcessTargetGroundingAuthority authority)
    {
        if (authority == ProcessTargetGroundingAuthority.Writable &&
            sourceKind is ProcessTargetGroundingSourceKind.LaunchPlan
                or ProcessTargetGroundingSourceKind.ProjectStructureCurrentRun
                or ProcessTargetGroundingSourceKind.ExplicitStepContract)
        {
            return "current-run-target";
        }

        return sourceKind is ProcessTargetGroundingSourceKind.UpstreamArtifact
            or ProcessTargetGroundingSourceKind.UpstreamArtifactProvenance
                ? "read-context"
                : "grounding-context";
    }

    private static string ResolveGroundingTrustLevel(
        ProcessTargetGroundingSourceKind sourceKind,
        ProcessTargetGroundingAuthority authority)
    {
        return (sourceKind, authority) switch
        {
            (ProcessTargetGroundingSourceKind.LaunchPlan, ProcessTargetGroundingAuthority.Writable) => "trusted-launch",
            (ProcessTargetGroundingSourceKind.ProjectStructureCurrentRun, ProcessTargetGroundingAuthority.Writable) => "trusted-current-run",
            (ProcessTargetGroundingSourceKind.ExplicitStepContract, ProcessTargetGroundingAuthority.Writable) => "trusted-step-contract",
            (ProcessTargetGroundingSourceKind.UpstreamArtifact, _) => "untrusted-read-context",
            (ProcessTargetGroundingSourceKind.UpstreamArtifactProvenance, _) => "untrusted-read-context",
            _ => "text-derived-read-context"
        };
    }

    private static decimal ResolveGroundingConfidence(
        ProcessTargetGroundingSourceKind sourceKind,
        ProcessTargetGroundingAuthority authority)
    {
        return (sourceKind, authority) switch
        {
            (ProcessTargetGroundingSourceKind.ProjectStructureCurrentRun, ProcessTargetGroundingAuthority.Writable) => 0.95m,
            (ProcessTargetGroundingSourceKind.LaunchPlan, ProcessTargetGroundingAuthority.Writable) => 0.9m,
            (ProcessTargetGroundingSourceKind.ExplicitStepContract, ProcessTargetGroundingAuthority.Writable) => 0.85m,
            (ProcessTargetGroundingSourceKind.ProjectStructureContext, _) => 0.7m,
            (ProcessTargetGroundingSourceKind.UpstreamArtifact, _) => 0.55m,
            (ProcessTargetGroundingSourceKind.UpstreamArtifactProvenance, _) => 0.5m,
            _ => 0.4m
        };
    }

    private static string ResolveGroundingScope(string alias)
    {
        var segments = alias.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length <= 3
            ? "root"
            : "descendant";
    }

    private static IReadOnlyList<string> ResolveMutableExternalTargetAliases(
        DispatchCandidate candidate,
        IReadOnlyList<ProcessTargetGroundingRecord> targetGroundings)
    {
        var trustedWritableAliases = PruneAllowedExternalTargetAliasesForCurrentRun(
            targetGroundings
                .Where(grounding => grounding.Authority == ProcessTargetGroundingAuthority.Writable)
                .Select(grounding => grounding.Alias));
        var mutableAliases = trustedWritableAliases
            .Where(alias => !IsNonProductExternalTargetAlias(alias))
            .ToList();
        if (mutableAliases.Count == 0)
        {
            return [];
        }

        var preferredAliases = mutableAliases
            .Where(IsPreferredProductExternalTargetAlias)
            .ToList();
        var candidateAliases = preferredAliases.Count > 0 ? preferredAliases : mutableAliases;
        var currentRunTokens = ResolveCurrentRunExternalTargetAliasTokens(candidate);
        if (currentRunTokens.Count > 0)
        {
            var currentRunAliases = candidateAliases
                .Where(alias => currentRunTokens.Any(token => alias.Contains(token, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (currentRunAliases.Count > 0)
            {
                candidateAliases = currentRunAliases;
            }
        }

        return candidateAliases
            .Where(alias => !candidateAliases.Any(other =>
                !string.Equals(alias, other, StringComparison.OrdinalIgnoreCase) &&
                IsExternalTargetAliasAncestor(alias, other) &&
                !IsLikelyExternalTargetFileAlias(other)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(alias => alias.Length)
            .ToArray();
    }

    private static IReadOnlyList<string> PreferCurrentRunExternalTargetAliases(
        DispatchCandidate candidate,
        IReadOnlyList<string> aliases)
    {
        var currentRunTokens = ResolveCurrentRunExternalTargetAliasTokens(candidate);
        if (currentRunTokens.Count == 0)
        {
            return aliases;
        }

        var currentRunAliases = aliases
            .Where(alias => currentRunTokens.Any(token => alias.Contains(token, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        return currentRunAliases.Length > 0
            ? currentRunAliases
            : aliases;
    }

    private static IReadOnlyList<string> ResolveCurrentRunExternalTargetAliasTokens(DispatchCandidate candidate)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddCurrentRunAliasTokens(tokens, candidate.Run.Name);
        AddCurrentRunAliasTokens(tokens, candidate.Run.TriggerReason);
        return tokens.ToArray();
    }

    private static void AddCurrentRunAliasTokens(HashSet<string> tokens, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        foreach (Match match in Regex.Matches(
                     text,
                     @"(?<!\d)(?<token>\d{8}[-_]\d{4,6})(?!\d)",
                     RegexOptions.CultureInvariant))
        {
            var token = match.Groups["token"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(token))
            {
                tokens.Add(token);
            }
        }

        foreach (Match match in Regex.Matches(
                     text,
                     @"(?i)\b(?<token>[a-z][a-z0-9]+(?:[-_][a-z0-9]+){2,}[-_]\d{8}[-_]\d{4,6})\b",
                     RegexOptions.CultureInvariant))
        {
            var token = match.Groups["token"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(token))
            {
                tokens.Add(token);
            }
        }
    }

    internal static IReadOnlyList<string> PruneAllowedExternalTargetAliasesForCurrentRun(IEnumerable<string> aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);

        var normalizedAliases = aliases
            .Select(NormalizeExternalTargetAlias)
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Where(alias => alias.StartsWith(ExternalTargetAliasRoot + "/", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return normalizedAliases
            .Where(alias => !IsLikelyExternalTargetFileAlias(alias) ||
                            !normalizedAliases.Any(other =>
                                !string.Equals(alias, other, StringComparison.OrdinalIgnoreCase) &&
                                IsExternalTargetAliasAncestor(other, alias)))
            .Where(alias => IsPreferredProductExternalTargetAlias(alias) ||
                            !normalizedAliases.Any(other =>
                !string.Equals(alias, other, StringComparison.OrdinalIgnoreCase) &&
                IsExternalTargetAliasAncestor(alias, other) &&
                !IsLikelyExternalTargetFileAlias(other)))
            .Where(alias => !normalizedAliases.Any(other =>
                !string.Equals(alias, other, StringComparison.OrdinalIgnoreCase) &&
                IsAmbiguousExternalTargetPrefixAlias(alias, other)))
            .OrderByDescending(alias => alias.Length)
            .ToArray();
    }

    private static bool IsAliasCoveredByAny(string alias, IReadOnlyCollection<string> roots)
        => roots.Any(root =>
            string.Equals(alias, root, StringComparison.OrdinalIgnoreCase) ||
            alias.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase));

    private static bool IsPreferredProductExternalTargetAlias(string alias)
    {
        var leaf = alias.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();
        return string.Equals(leaf, "product", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(leaf, "app", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(leaf, "source", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(leaf, "src", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNonProductExternalTargetAlias(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return false;
        }

        var segments = alias.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Any(segment =>
            string.Equals(segment, "project-structure-backup", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "agent-evidence", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "api-snapshots", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "launch-plan", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "observation", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "process-definition", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "process-definition-corrected", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "project-structure-mutations", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsExternalTargetAliasAncestor(string alias, string other)
        => other.StartsWith(alias + "/", StringComparison.OrdinalIgnoreCase);

    private static bool IsAmbiguousExternalTargetPrefixAlias(string alias, string other)
    {
        if (!other.StartsWith(alias, StringComparison.OrdinalIgnoreCase) ||
            other.Length <= alias.Length)
        {
            return false;
        }

        var suffix = other[alias.Length..];
        return suffix[0] != '/' && suffix.Contains('/', StringComparison.Ordinal);
    }

    private static bool IsLikelyExternalTargetFileAlias(string alias)
    {
        var lastSlashIndex = alias.LastIndexOf('/');
        if (lastSlashIndex < 0 || lastSlashIndex >= alias.Length - 1)
        {
            return false;
        }

        var leaf = alias[(lastSlashIndex + 1)..];
        return leaf.StartsWith(".", StringComparison.Ordinal) ||
               leaf.Contains('.');
    }

    private static IReadOnlyList<string> ExtractExternalTargetAliasesFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in WorkspacePathInToolRequestRegex.Matches(text))
        {
            var path = match.Groups["path"].Value;
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            if (path.StartsWith(ExternalTargetAliasRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                var alias = NormalizeExternalTargetAlias(path);
                if (!string.IsNullOrWhiteSpace(alias))
                {
                    aliases.Add(alias);
                }

                continue;
            }
        }

        foreach (var candidatePath in EnumerateAbsoluteExternalPathCandidates(text))
        {
            if (TryMapAbsoluteExternalPathToAlias(candidatePath, out var mappedAlias))
            {
                aliases.Add(mappedAlias);
            }
        }

        return aliases
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .ToArray();
    }

    private static bool TryMapAbsoluteExternalPathToAlias(
        string path,
        out string mappedAlias)
    {
        mappedAlias = string.Empty;
        if (!TryNormalizeAbsoluteExternalPathCandidate(path, out var normalizedPath))
        {
            return false;
        }

        var driveLetter = char.ToUpperInvariant(normalizedPath[0]);
        var remainder = normalizedPath.Length == 3
            ? string.Empty
            : CollapseExternalTargetAliasSeparators(normalizedPath[3..]).Trim('/');
        mappedAlias = string.IsNullOrWhiteSpace(remainder)
            ? $"{ExternalTargetAliasRoot}/{driveLetter}"
            : $"{ExternalTargetAliasRoot}/{driveLetter}/{remainder}";
        return true;
    }

    private static string NormalizeExternalTargetAlias(string? alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return string.Empty;
        }

        var normalizedAlias = alias
            .Replace('\\', '/')
            .Trim()
            .TrimEnd('/', '.', ',', ';', ':', ')', ']', '}');
        normalizedAlias = StripEscapedLineBreakPathAnnotations(normalizedAlias)
            .TrimEnd('/', '.', ',', ';', ':', ')', ']', '}');
        normalizedAlias = StripInlinePathAnnotations(normalizedAlias)
            .TrimEnd('/', '.', ',', ';', ':', ')', ']', '}');

        return CollapseExternalTargetAliasSeparators(normalizedAlias);
    }

    private static string CollapseExternalTargetAliasSeparators(string value)
    {
        return Regex.Replace(
            value.Replace('\\', '/'),
            "/{2,}",
            "/",
            RegexOptions.CultureInvariant);
    }
}
