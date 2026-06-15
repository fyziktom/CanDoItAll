using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Builder;

public sealed partial class ProcessInstancePlanCompiler
{
    private const string BuilderBindingVersion = "builder/1.0";

    public ProcessPlanCompileResult Compile(ProcessInstancePlanCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var planId = ProcessInstancePlanId.New();
        return Compile(
            request,
            [],
            planId,
            null,
            null,
            planId,
            0,
            request.MaximumSubprocessDepth);
    }

    private ProcessPlanCompileResult Compile(
        ProcessInstancePlanCompileRequest request,
        IReadOnlyList<DefinitionIdentity> ancestorDefinitions,
        ProcessInstancePlanId rootPlanId,
        ProcessInstancePlanId? parentPlanId,
        ProcessStepInstanceId? parentStepId,
        ProcessInstancePlanId planId,
        int hierarchyDepth,
        int maximumSubprocessDepth)
    {
        var diagnostics = new List<ProcessBuildDiagnostic>();
        ValidateRequiredValues(request, diagnostics);
        ValidateSubprocessDepth(hierarchyDepth, maximumSubprocessDepth, diagnostics);
        ValidateDefinitionCycle(request, ancestorDefinitions, diagnostics);
        var migrationIds = ValidateMigrations(request, diagnostics);
        var resolvedComponents = ResolveTemplateComponents(request, diagnostics);
        var appliedOverridePointers = ValidateLocalOverrides(request, diagnostics);
        ValidateDefinition(request, diagnostics);
        ValidateInitialArtifacts(request, diagnostics);
        var capabilityMatch = SelectDriverStack(request, diagnostics);

        if (diagnostics.Any(diagnostic => diagnostic.Severity == ProcessBuildDiagnosticSeverity.Error))
        {
            return ProcessPlanCompileResult.Failure(diagnostics);
        }

        var selectedDrivers = capabilityMatch.OrderedDrivers;
        var strategyIndex = BuildStrategyIndex(selectedDrivers, diagnostics);
        var stepPlans = BuildStepPlans(request, strategyIndex, diagnostics);
        var managerPlan = BuildManagerPlan(request, strategyIndex, diagnostics);

        if (diagnostics.Any(diagnostic => diagnostic.Severity == ProcessBuildDiagnosticSeverity.Error))
        {
            return ProcessPlanCompileResult.Failure(diagnostics);
        }

        var nextAncestors = ancestorDefinitions
            .Append(DefinitionIdentity.From(request.Definition))
            .ToArray();
        var subprocessRefs = BuildSubprocessRefs(
            request,
            nextAncestors,
            rootPlanId,
            planId,
            stepPlans,
            hierarchyDepth,
            maximumSubprocessDepth,
            diagnostics);

        if (diagnostics.Any(diagnostic => diagnostic.Severity == ProcessBuildDiagnosticSeverity.Error))
        {
            return ProcessPlanCompileResult.Failure(diagnostics);
        }

        var plan = new ProcessInstancePlan(
            new ProcessInstancePlanHeader(
                planId,
                rootPlanId,
                parentPlanId,
                parentStepId,
                request.TargetSchemaVersion,
                DateTimeOffset.UtcNow,
                hierarchyDepth),
            new ResolvedProcessDefinitionSnapshot(
                request.Definition.DefinitionId,
                request.Definition.VersionId,
                request.DefinitionContentHash,
                request.SourceSchemaVersion,
                request.TargetSchemaVersion,
                migrationIds,
                resolvedComponents,
                appliedOverridePointers),
            BuildDriverStack(selectedDrivers),
            BuildStrategyBindingSet(stepPlans, managerPlan),
            stepPlans,
            BuildArtifactPlan(request),
            BuildBranchRouteTable(request),
            subprocessRefs,
            managerPlan,
            BuildBudgetPlan(request),
            new MonitoringPlan(request.Monitoring.Enabled, request.Monitoring.ProjectionConfigHash),
            new SecurityPlan(
                request.Security.GovernancePolicyHash,
                request.Security.RequiredApprovalKeys.Order(StringComparer.Ordinal).ToArray()),
            string.Empty);

        var hashedPlan = plan with
        {
            PlanHash = ProcessPlanHasher.Compute(plan)
        };

        return ProcessPlanCompileResult.Success(hashedPlan);
    }

    private static ProcessBuildDiagnostic Error(string code, string message)
    {
        return new ProcessBuildDiagnostic(code, message);
    }

    private sealed record ResolvedStrategyDescriptor(
        ProcessDriverDescriptor Driver,
        ProcessStrategyDescriptor Strategy);

    private sealed record DefinitionIdentity(
        ProcessDefinitionId DefinitionId,
        ProcessDefinitionVersionId VersionId)
    {
        public static DefinitionIdentity From(ProcessDefinitionKernel definition)
        {
            return new DefinitionIdentity(definition.DefinitionId, definition.VersionId);
        }
    }
}
