using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessLaunchApplicationService(
    ProcessTemplatePackLoader templatePackLoader,
    IProcessProjectionClock clock,
    IProcessLaunchDriverCatalogProvider driverCatalogProvider,
    IProcessLaunchExecutorResolver executorResolver,
    IProcessInstancePlanStore planStore,
    IProcessRuntimeUnitOfWork unitOfWork,
    IProcessRuntimeStateStore stateStore,
    IProcessRuntimeStepAssignmentStore assignmentStore,
    IProcessLaunchArtifactInitializer artifactInitializer,
    ProcessRuntimeDispatchApplicationService dispatchService,
    ProcessRuntimeProjectionCatchupService projectionCatchupService)
{
    private const string ExecuteExternalActionOperationName = "ExecuteExternalAction";
    private const string SubprocessLaunchToolName = "project_structure_process_subprocess_launch";
    private const string SubprocessStepKind = "Subprocess";

    public async Task<ProcessLaunchResult> PreviewAsync(
        ProcessLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var nowUtc = NormalizeUtc(clock.GetUtcNow());
        var preparation = await PrepareLaunchAsync(request, nowUtc, cancellationToken).ConfigureAwait(false);
        if (preparation.EarlyResult is not null)
        {
            return preparation.EarlyResult;
        }

        var plan = preparation.Plan ?? throw new InvalidOperationException("Prepared process launch did not include an instance plan.");
        var launchPlan = preparation.LaunchPlan ?? throw new InvalidOperationException("Prepared process launch did not include a launch plan view.");
        var stage = preparation.BlockingFindings.Count > 0
            ? ProcessLaunchStage.Blocked
            : ProcessLaunchStage.Planned;

        return new ProcessLaunchResult(
            plan.Definition.DefinitionId,
            plan.Header.PlanId,
            RunId: null,
            stage,
            Route: string.Empty,
            launchPlan,
            launchPlan.ReadinessFindings
                .Where(finding => finding.Severity != ProcessLaunchReadinessSeverity.Info)
                .Select(finding => finding.Message)
                .ToArray());
    }

    public async Task<ProcessLaunchResult> LaunchAsync(
        ProcessLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var nowUtc = NormalizeUtc(clock.GetUtcNow());
        var preparation = await PrepareLaunchAsync(request, nowUtc, cancellationToken).ConfigureAwait(false);
        if (preparation.EarlyResult is not null)
        {
            return preparation.EarlyResult;
        }

        var selected = preparation.Selected ?? throw new InvalidOperationException("Prepared process launch did not include a template selection.");
        var plan = preparation.Plan ?? throw new InvalidOperationException("Prepared process launch did not include an instance plan.");
        var assignments = preparation.Assignments;
        var launchPlan = preparation.LaunchPlan ?? throw new InvalidOperationException("Prepared process launch did not include a launch plan view.");

        await planStore.PersistAsync(plan, cancellationToken).ConfigureAwait(false);

        var initialState = BuildInitialState(
            plan,
            selected.Definition,
            assignments,
            request.RootRunIdOverride,
            nowUtc);
        var createContext = CreateContext(request.RequestedBy, nowUtc);
        var createMutation = CreateAppliedMutation(
            initialState,
            createContext,
            ProcessRuntimeEventTypes.ProcessRunCreated,
            plan.PlanHash);
        var createCommit = await unitOfWork.CommitAsync(
            new ProcessRuntimeCommitRequest(createContext.CommandId, initialState, createMutation),
            cancellationToken).ConfigureAwait(false);
        if (!createCommit.Succeeded)
        {
            return new ProcessLaunchResult(
                plan.Definition.DefinitionId,
                plan.Header.PlanId,
                initialState.RunId,
                ProcessLaunchStage.Failed,
                BuildRunRoute(initialState.RunId, request.ProjectId),
                launchPlan,
                createCommit.Diagnostics.Select(diagnostic => diagnostic.Message).ToArray());
        }

        await assignmentStore.SaveAsync(assignments, cancellationToken).ConfigureAwait(false);
        var artifactRoot = BuildManagedProcessArtifactRoot(initialState.RunId);
        try
        {
            await artifactInitializer.InitializeAsync(
                new ProcessLaunchArtifactInitializationRequest(
                    initialState.RunId,
                    plan.Definition.DefinitionId,
                    plan.Header.PlanId,
                    selected.Definition.Key,
                    request.ProjectId,
                    artifactRoot),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new ProcessLaunchResult(
                plan.Definition.DefinitionId,
                plan.Header.PlanId,
                initialState.RunId,
                ProcessLaunchStage.Failed,
                BuildRunRoute(initialState.RunId, request.ProjectId),
                launchPlan,
                [$"Failed to initialize managed process artifact root '{artifactRoot}': {exception.Message}"]);
        }

        var engine = new ProcessRuntimeEngine(unitOfWork);
        var activeCommit = await engine.ActivateAsync(
            createCommit.State,
            CreateContext(request.RequestedBy, nowUtc.AddMilliseconds(1)),
            cancellationToken).ConfigureAwait(false);
        var activeState = activeCommit.State;
        if (activeCommit.Succeeded)
        {
            var scheduled = await engine.ScheduleReadyAsync(
                activeCommit.State,
                CreateContext(request.RequestedBy, nowUtc.AddMilliseconds(2)),
                cancellationToken).ConfigureAwait(false);
            activeState = scheduled.State;
        }

        await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);

        var stage = ProcessLaunchStage.Running;
        if (request.Execute)
        {
            var dispatch = await dispatchService.ExecuteReadyAsync(
                activeState.RunId,
                request.RequestedBy,
                cancellationToken).ConfigureAwait(false);
            stage = dispatch.Stage;
        }

        return new ProcessLaunchResult(
            plan.Definition.DefinitionId,
            plan.Header.PlanId,
            activeState.RunId,
            stage,
            BuildRunRoute(activeState.RunId, request.ProjectId),
            launchPlan,
            launchPlan.ReadinessFindings
                .Where(finding => finding.Severity != ProcessLaunchReadinessSeverity.Info)
                .Select(finding => finding.Message)
                .ToArray());
    }

    public async Task<ProcessLaunchResult?> FindExistingLaunchAsync(
        ProcessExistingLaunchLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.DefinitionKey))
        {
            throw new ArgumentException("A process definition key is required for existing launch lookup.", nameof(request));
        }

        if (request.RequiredLaunchVariables.Count == 0)
        {
            throw new ArgumentException("At least one launch variable is required for existing launch lookup.", nameof(request));
        }

        var selected = ResolveDefinition(new ProcessLaunchRequest(
            request.DefinitionKey,
            ProcessDefinitionId: null,
            request.LiveRunProfileKey,
            request.ProjectId,
            ProjectNodeId: null,
            RequestedBy: "existing-launch-lookup",
            Variables: new Dictionary<string, string>(StringComparer.Ordinal),
            RunReadiness: false,
            Execute: false));
        var expectedDefinitionId = ProcessTemplateKernelBuilder.CreateDefinitionId(selected.Definition.Key);
        var matchingAssignments = await assignmentStore
            .FindByLaunchVariablesAsync(request.RequiredLaunchVariables, cancellationToken)
            .ConfigureAwait(false);

        foreach (var runGroup in matchingAssignments
            .GroupBy(assignment => assignment.RunId)
            .OrderByDescending(group => group.Max(assignment => assignment.CreatedAtUtc)))
        {
            var state = await stateStore.LoadAsync(runGroup.Key, cancellationToken).ConfigureAwait(false);
            if (state is null)
            {
                throw new InvalidOperationException($"Existing launch lookup found assignments for missing process run '{runGroup.Key}'.");
            }

            var plan = await planStore.LoadAsync(state.PlanId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Existing launch lookup found process run '{state.RunId}' with missing plan '{state.PlanId}'.");
            if (plan.Definition.DefinitionId != expectedDefinitionId)
            {
                continue;
            }

            var assignments = await assignmentStore.LoadByRunAsync(state.RunId, cancellationToken).ConfigureAwait(false);
            var launchPlan = CreateLaunchPlanView(
                selected,
                plan,
                assignments,
                [new ProcessLaunchReadinessFinding(
                    ProcessLaunchReadinessSeverity.Info,
                    "process.launch.existing_reused",
                    $"Reused existing process run '{state.RunId.Value:D}' for matching launch variables.")]);

            return new ProcessLaunchResult(
                plan.Definition.DefinitionId,
                plan.Header.PlanId,
                state.RunId,
                MapLaunchStage(state.Status),
                BuildRunRoute(state.RunId, request.ProjectId),
                launchPlan,
                [$"Reused existing process run '{state.RunId.Value:D}' for matching launch variables."]);
        }

        return null;
    }

    private async Task<ProcessLaunchPreparation> PrepareLaunchAsync(
        ProcessLaunchRequest request,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var selected = ResolveDefinition(request);
        var driverCatalog = await driverCatalogProvider.LoadAsync(cancellationToken).ConfigureAwait(false);
        var kernelBuild = ProcessTemplateKernelBuilder.Build(
            selected.Definition,
            selected.Pack.Manifest.Version,
            driverCatalog.StepExecutionStrategyId);
        var compileRequest = CreateCompileRequest(
            kernelBuild,
            selected,
            driverCatalog);
        var compileResult = new ProcessInstancePlanCompiler().Compile(compileRequest);
        if (!compileResult.Succeeded || compileResult.Plan is null)
        {
            var findings = compileResult.Diagnostics
                .Select(diagnostic => new ProcessLaunchReadinessFinding(
                    ProcessLaunchReadinessSeverity.Error,
                    diagnostic.Code,
                    diagnostic.Message))
                .ToArray();
            var blockedPlanId = ProcessInstancePlanId.New();
            return new ProcessLaunchPreparation(
                new ProcessLaunchResult(
                    kernelBuild.Definition.DefinitionId,
                    blockedPlanId,
                    RunId: null,
                    ProcessLaunchStage.Blocked,
                    Route: string.Empty,
                    new ProcessLaunchPlanView(
                        blockedPlanId,
                        kernelBuild.Definition.DefinitionId,
                        kernelBuild.Definition.VersionId,
                        selected.Definition.Key,
                        selected.Definition.DisplayName,
                        selected.Definition.Summary,
                        selected.LiveRunProfile?.Key,
                        PlanHash: string.Empty,
                        Steps: [],
                        findings),
                    findings.Select(finding => finding.Message).ToArray()),
                Selected: null,
                Plan: null,
                Assignments: [],
                LaunchPlan: null,
                BlockingFindings: findings);
        }

        var plan = compileResult.Plan;
        var executorResolution = await executorResolver.ResolveAsync(
            new ProcessLaunchExecutorResolutionRequest(
                selected.Definition,
                plan,
                selected.LiveRunProfile,
                request.Variables),
            cancellationToken).ConfigureAwait(false);
        var assignments = BuildAssignments(
            request,
            selected,
            kernelBuild,
            plan,
            executorResolution,
            nowUtc);
        var launchPlan = CreateLaunchPlanView(
            selected,
            plan,
            assignments,
            executorResolution.Findings);
        var blockingFindings = executorResolution.Findings
            .Where(finding => finding.Severity == ProcessLaunchReadinessSeverity.Error)
            .ToArray();
        if (request.RunReadiness && blockingFindings.Length > 0)
        {
            return new ProcessLaunchPreparation(
                new ProcessLaunchResult(
                    plan.Definition.DefinitionId,
                    plan.Header.PlanId,
                    RunId: null,
                    ProcessLaunchStage.Blocked,
                    Route: string.Empty,
                    launchPlan,
                    blockingFindings.Select(finding => finding.Message).ToArray()),
                Selected: null,
                Plan: null,
                Assignments: [],
                LaunchPlan: null,
                BlockingFindings: blockingFindings);
        }

        return new ProcessLaunchPreparation(
            EarlyResult: null,
            Selected: selected,
            Plan: plan,
            Assignments: assignments,
            LaunchPlan: launchPlan,
            BlockingFindings: blockingFindings);
    }

    private ProcessTemplateSelection ResolveDefinition(ProcessLaunchRequest request)
    {
        var pack = templatePackLoader.Load();
        var liveProfiles = templatePackLoader.LoadLiveRunProfiles();
        var definitionKey = request.DefinitionKey;
        if (string.IsNullOrWhiteSpace(definitionKey) && request.ProcessDefinitionId is { } definitionId)
        {
            definitionKey = pack.Definitions
                .FirstOrDefault(definition => ProcessTemplateKernelBuilder.CreateDefinitionId(definition.Key) == definitionId)
                ?.Key;
        }

        var liveProfile = ResolveLiveRunProfile(request, liveProfiles, definitionKey);
        if (string.IsNullOrWhiteSpace(definitionKey))
        {
            definitionKey = liveProfile?.ProcessTemplateKey;
        }

        if (string.IsNullOrWhiteSpace(definitionKey))
        {
            definitionKey = pack.Definitions.FirstOrDefault()?.Key;
        }

        if (string.IsNullOrWhiteSpace(definitionKey))
        {
            throw new InvalidOperationException("No process template definitions are available for launch.");
        }

        var definition = templatePackLoader.LoadDefinition(definitionKey);
        return new ProcessTemplateSelection(pack, definition, liveProfile);
    }

    private static ProcessTemplateLiveRunProfileDocument? ResolveLiveRunProfile(
        ProcessLaunchRequest request,
        IReadOnlyList<ProcessTemplateLiveRunProfileDocument> liveProfiles,
        string? definitionKey)
    {
        if (!string.IsNullOrWhiteSpace(request.LiveRunProfileKey))
        {
            var profile = liveProfiles.FirstOrDefault(profile =>
                string.Equals(profile.Key, request.LiveRunProfileKey.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Live-run profile '{request.LiveRunProfileKey}' is not available.");

            if (!string.IsNullOrWhiteSpace(definitionKey) &&
                !string.Equals(profile.ProcessTemplateKey, definitionKey.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Live-run profile '{profile.Key}' targets process template '{profile.ProcessTemplateKey}', not '{definitionKey.Trim()}'.");
            }

            return profile;
        }

        if (!string.IsNullOrWhiteSpace(definitionKey))
        {
            return liveProfiles.FirstOrDefault(profile =>
                string.Equals(profile.ProcessTemplateKey, definitionKey.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        return liveProfiles.FirstOrDefault();
    }

    private static ProcessInstancePlanCompileRequest CreateCompileRequest(
        ProcessTemplateKernelBuildResult kernelBuild,
        ProcessTemplateSelection selection,
        ProcessLaunchDriverCatalog driverCatalog)
    {
        var templateComponent = new ProcessTemplateComponentReference(
            new TemplateComponentId(ProcessTemplateKernelBuilder.CreateDefinitionId(selection.Definition.Key).Value),
            selection.Definition.Key,
            selection.Pack.Manifest.Version,
            kernelBuild.DefinitionContentHash);

        return new ProcessInstancePlanCompileRequest(
            new ProcessPlanCompileSource(
                SourceSchemaVersion: "runtime/1.0",
                TargetSchemaVersion: "runtime/1.0",
                kernelBuild.Definition,
                kernelBuild.DefinitionContentHash,
                driverCatalog.DriverCatalog,
                new ProcessCapabilityRequest(
                    driverCatalog.RequiredCapabilityTags,
                    driverCatalog.RequiredCapabilityTags,
                    new HashSet<CapabilityTag>()),
                [templateComponent],
                [templateComponent],
                [],
                new HashSet<string>(StringComparer.Ordinal),
                [],
                new ProcessManagerPlanRequest(
                    ManagerStrategyId: null,
                    RecoveryStrategyIds: [],
                    ResupplyStrategyIds: [],
                    PolicyHash: ComputeHash(selection.Definition.GovernancePolicySummary)),
                new ProcessMonitoringPlanRequest(
                    Enabled: true,
                    ProjectionConfigHash: ComputeHash("runtime-projections:v1")),
                new ProcessSecurityPlanRequest(
                    GovernancePolicyHash: ComputeHash(selection.Definition.GovernancePolicySummary),
                    RequiredApprovalKeys: [])),
            Subprocesses: []);
    }

    private static IReadOnlyList<ProcessRuntimeStepAssignment> BuildAssignments(
        ProcessLaunchRequest request,
        ProcessTemplateSelection selection,
        ProcessTemplateKernelBuildResult kernelBuild,
        ProcessInstancePlan plan,
        ProcessLaunchExecutorResolution executorResolution,
        DateTimeOffset nowUtc)
    {
        var executorByStepKey = executorResolution.Bindings.ToDictionary(
            binding => binding.StepKey,
            StringComparer.OrdinalIgnoreCase);
        var overrideByStepKey = request.ExecutorOverrides
            .Where(item => !string.IsNullOrWhiteSpace(item.StepKey))
            .GroupBy(item => item.StepKey.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last(),
                StringComparer.OrdinalIgnoreCase);
        var stepsByKey = selection.Definition.Steps.ToDictionary(step => step.Key, StringComparer.OrdinalIgnoreCase);
        var launchVariables = NormalizeLaunchVariables(request.Variables);
        var runId = ProcessRunId.New();
        var assignments = new List<ProcessRuntimeStepAssignment>();

        foreach (var planStep in plan.Steps.Where(step => step.IsExecutable))
        {
            if (!stepsByKey.TryGetValue(planStep.StepKey, out var templateStep))
            {
                continue;
            }

            executorByStepKey.TryGetValue(planStep.StepKey, out var binding);
            if (overrideByStepKey.TryGetValue(planStep.StepKey, out var executorOverride))
            {
                binding = new ProcessLaunchExecutorBinding(
                    planStep.StepKey,
                    NormalizeOptional(executorOverride.RoleKey, binding?.RoleKey ?? ResolvePrimaryRoleKey(templateStep)),
                    NormalizeOptional(executorOverride.ExecutorKind, binding?.ExecutorKind ?? string.Empty),
                    NormalizeOptional(executorOverride.ExecutorId, binding?.ExecutorId ?? string.Empty),
                    NormalizeOptional(executorOverride.ExecutorDisplayName, binding?.ExecutorDisplayName ?? string.Empty),
                    ComputeHash($"override:{planStep.StepKey}:{executorOverride.ExecutorKind}:{executorOverride.ExecutorId}"),
                    NormalizeOptional(executorOverride.AssignmentReason, "Executor selected during launch review."));
            }

            var requiredSlots = ResolveRequiredSlots(templateStep, kernelBuild);
            var producedSlots = ResolveProducedSlots(templateStep, kernelBuild);
            assignments.Add(new ProcessRuntimeStepAssignment(
                runId,
                plan.Header.PlanId,
                planStep.StepInstanceId,
                planStep.StepKey,
                binding?.RoleKey ?? ResolvePrimaryRoleKey(templateStep),
                binding?.ExecutorKind ?? string.Empty,
                binding?.ExecutorId ?? string.Empty,
                binding?.ExecutorDisplayName ?? string.Empty,
                BuildStepPrompt(
                    request,
                    selection,
                    templateStep,
                    binding,
                    requiredSlots,
                    producedSlots,
                    kernelBuild.ArtifactSlotByStepExpectation,
                    runId),
                binding?.ReadinessHash ?? ComputeHash($"missing:{planStep.StepKey}"),
                binding?.AssignmentReason ?? "No executor binding was resolved.",
                producedSlots,
                requiredSlots,
                NormalizeAllowedOperations(templateStep.AllowedOperations),
                NormalizeOperationTargetScope(templateStep.OperationTargetScope),
                launchVariables,
                ResolveBranchGate(templateStep),
                nowUtc));
        }

        if (assignments.Count == 0)
        {
            return assignments;
        }

        return assignments;
    }

    private static ProcessRuntimeStateSnapshot BuildInitialState(
        ProcessInstancePlan plan,
        ProcessTemplateDefinitionDocument definition,
        IReadOnlyList<ProcessRuntimeStepAssignment> assignments,
        ProcessRunId? rootRunIdOverride,
        DateTimeOffset nowUtc)
    {
        var assignmentByStep = assignments.ToDictionary(assignment => assignment.StepInstanceId);
        var stepKeyToInstanceId = plan.Steps.ToDictionary(step => step.StepKey, step => step.StepInstanceId, StringComparer.OrdinalIgnoreCase);
        var templateStepByKey = definition.Steps.ToDictionary(step => step.Key, StringComparer.OrdinalIgnoreCase);
        var runtimeSteps = new List<ProcessRuntimeStepState>(plan.Steps.Count);
        var runId = assignments.FirstOrDefault()?.RunId ?? ProcessRunId.New();

        foreach (var planStep in plan.Steps)
        {
            templateStepByKey.TryGetValue(planStep.StepKey, out var templateStep);
            assignmentByStep.TryGetValue(planStep.StepInstanceId, out var assignment);
            var dependencies = templateStep is null
                ? new HashSet<ProcessStepInstanceId>()
                : ResolveDependencyStepIds(templateStep, stepKeyToInstanceId);
            var status = assignment?.BranchGate is null
                ? ProcessRuntimeStepStatus.Pending
                : ProcessRuntimeStepStatus.Blocked;

            runtimeSteps.Add(new ProcessRuntimeStepState(
                planStep.StepInstanceId,
                planStep.StepDefinitionId,
                status,
                planStep.IsExecutable,
                AttemptNumber: 0,
                dependencies,
                assignment?.RequiredArtifactSlotIds.ToHashSet() ?? [],
                ActiveClaimToken: null,
                CompletedResultKey: null));
        }

        return new ProcessRuntimeStateSnapshot(
            rootRunIdOverride ?? runId,
            runId,
            plan.Header.PlanId,
            plan.PlanHash,
            ProcessRuntimeStatus.Created,
            runtimeSteps,
            Claims: [],
            AppliedResults: [],
            plan.ArtifactPlan.InitialLedgerEntries.Select(entry => entry.SlotId).ToHashSet(),
            nowUtc);
    }

    private static ProcessLaunchPlanView CreateLaunchPlanView(
        ProcessTemplateSelection selection,
        ProcessInstancePlan plan,
        IReadOnlyList<ProcessRuntimeStepAssignment> assignments,
        IReadOnlyList<ProcessLaunchReadinessFinding> findings)
    {
        var assignmentByStepKey = assignments.ToDictionary(assignment => assignment.StepKey, StringComparer.OrdinalIgnoreCase);
        var templateStepByKey = selection.Definition.Steps.ToDictionary(step => step.Key, StringComparer.OrdinalIgnoreCase);
        var stepViews = plan.Steps
            .Select(step =>
            {
                assignmentByStepKey.TryGetValue(step.StepKey, out var assignment);
                templateStepByKey.TryGetValue(step.StepKey, out var templateStep);
                var blockingFinding = findings.FirstOrDefault(finding =>
                    finding.Severity == ProcessLaunchReadinessSeverity.Error &&
                    string.Equals(finding.StepKey, step.StepKey, StringComparison.OrdinalIgnoreCase));
                return new ProcessLaunchStepView(
                    step.StepInstanceId,
                    step.StepKey,
                    templateStep?.Title ?? step.StepKey,
                    assignment?.RoleKey ?? string.Empty,
                    assignment?.ExecutorKind ?? string.Empty,
                    assignment?.ExecutorId ?? string.Empty,
                    assignment?.ExecutorDisplayName ?? string.Empty,
                    blockingFinding is not null,
                    blockingFinding?.Message,
                    assignment?.BranchGate);
            })
            .ToArray();

        return new ProcessLaunchPlanView(
            plan.Header.PlanId,
            plan.Definition.DefinitionId,
            plan.Definition.VersionId,
            selection.Definition.Key,
            selection.Definition.DisplayName,
            selection.Definition.Summary,
            selection.LiveRunProfile?.Key,
            plan.PlanHash,
            stepViews,
            findings);
    }

    private static IReadOnlyList<ArtifactSlotId> ResolveRequiredSlots(
        ProcessTemplateDefinitionStepDocument step,
        ProcessTemplateKernelBuildResult kernelBuild)
    {
        var slots = new List<ArtifactSlotId>();
        foreach (var input in step.ArtifactInputs)
        {
            if (kernelBuild.ArtifactSlotByStepExpectation.TryGetValue((input.SourceStepKey, input.ArtifactExpectationKey), out var slotId))
            {
                slots.Add(slotId);
            }
        }

        return slots;
    }

    private static IReadOnlyList<ArtifactSlotId> ResolveProducedSlots(
        ProcessTemplateDefinitionStepDocument step,
        ProcessTemplateKernelBuildResult kernelBuild)
    {
        var slots = new List<ArtifactSlotId>();
        foreach (var expectation in step.ArtifactExpectations)
        {
            if (kernelBuild.ArtifactSlotByStepExpectation.TryGetValue((step.Key, expectation.Key), out var slotId))
            {
                slots.Add(slotId);
            }
        }

        return slots;
    }

    private static HashSet<ProcessStepInstanceId> ResolveDependencyStepIds(
        ProcessTemplateDefinitionStepDocument templateStep,
        IReadOnlyDictionary<string, ProcessStepInstanceId> stepKeyToInstanceId)
    {
        var dependencies = new HashSet<ProcessStepInstanceId>();
        foreach (var dependency in ProcessTemplateKernelBuilder.EnumerateDependencies(templateStep)
                     .Where(dependency => string.IsNullOrWhiteSpace(dependency.BranchOutcomeKey)))
        {
            if (stepKeyToInstanceId.TryGetValue(dependency.StepKey, out var stepInstanceId))
            {
                dependencies.Add(stepInstanceId);
            }
        }

        return dependencies;
    }

    private static ProcessRuntimeBranchGate? ResolveBranchGate(ProcessTemplateDefinitionStepDocument step)
    {
        var branchDependency = ProcessTemplateKernelBuilder.EnumerateDependencies(step)
            .FirstOrDefault(dependency => !string.IsNullOrWhiteSpace(dependency.BranchOutcomeKey));
        return string.IsNullOrWhiteSpace(branchDependency?.StepKey)
            ? null
            : new ProcessRuntimeBranchGate(branchDependency.StepKey, branchDependency.BranchOutcomeKey);
    }

    private static string ResolvePrimaryRoleKey(ProcessTemplateDefinitionStepDocument step)
    {
        return step.RoleAssignments
            .OrderBy(assignment => assignment.FallbackOrder)
            .Select(assignment => assignment.RoleKey)
            .FirstOrDefault(roleKey => !string.IsNullOrWhiteSpace(roleKey)) ?? string.Empty;
    }

    private static IReadOnlyList<string> NormalizeAllowedOperations(IEnumerable<string> operations)
    {
        return operations
            .Where(operation => !string.IsNullOrWhiteSpace(operation))
            .Select(operation => operation.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(operation => operation, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeOperationTargetScope(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    private static string NormalizeOptional(string? value, string defaultValue)
        => string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();

    private static IReadOnlyDictionary<string, string> NormalizeLaunchVariables(
        IReadOnlyDictionary<string, string> variables)
    {
        if (variables.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in variables.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            normalized[key.Trim()] = value?.Trim() ?? string.Empty;
        }

        return normalized;
    }

    private static string BuildStepPrompt(
        ProcessLaunchRequest request,
        ProcessTemplateSelection selection,
        ProcessTemplateDefinitionStepDocument step,
        ProcessLaunchExecutorBinding? binding,
        IReadOnlyList<ArtifactSlotId> requiredSlots,
        IReadOnlyList<ArtifactSlotId> producedSlots,
        IReadOnlyDictionary<(string StepKey, string ExpectationKey), ArtifactSlotId> artifactSlotByStepExpectation,
        ProcessRunId runId)
    {
        var variables = request.Variables.Count == 0
            ? "No launch variables were supplied."
            : string.Join(Environment.NewLine, request.Variables.OrderBy(item => item.Key).Select(item => $"- {item.Key}: {item.Value}"));
        var branchOutcomes = step.BranchOutcomes.Count == 0
            ? "No branch outcomes."
            : string.Join(
                Environment.NewLine,
                step.BranchOutcomes.Select(outcome => $"- {outcome.Key}: {outcome.Title} - {outcome.Description}"));
        var requiredArtifacts = BuildRequiredArtifactContext(
            selection.Definition,
            step,
            requiredSlots,
            artifactSlotByStepExpectation,
            runId);
        var producedArtifacts = BuildProducedArtifactContext(
            step,
            producedSlots,
            artifactSlotByStepExpectation,
            runId);
        var stepKind = string.IsNullOrWhiteSpace(step.StepKind)
            ? "Work"
            : step.StepKind.Trim();
        var subprocessGuidance = BuildSubprocessGuidance(step);

        return $"""
        You are executing a CanDoItAll process step.

        Process: {selection.Definition.DisplayName}
        Step key: {step.Key}
        Step title: {step.Title}
        Step kind: {stepKind}
        Role key: {binding?.RoleKey ?? ResolvePrimaryRoleKey(step)}
        Requested by: {request.RequestedBy}
        Project id: {request.ProjectId?.ToString("D") ?? "not scoped"}
        Project node id: {request.ProjectNodeId ?? "not scoped"}
        Process run id: {runId}
        Managed process artifact root: {BuildManagedProcessArtifactRoot(runId)}
        Evidence write rule: write process step summaries, proof, screenshots, logs, and handoff notes under the managed process artifact root or a child path. Include the written managed artifact paths in evidenceRefs. Do not write evidence under output/ unless this step is explicitly mutating a managed product output path.

        Launch variables:
        {variables}

        Step instructions:
        {step.Notes}

        Input contract:
        {step.InputContractSummary}

        Output contract:
        {step.OutputContractSummary}

        Evidence contract:
        {step.EvidenceContractSummary}

        Allowed operations:
        {string.Join(", ", step.AllowedOperations)}

        Operation target scope:
        {NormalizeOperationTargetScope(step.OperationTargetScope)}

        Subprocess mapping:
        {subprocessGuidance}

        Required upstream artifact slots:
        {requiredArtifacts}

        Produced artifact slots:
        {producedArtifacts}

        Available branch outcomes:
        {branchOutcomes}

        Return only JSON matching the process_step_outcome_result structured output contract.
        Use Status Completed when the step is done, Blocked when required input or tools are missing, Failed for unrecoverable execution failure, or WaitingApproval when a human approval is required.
        If branch outcomes are listed, set BranchOutcomeKey to exactly one listed outcome key.
        """;
    }

    private static string BuildRequiredArtifactContext(
        ProcessTemplateDefinitionDocument definition,
        ProcessTemplateDefinitionStepDocument step,
        IReadOnlyList<ArtifactSlotId> requiredSlots,
        IReadOnlyDictionary<(string StepKey, string ExpectationKey), ArtifactSlotId> artifactSlotByStepExpectation,
        ProcessRunId runId)
    {
        if (requiredSlots.Count == 0)
        {
            return "No required upstream artifact slots.";
        }

        var requiredSlotSet = requiredSlots.ToHashSet();
        var describedSlots = new HashSet<ArtifactSlotId>();
        var stepsByKey = definition.Steps.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        var lines = new List<string>();

        foreach (var input in step.ArtifactInputs)
        {
            var sourceStepKey = NormalizeOptional(input.SourceStepKey, string.Empty);
            var expectationKey = NormalizeOptional(input.ArtifactExpectationKey, string.Empty);
            if (string.IsNullOrWhiteSpace(sourceStepKey) ||
                string.IsNullOrWhiteSpace(expectationKey) ||
                !artifactSlotByStepExpectation.TryGetValue((sourceStepKey, expectationKey), out var slotId) ||
                !requiredSlotSet.Contains(slotId) ||
                !stepsByKey.TryGetValue(sourceStepKey, out var sourceStep))
            {
                continue;
            }

            var expectation = sourceStep.ArtifactExpectations.FirstOrDefault(item =>
                string.Equals(item.Key, expectationKey, StringComparison.OrdinalIgnoreCase));
            lines.Add(FormatRequiredArtifactContext(runId, slotId, sourceStep, expectationKey, expectation));
            describedSlots.Add(slotId);
        }

        foreach (var slotId in requiredSlots.Where(slotId => !describedSlots.Contains(slotId)))
        {
            lines.Add($"""
            - Slot {slotId}
              Producer context: unresolved from template artifact input mapping.
              Runtime rule: this slot was available before scheduling this step. Do not block only because a slot-id directory is absent; inspect upstream step summaries and managed process artifacts first.
            """);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatRequiredArtifactContext(
        ProcessRunId runId,
        ArtifactSlotId slotId,
        ProcessTemplateDefinitionStepDocument sourceStep,
        string expectationKey,
        ProcessTemplateDefinitionArtifactExpectationDocument? expectation)
    {
        var expectationTitle = string.IsNullOrWhiteSpace(expectation?.Title)
            ? expectationKey
            : expectation.Title.Trim();
        var artifactKind = string.IsNullOrWhiteSpace(expectation?.ArtifactKind)
            ? "Artifact"
            : expectation.ArtifactKind.Trim();
        var validation = string.IsNullOrWhiteSpace(expectation?.ValidationRequirementSummary)
            ? "Use the producer step output contract and evidence contract."
            : expectation.ValidationRequirementSummary.Trim();

        return $"""
        - Slot {slotId}
          Producer step: {sourceStep.Key} - {sourceStep.Title}
          Artifact expectation: {expectationKey} - {expectationTitle} ({artifactKind})
          Evidence refs to inspect: {BuildStepEvidencePath(runId, sourceStep.Key)}; {BuildSlotEvidenceRoot(runId, slotId)}; {BuildStepEvidenceRoot(runId, sourceStep.Key)}
          Runtime rule: this slot is available only after the producer completed. Do not block only because a slot-id directory is absent; the producer step summary path is valid upstream evidence for this slot.
          Validation: {validation}
        """;
    }

    private static string BuildProducedArtifactContext(
        ProcessTemplateDefinitionStepDocument step,
        IReadOnlyList<ArtifactSlotId> producedSlots,
        IReadOnlyDictionary<(string StepKey, string ExpectationKey), ArtifactSlotId> artifactSlotByStepExpectation,
        ProcessRunId runId)
    {
        if (producedSlots.Count == 0)
        {
            return "No produced artifact slots.";
        }

        var producedSlotSet = producedSlots.ToHashSet();
        var describedSlots = new HashSet<ArtifactSlotId>();
        var lines = new List<string>();

        foreach (var expectation in step.ArtifactExpectations)
        {
            if (!artifactSlotByStepExpectation.TryGetValue((step.Key, expectation.Key), out var slotId) ||
                !producedSlotSet.Contains(slotId))
            {
                continue;
            }

            lines.Add(FormatProducedArtifactContext(runId, slotId, step, expectation));
            describedSlots.Add(slotId);
        }

        foreach (var slotId in producedSlots.Where(slotId => !describedSlots.Contains(slotId)))
        {
            lines.Add($"""
            - Slot {slotId}
              Write refs: {BuildStepEvidencePath(runId, step.Key)}; {BuildSlotEvidenceRoot(runId, slotId)}
              Completion rule: include the written managed artifact path in evidenceRefs before returning Completed.
            """);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatProducedArtifactContext(
        ProcessRunId runId,
        ArtifactSlotId slotId,
        ProcessTemplateDefinitionStepDocument step,
        ProcessTemplateDefinitionArtifactExpectationDocument expectation)
    {
        var title = string.IsNullOrWhiteSpace(expectation.Title)
            ? expectation.Key
            : expectation.Title.Trim();
        var artifactKind = string.IsNullOrWhiteSpace(expectation.ArtifactKind)
            ? "Artifact"
            : expectation.ArtifactKind.Trim();
        var validation = string.IsNullOrWhiteSpace(expectation.ValidationRequirementSummary)
            ? "Use this step's output contract and evidence contract."
            : expectation.ValidationRequirementSummary.Trim();

        return $"""
        - Slot {slotId}
          Artifact expectation: {expectation.Key} - {title} ({artifactKind})
          Write refs: {BuildStepEvidencePath(runId, step.Key)}; {BuildSlotEvidenceRoot(runId, slotId)}; {BuildStepEvidenceRoot(runId, step.Key)}
          Completion rule: include the written managed artifact path in evidenceRefs before returning Completed.
          Validation: {validation}
        """;
    }

    private static string BuildStepEvidencePath(ProcessRunId runId, string stepKey)
        => $"{BuildManagedProcessArtifactRoot(runId)}/steps/{SanitizeEvidencePathSegment(stepKey)}.md";

    private static string BuildSlotEvidenceRoot(ProcessRunId runId, ArtifactSlotId slotId)
        => $"{BuildManagedProcessArtifactRoot(runId)}/{slotId}";

    private static string BuildStepEvidenceRoot(ProcessRunId runId, string stepKey)
        => $"{BuildManagedProcessArtifactRoot(runId)}/{SanitizeEvidencePathSegment(stepKey)}/";

    private static string SanitizeEvidencePathSegment(string value)
    {
        var normalized = NormalizeOptional(value, "step");
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-');
        }

        return builder.Length == 0 ? "step" : builder.ToString();
    }

    private static string BuildSubprocessGuidance(ProcessTemplateDefinitionStepDocument step)
    {
        var isSubprocessStep = string.Equals(step.StepKind, SubprocessStepKind, StringComparison.OrdinalIgnoreCase) ||
                               !string.IsNullOrWhiteSpace(step.SubprocessProcessKey);
        if (!isSubprocessStep)
        {
            return "No subprocess mapping.";
        }

        var hasSubprocessKey = !string.IsNullOrWhiteSpace(step.SubprocessProcessKey);
        var subprocessKey = hasSubprocessKey
            ? step.SubprocessProcessKey.Trim()
            : "not mapped";
        var snapshotName = string.IsNullOrWhiteSpace(step.SubprocessDefinitionSnapshotName)
            ? "not supplied"
            : step.SubprocessDefinitionSnapshotName.Trim();
        var launchInstruction = !hasSubprocessKey
            ? "This step is marked as a subprocess but has no child process definition key. Return Blocked unless upstream evidence already supplies the missing child run."
            : $"Use {SubprocessLaunchToolName} with DefinitionKey \"{subprocessKey}\" when {ExecuteExternalActionOperationName} is allowed. Do not mark Completed until the child run receipt and required child evidence are available, or return Blocked with the missing evidence.";

        return $"""
        - Child process definition key: {subprocessKey}
        - Child definition snapshot name: {snapshotName}
        - Governed launch tool: {SubprocessLaunchToolName}
        - Completion rule: {launchInstruction}
        - Live-run profile rule: leave LiveRunProfileKey empty unless the launch variables explicitly provide a valid process live-run profile key for this child definition. BranchName, RepositoryRoot, SessionId, parent DefinitionKey, and child DefinitionKey are not live-run profile keys.
        - Retry rule: repeated launch-tool calls for the same parent run, parent step, project node, and child definition return the existing child run instead of creating another child.
        - Evidence rule: the launch tool result includes ChildManagedArtifactRoot, ChildStepsArtifactRoot, ChildLiveProcessesRoute, and ExpectedChildEvidenceRefs. Treat artifacts under ChildManagedArtifactRoot as the child evidence bundle; do not require child evidence to be copied into the parent run root.
        """;
    }

    public static string BuildManagedProcessArtifactRoot(ProcessRunId runId)
    {
        return $"artifacts/process-runs/{runId}";
    }

    private static RuntimeCommandContext CreateContext(string requestedBy, DateTimeOffset occurredAtUtc)
    {
        return new RuntimeCommandContext(
            RuntimeCommandId.New(),
            new ProcessEventActor(
                ProcessEventActorKind.User,
                new ProcessActorId(string.IsNullOrWhiteSpace(requestedBy) ? "process-launch" : SanitizeActorId(requestedBy))),
            new ProcessCorrelationId($"launch-{Guid.NewGuid():N}"),
            occurredAtUtc);
    }

    private static ProcessRuntimeMutation CreateAppliedMutation(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context,
        ProcessEventType eventType,
        string payloadHash)
    {
        var runtimeEvent = new ProcessRuntimeEventEnvelope(
            RuntimeEventId.New(),
            state.RootRunId,
            state.RunId,
            context.CorrelationId,
            CausationId: null,
            context.Actor,
            ProcessContractVersions.RuntimeEventEnvelopeV1,
            ProcessEventSensitivity.Normal,
            context.OccurredAtUtc,
            eventType,
            payloadHash);
        var validation = ProcessRuntimeEventRules.Validate(runtimeEvent);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.Failures[0].Message);
        }

        return new ProcessRuntimeMutation(
            ProcessRuntimeTransitionOutcome.Applied,
            state,
            [runtimeEvent],
            [new ProcessOutboxMessage(RuntimeOutboxMessageId.New(), runtimeEvent.EventId, ProcessOutboxSubscriberKind.RuntimeProjection, runtimeEvent.PayloadHash)],
            [],
            []);
    }

    private static DateTimeOffset NormalizeUtc(DateTimeOffset value)
    {
        return value.Offset == TimeSpan.Zero ? value : value.ToUniversalTime();
    }

    private static string SanitizeActorId(string value)
    {
        var normalized = new string(value.Trim().Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.' ? character : '-').ToArray());
        return string.IsNullOrWhiteSpace(normalized) ? "process-launch" : normalized;
    }

    private static string BuildRunRoute(ProcessRunId runId, Guid? projectId)
    {
        return projectId is { } scopedProjectId
            ? $"/projects/{scopedProjectId:D}/processes/live?runId={runId.Value:D}"
            : $"/processes/live?runId={runId.Value:D}";
    }

    private static ProcessLaunchStage MapLaunchStage(ProcessRuntimeStatus status)
    {
        return status switch
        {
            ProcessRuntimeStatus.Completed => ProcessLaunchStage.Completed,
            ProcessRuntimeStatus.Failed or ProcessRuntimeStatus.Cancelled => ProcessLaunchStage.Failed,
            ProcessRuntimeStatus.Blocked => ProcessLaunchStage.Blocked,
            _ => ProcessLaunchStage.Running
        };
    }

    private static string ComputeHash(string? value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed record ProcessLaunchPreparation(
        ProcessLaunchResult? EarlyResult,
        ProcessTemplateSelection? Selected,
        ProcessInstancePlan? Plan,
        IReadOnlyList<ProcessRuntimeStepAssignment> Assignments,
        ProcessLaunchPlanView? LaunchPlan,
        IReadOnlyList<ProcessLaunchReadinessFinding> BlockingFindings);

    private sealed record ProcessTemplateSelection(
        ProcessTemplatePack Pack,
        ProcessTemplateDefinitionDocument Definition,
        ProcessTemplateLiveRunProfileDocument? LiveRunProfile);
}
