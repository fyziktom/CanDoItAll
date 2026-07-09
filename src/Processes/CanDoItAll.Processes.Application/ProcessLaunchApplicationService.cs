using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    IProcessStepBriefBuilder stepBriefBuilder,
    IProcessRuntimeDispatchQueue dispatchQueue,
    ProcessRuntimeProjectionCatchupService projectionCatchupService,
    ILaunchVariableTemplateResolver launchVariableTemplateResolver)
{
    private const string ProcessRunNodePrefix = "process-run:";
    private const string ProcessRunIdVariableName = "ProcessRunId";
    private const string ProcessRunNodeIdVariableName = "ProcessRunNodeId";
    private const string CurrentProcessRunIdVariableName = "CurrentProcessRunId";
    private const string CurrentProcessRunNodeIdVariableName = "CurrentProcessRunNodeId";
    private const string CurrentManagedArtifactRootVariableName = "CurrentManagedArtifactRoot";
    private const string ManagedArtifactRootVariableName = "ManagedArtifactRoot";
    private const string ProjectIdVariableName = ProcessRuntimeLaunchVariables.ProjectId;
    private const string ProjectNodeIdVariableName = "ProjectNodeId";
    private const string OutputFolderVariableName = "OutputFolder";
    private const string OutputRootVariableName = "OutputRoot";
    private const string ProductRootVariableName = "ProductRoot";
    private const string ExternalTargetRootVariableName = "ExternalTargetRoot";
    private const string OutputRootAliasVariableName = "OutputRootAlias";
    private const string ProductRootAliasVariableName = "ProductRootAlias";
    private const string WorkspaceAliasVariableName = "WorkspaceAlias";
    private const string LegacyProcessRunIdVariableName = "processRunId";
    private const string LegacyManagedArtifactRootVariableName = "managedArtifactRoot";
    private const string ParentManagedArtifactRootVariableName = "ParentManagedArtifactRoot";
    private const int MaximumLaunchLifecycleConcurrencyRetries = 3;
    private static readonly TimeSpan LaunchLifecycleConcurrencyRetryDelay = TimeSpan.FromMilliseconds(100);

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
        var activeCommit = await ExecuteLaunchLifecycleTransitionWithConcurrencyRetryAsync(
            initialState.RunId,
            reloadedState => engine.ActivateAsync(
                reloadedState,
                CreateContext(request.RequestedBy, NormalizeUtc(clock.GetUtcNow())),
                cancellationToken),
            cancellationToken).ConfigureAwait(false);
        var activeState = activeCommit.State;
        if (activeCommit.Succeeded)
        {
            var scheduled = await ExecuteLaunchLifecycleTransitionWithConcurrencyRetryAsync(
                initialState.RunId,
                reloadedState => engine.ScheduleReadyAsync(
                    reloadedState,
                    CreateContext(request.RequestedBy, NormalizeUtc(clock.GetUtcNow())),
                    cancellationToken),
                cancellationToken).ConfigureAwait(false);
            activeState = scheduled.State;
        }

        await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);

        var stage = ProcessLaunchStage.Running;
        if (request.Execute)
        {
            await dispatchQueue.EnqueueAsync(
                new ProcessRuntimeDispatchQueueRequest(activeState.RunId, request.RequestedBy),
                cancellationToken).ConfigureAwait(false);
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

            if (state.Status is ProcessRuntimeStatus.Failed or ProcessRuntimeStatus.Cancelled or ProcessRuntimeStatus.Blocked)
            {
                continue;
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
                request.Variables)
            {
                ExecutorOverrides = request.ExecutorOverrides
            },
            cancellationToken).ConfigureAwait(false);
        var assignmentBuild = BuildAssignments(
            request,
            selected,
            kernelBuild,
            plan,
            executorResolution,
            nowUtc);
        var assignments = assignmentBuild.Assignments;
        var readinessFindings = executorResolution.Findings
            .Concat(assignmentBuild.Findings)
            .ToArray();
        var launchPlan = CreateLaunchPlanView(
            selected,
            plan,
            assignments,
            readinessFindings);
        var blockingFindings = readinessFindings
            .Where(finding => finding.Severity == ProcessLaunchReadinessSeverity.Error)
            .ToArray();
        var assignmentBlockingFindings = assignmentBuild.Findings
            .Where(finding => finding.Severity == ProcessLaunchReadinessSeverity.Error)
            .ToArray();
        if (assignmentBlockingFindings.Length > 0 ||
            (request.RunReadiness && blockingFindings.Length > 0))
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

    private ProcessLaunchAssignmentBuildResult BuildAssignments(
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
        var stepsByKey = selection.Definition.Steps.ToDictionary(step => step.Key, StringComparer.OrdinalIgnoreCase);
        var roleByKey = selection.Definition.RoleUsages.ToDictionary(role => role.Key, StringComparer.OrdinalIgnoreCase);
        var launchVariables = EnrichLaunchScopeVariables(
            NormalizeLaunchVariables(request.Variables),
            request);
        launchVariables = EnrichDefinitionLaunchVariables(launchVariables, selection.Definition);
        var runId = ProcessRunId.New();
        var managedArtifactRoot = BuildManagedProcessArtifactRoot(runId);
        var effectiveLaunchVariables = EnrichRunLaunchVariables(launchVariables, runId, managedArtifactRoot);
        var assignments = new List<ProcessRuntimeStepAssignment>();
        var findings = new List<ProcessLaunchReadinessFinding>();

        foreach (var planStep in plan.Steps.Where(step => step.IsExecutable))
        {
            if (!stepsByKey.TryGetValue(planStep.StepKey, out var templateStep))
            {
                continue;
            }

            executorByStepKey.TryGetValue(planStep.StepKey, out var binding);

            var requiredSlots = ResolveRequiredSlots(templateStep, kernelBuild);
            var producedSlots = ResolveProducedSlots(templateStep, kernelBuild);
            var roleKey = binding?.RoleKey ?? ResolvePrimaryRoleKey(templateStep);
            roleByKey.TryGetValue(roleKey, out var role);
            var stepLaunchVariables = EnrichStepLaunchVariables(effectiveLaunchVariables, templateStep);
            var resolution = launchVariableTemplateResolver.Resolve(stepLaunchVariables);
            stepLaunchVariables = resolution.Variables;
            findings.AddRange(CreateLaunchVariableReadinessFindings(planStep.StepKey, roleKey, resolution.Diagnostics));
            assignments.Add(new ProcessRuntimeStepAssignment(
                runId,
                plan.Header.PlanId,
                planStep.StepInstanceId,
                planStep.StepKey,
                roleKey,
                role?.RoleResourceKey ?? string.Empty,
                role?.DisplayName ?? roleKey,
                binding?.ExecutorKind ?? string.Empty,
                binding?.ExecutorId ?? string.Empty,
                binding?.ExecutorDisplayName ?? string.Empty,
                stepBriefBuilder.Build(new ProcessStepBriefBuildRequest(
                    request,
                    selection.Definition,
                    templateStep,
                    binding,
                    requiredSlots,
                    producedSlots,
                    kernelBuild.ArtifactSlotByStepExpectation,
                    runId,
                    managedArtifactRoot,
                    stepLaunchVariables)),
                binding?.ReadinessHash ?? ComputeHash($"missing:{planStep.StepKey}"),
                binding?.AssignmentReason ?? "No executor binding was resolved.",
                producedSlots,
                requiredSlots,
                NormalizeAllowedOperations(templateStep.AllowedOperations),
                NormalizeOperationTargetScope(templateStep.OperationTargetScope),
                stepLaunchVariables,
                ResolveBranchGate(templateStep),
                nowUtc)
            {
                CapabilityScope = ProcessCapabilityScope.Normalize(templateStep.CapabilityScope)
            });
        }

        if (assignments.Count == 0)
        {
            return new ProcessLaunchAssignmentBuildResult(assignments, findings);
        }

        return new ProcessLaunchAssignmentBuildResult(assignments, findings);
    }

    private static IReadOnlyList<ProcessLaunchReadinessFinding> CreateLaunchVariableReadinessFindings(
        string stepKey,
        string roleKey,
        IReadOnlyList<LaunchVariableTemplateDiagnostic> diagnostics)
    {
        return diagnostics
            .Where(diagnostic => diagnostic.IsBlocking)
            .Select(diagnostic => new ProcessLaunchReadinessFinding(
                ProcessLaunchReadinessSeverity.Error,
                MapLaunchVariableDiagnosticCode(diagnostic.Kind),
                diagnostic.Message,
                stepKey,
                roleKey))
            .ToArray();
    }

    private static string MapLaunchVariableDiagnosticCode(LaunchVariableTemplateDiagnosticKind kind)
    {
        return kind switch
        {
            LaunchVariableTemplateDiagnosticKind.Cycle => "process.launch.variable_placeholder_cycle",
            LaunchVariableTemplateDiagnosticKind.MaximumPassesExceeded => "process.launch.variable_placeholder_max_passes",
            _ => "process.launch.variable_placeholder_unresolved"
        };
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
                CompletedResultKey: null)
            {
                ProducedArtifactSlots = assignment?.ProducedArtifactSlotIds.ToHashSet() ?? [],
                RequiredRuntimeToolNames = assignment is null
                    ? []
                    : ResolveLaunchPlanRequiredRuntimeToolNames(assignment, templateStep),
                ArtifactDescriptors = templateStep is null
                    ? []
                    : BuildRuntimeArtifactDescriptors(definition, templateStep, plan.ArtifactPlan.Slots, runId),
                SubprocessArtifactMappings = templateStep is null
                    ? []
                    : BuildSubprocessArtifactMappings(templateStep, plan.ArtifactPlan.Slots)
            });
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
            nowUtc)
        {
            ConnectedInputArtifacts = ProcessRuntimeArtifactContracts.BuildInitialInputArtifacts(
                assignments,
                plan.ArtifactPlan.InitialLedgerEntries)
        };
    }

    private static ProcessLaunchPlanView CreateLaunchPlanView(
        ProcessTemplateSelection selection,
        ProcessInstancePlan plan,
        IReadOnlyList<ProcessRuntimeStepAssignment> assignments,
        IReadOnlyList<ProcessLaunchReadinessFinding> findings)
    {
        var assignmentByStepKey = assignments.ToDictionary(assignment => assignment.StepKey, StringComparer.OrdinalIgnoreCase);
        var templateStepByKey = selection.Definition.Steps.ToDictionary(step => step.Key, StringComparer.OrdinalIgnoreCase);
        var roleByKey = selection.Definition.RoleUsages.ToDictionary(role => role.Key, StringComparer.OrdinalIgnoreCase);
        var stepViews = plan.Steps
            .Select(step =>
            {
                assignmentByStepKey.TryGetValue(step.StepKey, out var assignment);
                templateStepByKey.TryGetValue(step.StepKey, out var templateStep);
                ProcessTemplateDefinitionRoleUsageDocument? role = null;
                if (!string.IsNullOrWhiteSpace(assignment?.RoleKey))
                {
                    roleByKey.TryGetValue(assignment.RoleKey, out role);
                }

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
                    assignment?.BranchGate)
                {
                    AllowedOperations = templateStep is null ? [] : NormalizeAllowedOperations(templateStep.AllowedOperations),
                    OperationTargetScope = templateStep is null ? string.Empty : NormalizeOperationTargetScope(templateStep.OperationTargetScope),
                    RoleResourceKey = FirstNonEmpty(assignment?.RoleResourceKey, role?.RoleResourceKey),
                    RoleDisplayName = FirstNonEmpty(assignment?.RoleDisplayName, role?.DisplayName, assignment?.RoleKey),
                    CapabilityScope = ProcessCapabilityScope.Normalize(templateStep?.CapabilityScope),
                    RequiredRuntimeToolNames = ResolveLaunchPlanRequiredRuntimeToolNames(assignment, templateStep)
                };
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

    private static IReadOnlyList<string> ResolveLaunchPlanRequiredRuntimeToolNames(
        ProcessRuntimeStepAssignment? assignment,
        ProcessTemplateDefinitionStepDocument? templateStep)
    {
        if (assignment is null)
        {
            return [];
        }

        var launchContextToolNames = ResolveLaunchPlanRequiredRuntimeToolNameSet(assignment.LaunchVariables);
        return launchContextToolNames
            .Concat(ProcessRequiredRuntimeToolNames.NormalizeRuntimeToolNameCandidates(templateStep?.ExecutionContract?.RequiredRuntimeToolNames))
            .Concat(ProcessRequiredRuntimeToolNames.FromCapabilityScope(assignment.CapabilityScope, launchContextToolNames))
            .Where(toolName => !string.IsNullOrWhiteSpace(toolName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(toolName => toolName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlySet<string> ResolveLaunchPlanRequiredRuntimeToolNameSet(
        IReadOnlyDictionary<string, string> launchVariables)
    {
        if (!launchVariables.TryGetValue(ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts, out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return ProcessRequiredRuntimeToolNames.FromProductCompletionRequiredToolReceipts(value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
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

    private static IReadOnlyList<ProcessArtifactSlotDescriptor> BuildRuntimeArtifactDescriptors(
        ProcessTemplateDefinitionDocument definition,
        ProcessTemplateDefinitionStepDocument step,
        IReadOnlyList<ArtifactSlotPlan> artifactSlots,
        ProcessRunId runId)
    {
        var descriptors = new Dictionary<ArtifactSlotId, ProcessArtifactSlotDescriptor>();
        var stepByKey = definition.Steps.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        var slotByKey = artifactSlots.ToDictionary(slot => slot.SlotKey, StringComparer.OrdinalIgnoreCase);
        var managedArtifactRoot = BuildManagedProcessArtifactRoot(runId);

        foreach (var input in step.ArtifactInputs)
        {
            if (string.IsNullOrWhiteSpace(input.SourceStepKey) ||
                string.IsNullOrWhiteSpace(input.ArtifactExpectationKey) ||
                !stepByKey.TryGetValue(input.SourceStepKey, out var sourceStep) ||
                !slotByKey.TryGetValue(BuildArtifactSlotKey(input.SourceStepKey, input.ArtifactExpectationKey), out var slot))
            {
                continue;
            }

            var expectation = sourceStep.ArtifactExpectations.FirstOrDefault(candidate =>
                string.Equals(candidate.Key, input.ArtifactExpectationKey, StringComparison.OrdinalIgnoreCase));
            descriptors[slot.SlotId] = CreateArtifactSlotDescriptor(
                slot,
                sourceStep.Key,
                input.ArtifactExpectationKey,
                expectation,
                BuildManagedStepArtifactPath(managedArtifactRoot, sourceStep.Key),
                ProcessArtifactMaterializationMode.AgentWritten);
        }

        foreach (var expectation in step.ArtifactExpectations)
        {
            if (!slotByKey.TryGetValue(BuildArtifactSlotKey(step.Key, expectation.Key), out var slot))
            {
                continue;
            }

            descriptors[slot.SlotId] = CreateArtifactSlotDescriptor(
                slot,
                step.Key,
                expectation.Key,
                expectation,
                BuildManagedStepArtifactPath(managedArtifactRoot, step.Key),
                ResolveProducedArtifactMaterializationMode(step, expectation));
        }

        return descriptors.Values
            .OrderBy(descriptor => descriptor.StepKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(descriptor => descriptor.ArtifactExpectationKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ProcessArtifactSlotDescriptor CreateArtifactSlotDescriptor(
        ArtifactSlotPlan slot,
        string stepKey,
        string expectationKey,
        ProcessTemplateDefinitionArtifactExpectationDocument? expectation,
        string primaryManagedRef,
        ProcessArtifactMaterializationMode materializationMode)
    {
        return new ProcessArtifactSlotDescriptor(
            slot.SlotId,
            slot.SlotKey,
            stepKey,
            expectationKey,
            FirstNonEmpty(expectation?.Title, expectationKey),
            FirstNonEmpty(expectation?.ArtifactKind, "Artifact"),
            primaryManagedRef,
            materializationMode);
    }

    private static ProcessArtifactMaterializationMode ResolveProducedArtifactMaterializationMode(
        ProcessTemplateDefinitionStepDocument step,
        ProcessTemplateDefinitionArtifactExpectationDocument expectation)
    {
        if (step.SubprocessContract is not { } contract ||
            !string.Equals(
                contract.ParentProducedArtifactExpectationKey,
                expectation.Key,
                StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactMaterializationMode.AgentWritten;
        }

        return contract.MaterializationMode == ProcessSubprocessMaterializationMode.RuntimeSynthesizedParentHandoff
            ? ProcessArtifactMaterializationMode.RuntimeSynthesizedParentHandoff
            : ProcessArtifactMaterializationMode.AgentWritten;
    }

    private static IReadOnlyList<SubprocessArtifactMappingDescriptor> BuildSubprocessArtifactMappings(
        ProcessTemplateDefinitionStepDocument step,
        IReadOnlyList<ArtifactSlotPlan> artifactSlots)
    {
        if (step.SubprocessContract is not { } contract ||
            string.IsNullOrWhiteSpace(contract.ParentProducedArtifactExpectationKey))
        {
            return [];
        }

        var parentSlot = artifactSlots.FirstOrDefault(slot =>
            string.Equals(slot.SlotKey, BuildArtifactSlotKey(step.Key, contract.ParentProducedArtifactExpectationKey), StringComparison.OrdinalIgnoreCase));
        if (parentSlot is null)
        {
            return [];
        }

        return
        [
            new SubprocessArtifactMappingDescriptor(
                parentSlot.SlotId,
                contract.ParentProducedArtifactExpectationKey,
                contract.DefinitionKey,
                contract.AcceptedChildOutputs.Select(CreateSubprocessChildMappingDescriptor).ToArray(),
                contract.NoGoChildOutputs.Select(CreateSubprocessChildMappingDescriptor).ToArray())
        ];
    }

    private static SubprocessChildArtifactMappingDescriptor CreateSubprocessChildMappingDescriptor(
        ProcessSubprocessChildOutputContract output)
        => new(
            output.StepKey,
            output.ArtifactExpectationKey,
            output.ArtifactTitle,
            output.BranchOutcomeKey);

    private static string BuildArtifactSlotKey(
        string stepKey,
        string expectationKey)
        => $"{stepKey}:{expectationKey}";

    private static string BuildManagedStepArtifactPath(
        string managedArtifactRoot,
        string stepKey)
        => $"{managedArtifactRoot}/steps/{SanitizeManagedArtifactPathSegment(stepKey)}.md";

    private static string SanitizeManagedArtifactPathSegment(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? "step"
            : value.Trim();
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-');
        }

        return builder.Length == 0 ? "step" : builder.ToString();
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

        NormalizeProductRootSynonyms(normalized);
        return normalized;
    }

    private static void NormalizeProductRootSynonyms(IDictionary<string, string> variables)
    {
        var productRoot = FirstNonEmpty(
            TryGetNonEmptyLaunchVariable(variables, ProductRootVariableName),
            TryGetNonEmptyLaunchVariable(variables, OutputRootVariableName),
            TryGetNonEmptyLaunchVariable(variables, OutputFolderVariableName));
        var externalTargetRoot = TryGetNonEmptyLaunchVariable(variables, ExternalTargetRootVariableName);
        if (string.IsNullOrWhiteSpace(productRoot) &&
            !IsExternalTargetAlias(externalTargetRoot))
        {
            productRoot = externalTargetRoot;
        }

        if (!string.IsNullOrWhiteSpace(productRoot))
        {
            SetCanonicalLaunchVariableIfMissing(variables, ProductRootVariableName, productRoot);
            SetCanonicalLaunchVariableIfMissing(variables, OutputRootVariableName, productRoot);
            SetCanonicalLaunchVariableIfMissing(variables, OutputFolderVariableName, productRoot);
        }

        var externalTargetAlias = FirstNonEmpty(
            NormalizeExternalTargetAlias(productRoot),
            NormalizeExternalTargetAlias(externalTargetRoot));
        if (string.IsNullOrWhiteSpace(externalTargetAlias))
        {
            return;
        }

        SetCanonicalLaunchVariable(variables, ExternalTargetRootVariableName, externalTargetAlias);
        SetCanonicalLaunchVariableIfMissing(variables, OutputRootAliasVariableName, externalTargetAlias);
        SetCanonicalLaunchVariableIfMissing(variables, ProductRootAliasVariableName, externalTargetAlias);
        SetCanonicalLaunchVariableIfMissing(variables, WorkspaceAliasVariableName, externalTargetAlias);
    }

    private static string NormalizeExternalTargetAlias(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Replace('\\', '/').TrimEnd('/', '.', ',', ';', ')', ']');
        if (normalized.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        if (normalized.Length < 4 ||
            normalized[1] != ':' ||
            normalized[2] != '/' ||
            !char.IsAsciiLetter(normalized[0]))
        {
            return string.Empty;
        }

        var suffix = normalized[3..].Trim('/');
        if (string.IsNullOrWhiteSpace(suffix))
        {
            return string.Empty;
        }

        return $"external-target/{char.ToUpperInvariant(normalized[0])}/{suffix}";
    }

    private static bool IsExternalTargetAlias(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Trim().Replace('\\', '/').StartsWith("external-target/", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, string> EnrichLaunchScopeVariables(
        IReadOnlyDictionary<string, string> variables,
        ProcessLaunchRequest request)
    {
        var enriched = new Dictionary<string, string>(variables, StringComparer.Ordinal);
        if (request.ProjectId is { } projectId && projectId != Guid.Empty)
        {
            SetCanonicalLaunchVariable(enriched, ProjectIdVariableName, projectId.ToString("D"));
        }

        if (!string.IsNullOrWhiteSpace(request.ProjectNodeId))
        {
            SetCanonicalLaunchVariable(enriched, ProjectNodeIdVariableName, request.ProjectNodeId.Trim());
        }

        return enriched;
    }

    private static IReadOnlyDictionary<string, string> EnrichDefinitionLaunchVariables(
        IReadOnlyDictionary<string, string> variables,
        ProcessTemplateDefinitionDocument definition)
    {
        var enriched = new Dictionary<string, string>(variables, StringComparer.Ordinal);
        SetCanonicalLaunchVariable(enriched, ProcessRuntimeLaunchVariables.ProcessDefinitionKey, definition.Key);
        SetCanonicalLaunchVariable(enriched, ProcessRuntimeLaunchVariables.ProcessDefinitionName, definition.DisplayName);
        return enriched;
    }

    private static void SetCanonicalLaunchVariable(
        IDictionary<string, string> variables,
        string key,
        string value)
    {
        foreach (var existingKey in variables.Keys
            .Where(candidate => string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(candidate, key, StringComparison.Ordinal))
            .ToArray())
        {
            variables.Remove(existingKey);
        }

        variables[key] = value;
    }

    private static void SetCanonicalLaunchVariableIfMissing(
        IDictionary<string, string> variables,
        string key,
        string value)
    {
        var existingValue = TryGetNonEmptyLaunchVariable(variables, key);
        if (!string.IsNullOrWhiteSpace(existingValue))
        {
            SetCanonicalLaunchVariable(variables, key, existingValue);
            return;
        }

        SetCanonicalLaunchVariable(variables, key, value);
    }

    private static string TryGetNonEmptyLaunchVariable(
        IEnumerable<KeyValuePair<string, string>> variables,
        string key)
    {
        foreach (var (candidateKey, candidateValue) in variables)
        {
            if (string.Equals(candidateKey, key, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(candidateValue))
            {
                return candidateValue.Trim();
            }
        }

        return string.Empty;
    }

    private static IReadOnlyDictionary<string, string> EnrichRunLaunchVariables(
        IReadOnlyDictionary<string, string> variables,
        ProcessRunId runId,
        string managedArtifactRoot)
    {
        var enriched = new Dictionary<string, string>(variables, StringComparer.Ordinal);
        var runIdText = runId.Value.ToString("D");
        var runNodeId = BuildProcessRunNodeKey(runId.Value);

        PreservePreviousValue(
            enriched,
            LegacyManagedArtifactRootVariableName,
            ParentManagedArtifactRootVariableName,
            managedArtifactRoot);

        enriched[CurrentProcessRunIdVariableName] = runIdText;
        enriched[CurrentProcessRunNodeIdVariableName] = runNodeId;
        enriched[CurrentManagedArtifactRootVariableName] = managedArtifactRoot;
        enriched[ProcessRunIdVariableName] = runIdText;
        enriched[LegacyProcessRunIdVariableName] = runIdText;
        enriched[ManagedArtifactRootVariableName] = managedArtifactRoot;
        enriched[LegacyManagedArtifactRootVariableName] = managedArtifactRoot;

        if (!enriched.TryGetValue(ProcessRunNodeIdVariableName, out var processRunNodeId) ||
            string.IsNullOrWhiteSpace(processRunNodeId))
        {
            enriched[ProcessRunNodeIdVariableName] = runNodeId;
        }

        return enriched;
    }

    private static IReadOnlyDictionary<string, string> EnrichStepLaunchVariables(
        IReadOnlyDictionary<string, string> variables,
        ProcessTemplateDefinitionStepDocument step)
    {
        var enriched = new Dictionary<string, string>(variables, StringComparer.Ordinal);
        ApplyStepScopedLaunchVariablePrefixFilter(enriched, step.Key);
        var inheritedProductCompletionPaths = TryGetNonEmptyLaunchVariable(
            enriched,
            ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths);
        var inheritedProductCompletionFileContentChecks = TryGetNonEmptyLaunchVariable(
            enriched,
            ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks);
        var inheritedProductCompletionToolReceipts = TryGetNonEmptyLaunchVariable(
            enriched,
            ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts);
        RemoveCanonicalLaunchVariable(enriched, ProcessRuntimeLaunchVariables.ProcessStepKind);
        RemoveCanonicalLaunchVariable(enriched, ProcessRuntimeLaunchVariables.ProcessStepSubprocessContractJson);
        RemoveCanonicalLaunchVariable(enriched, ProcessRuntimeLaunchVariables.ProcessStepSubprocessDefinitionKey);
        RemoveCanonicalLaunchVariable(enriched, ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths);
        RemoveCanonicalLaunchVariable(enriched, ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks);
        RemoveCanonicalLaunchVariable(enriched, ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts);

        if (!string.IsNullOrWhiteSpace(step.StepKind))
        {
            SetCanonicalLaunchVariable(enriched, ProcessRuntimeLaunchVariables.ProcessStepKind, step.StepKind.Trim());
        }

        if (!string.IsNullOrWhiteSpace(step.SubprocessProcessKey))
        {
            SetCanonicalLaunchVariable(
                enriched,
                ProcessRuntimeLaunchVariables.ProcessStepSubprocessDefinitionKey,
                step.SubprocessProcessKey.Trim());
        }

        if (step.SubprocessContract is not null)
        {
            SetCanonicalLaunchVariable(
                enriched,
                ProcessRuntimeLaunchVariables.ProcessStepSubprocessContractJson,
                ProcessRuntimeLaunchVariables.SerializeProcessStepSubprocessContract(step.SubprocessContract));
        }

        if (TryResolveStepProductCompletionRequiredPaths(enriched, step.Key, out var stepProductCompletionPaths))
        {
            SetCanonicalLaunchVariable(
                enriched,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths,
                stepProductCompletionPaths);
        }
        else if (!string.IsNullOrWhiteSpace(inheritedProductCompletionPaths))
        {
            SetCanonicalLaunchVariable(
                enriched,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths,
                inheritedProductCompletionPaths);
        }

        if (TryResolveStepProductCompletionRequiredToolReceipts(enriched, step.Key, out var stepProductCompletionToolReceipts))
        {
            SetCanonicalLaunchVariable(
                enriched,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts,
                stepProductCompletionToolReceipts);
        }
        else if (!string.IsNullOrWhiteSpace(inheritedProductCompletionToolReceipts))
        {
            SetCanonicalLaunchVariable(
                enriched,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts,
                inheritedProductCompletionToolReceipts);
        }

        if (TryResolveStepProductCompletionRequiredFileContentChecks(enriched, step.Key, out var stepProductCompletionFileContentChecks))
        {
            SetCanonicalLaunchVariable(
                enriched,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks,
                stepProductCompletionFileContentChecks);
        }
        else if (!string.IsNullOrWhiteSpace(inheritedProductCompletionFileContentChecks))
        {
            SetCanonicalLaunchVariable(
                enriched,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks,
                inheritedProductCompletionFileContentChecks);
        }

        return enriched;
    }

    private static void ApplyStepScopedLaunchVariablePrefixFilter(
        IDictionary<string, string> variables,
        string stepKey)
    {
        var mapJson = TryGetNonEmptyLaunchVariable(
            variables,
            ProcessRuntimeLaunchVariables.ProcessStepScopedLaunchVariablePrefixesByStep);
        RemoveCanonicalLaunchVariable(
            variables,
            ProcessRuntimeLaunchVariables.ProcessStepScopedLaunchVariablePrefixesByStep);
        if (string.IsNullOrWhiteSpace(mapJson) ||
            string.IsNullOrWhiteSpace(stepKey) ||
            !TryResolveStepScopedLaunchVariablePrefixes(
                mapJson,
                stepKey,
                out var scopedPrefixes,
                out var currentStepPrefixes))
        {
            return;
        }

        foreach (var key in variables.Keys.ToArray())
        {
            if (StartsWithAnyPrefix(key, scopedPrefixes) &&
                !StartsWithAnyPrefix(key, currentStepPrefixes))
            {
                variables.Remove(key);
            }
        }
    }

    private static bool TryResolveStepScopedLaunchVariablePrefixes(
        string mapJson,
        string stepKey,
        out IReadOnlyList<string> scopedPrefixes,
        out IReadOnlyList<string> currentStepPrefixes)
    {
        scopedPrefixes = [];
        currentStepPrefixes = [];

        try
        {
            using var document = JsonDocument.Parse(mapJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var allPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var stepPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                var propertyPrefixes = ReadStringArray(property.Value);
                foreach (var prefix in propertyPrefixes)
                {
                    allPrefixes.Add(prefix);
                }

                if (!string.Equals(property.Name, stepKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var prefix in propertyPrefixes)
                {
                    stepPrefixes.Add(prefix);
                }
            }

            if (allPrefixes.Count == 0)
            {
                return false;
            }

            scopedPrefixes = allPrefixes
                .OrderByDescending(prefix => prefix.Length)
                .ThenBy(prefix => prefix, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            currentStepPrefixes = stepPrefixes
                .OrderByDescending(prefix => prefix.Length)
                .ThenBy(prefix => prefix, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString()?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(value) ? [] : [value];
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return element
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()?.Trim() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool StartsWithAnyPrefix(string value, IReadOnlyList<string> prefixes)
    {
        return prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryResolveStepProductCompletionRequiredPaths(
        IReadOnlyDictionary<string, string> variables,
        string stepKey,
        out string paths)
    {
        paths = string.Empty;
        var mapJson = TryGetNonEmptyLaunchVariable(
            variables,
            ProcessRuntimeLaunchVariables.ProductCompletionRequiredPathsByStep);
        if (string.IsNullOrWhiteSpace(mapJson) ||
            string.IsNullOrWhiteSpace(stepKey))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(mapJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, stepKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                paths = FormatProductCompletionRequiredStringList(property.Value);
                return !string.IsNullOrWhiteSpace(paths);
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool TryResolveStepProductCompletionRequiredToolReceipts(
        IReadOnlyDictionary<string, string> variables,
        string stepKey,
        out string toolReceipts)
    {
        toolReceipts = string.Empty;
        var mapJson = TryGetNonEmptyLaunchVariable(
            variables,
            ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep);
        if (string.IsNullOrWhiteSpace(mapJson) ||
            string.IsNullOrWhiteSpace(stepKey))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(mapJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, stepKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                toolReceipts = FormatProductCompletionRequiredStringList(property.Value);
                return !string.IsNullOrWhiteSpace(toolReceipts);
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool TryResolveStepProductCompletionRequiredFileContentChecks(
        IReadOnlyDictionary<string, string> variables,
        string stepKey,
        out string checks)
    {
        checks = string.Empty;
        var mapJson = TryGetNonEmptyLaunchVariable(
            variables,
            ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecksByStep);
        if (string.IsNullOrWhiteSpace(mapJson) ||
            string.IsNullOrWhiteSpace(stepKey))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(mapJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, stepKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                checks = property.Value.GetRawText();
                return !string.IsNullOrWhiteSpace(checks);
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static string FormatProductCompletionRequiredStringList(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString()?.Trim() ?? string.Empty;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        if (element.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.Object))
        {
            return element.GetRawText();
        }

        return string.Join(
            Environment.NewLine,
            element.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()?.Trim() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static void RemoveCanonicalLaunchVariable(
        IDictionary<string, string> variables,
        string key)
    {
        foreach (var existingKey in variables.Keys
            .Where(candidate => string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase))
            .ToArray())
        {
            variables.Remove(existingKey);
        }
    }

    private static void PreservePreviousValue(
        IDictionary<string, string> variables,
        string sourceKey,
        string targetKey,
        string replacementValue)
    {
        if (!variables.TryGetValue(sourceKey, out var value) ||
            string.IsNullOrWhiteSpace(value) ||
            string.Equals(value.Trim(), replacementValue, StringComparison.Ordinal))
        {
            return;
        }

        variables.TryAdd(targetKey, value.Trim());
    }

    public static string BuildManagedProcessArtifactRoot(ProcessRunId runId)
    {
        return $"artifacts/process-runs/{runId}";
    }

    private static string BuildProcessRunNodeKey(Guid runId)
    {
        return $"{ProcessRunNodePrefix}{runId:D}";
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

    private async Task<ProcessRuntimeCommitResult> ExecuteLaunchLifecycleTransitionWithConcurrencyRetryAsync(
        ProcessRunId runId,
        Func<ProcessRuntimeStateSnapshot, Task<ProcessRuntimeCommitResult>> execute,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaximumLaunchLifecycleConcurrencyRetries; attempt++)
        {
            try
            {
                var state = await stateStore.LoadAsync(runId, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException($"Process run '{runId}' was not found.");
                return await execute(state).ConfigureAwait(false);
            }
            catch (ProcessRuntimeOptimisticConcurrencyException) when (attempt < MaximumLaunchLifecycleConcurrencyRetries)
            {
                await Task.Delay(LaunchLifecycleConcurrencyRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        var latestState = await stateStore.LoadAsync(runId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Process run '{runId}' was not found.");
        return await execute(latestState).ConfigureAwait(false);
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

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

    private sealed record ProcessLaunchAssignmentBuildResult(
        IReadOnlyList<ProcessRuntimeStepAssignment> Assignments,
        IReadOnlyList<ProcessLaunchReadinessFinding> Findings);

    private sealed record ProcessTemplateSelection(
        ProcessTemplatePack Pack,
        ProcessTemplateDefinitionDocument Definition,
        ProcessTemplateLiveRunProfileDocument? LiveRunProfile);
}
