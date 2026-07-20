using System.Text.Json;
using System.Runtime.ExceptionServices;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowLaunchService(
    IWorkflowCatalogService catalog,
    IWorkflowRuntimeBackendCatalog backendCatalog,
    IWorkflowRunLauncher runLauncher,
    IWorkflowLaunchIdempotencyStore idempotencyStore,
    IWorkflowRunStore runStore,
    TimeProvider timeProvider) : IWorkflowLaunchService
{
    private static readonly TimeSpan ClaimLeaseDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ClaimRenewalInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ClaimPollInterval = TimeSpan.FromMilliseconds(300);

    public async Task<WorkflowLaunchResult> LaunchAsync(
        WorkflowLaunchIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(intent.Selection);
        ArgumentNullException.ThrowIfNull(intent.Origin);
        ArgumentNullException.ThrowIfNull(intent.Idempotency);
        cancellationToken.ThrowIfCancellationRequested();

        ValidateIntent(intent);
        var inputJson = ValidateAndNormalizeInput(intent.InputJson);
        if (intent.Idempotency is WorkflowLaunchIdempotency.CallerSupplied keyed)
        {
            return await LaunchIdempotentlyAsync(intent, inputJson, keyed.Key, cancellationToken);
        }

        return await LaunchNewAsync(
            intent,
            inputJson,
            WorkflowLaunchIdempotencyDisposition.NotRequested,
            cancellationToken);
    }

    private async Task<WorkflowLaunchResult> LaunchIdempotentlyAsync(
        WorkflowLaunchIntent intent,
        string inputJson,
        WorkflowLaunchIdempotencyKey callerKey,
        CancellationToken cancellationToken)
    {
        var scope = WorkflowLaunchIdempotencyRequestFactory.CreateScope(intent, callerKey);
        var fingerprint = WorkflowLaunchIdempotencyRequestFactory.CreateFingerprint(intent, inputJson);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var now = timeProvider.GetUtcNow();
            var claimToken = WorkflowLaunchIdempotencyClaimToken.New();
            var proposedRunId = WorkflowRunId.New();
            var claim = await idempotencyStore.TryClaimAsync(
                scope,
                fingerprint,
                claimToken,
                proposedRunId,
                now,
                now.Add(ClaimLeaseDuration),
                cancellationToken);

            switch (claim.Outcome)
            {
                case WorkflowLaunchIdempotencyClaimOutcome.Completed:
                    return CreateReplayResult(scope, claim.Completion);
                case WorkflowLaunchIdempotencyClaimOutcome.InProgress:
                    await Task.Delay(ClaimPollInterval, timeProvider, cancellationToken);
                    continue;
                case WorkflowLaunchIdempotencyClaimOutcome.Acquired:
                    var reservedRunId = claim.ReservedRunId
                        ?? throw new InvalidOperationException(
                            $"Acquired workflow launch idempotency claim for key '{scope.CallerKey}' has no reserved run id.");
                    return await LaunchClaimedAsync(
                        intent,
                        inputJson,
                        scope,
                        claimToken,
                        reservedRunId,
                        cancellationToken);
                default:
                    throw new InvalidOperationException(
                        $"Workflow launch idempotency claim outcome '{claim.Outcome}' is not supported.");
            }
        }
    }

    private async Task<WorkflowLaunchResult> LaunchClaimedAsync(
        WorkflowLaunchIntent intent,
        string inputJson,
        WorkflowLaunchIdempotencyScope scope,
        WorkflowLaunchIdempotencyClaimToken claimToken,
        WorkflowRunId reservedRunId,
        CancellationToken cancellationToken)
    {
        using var heartbeatStop = new CancellationTokenSource();
        using var launchCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var heartbeat = MaintainClaimLeaseAsync(
            scope,
            claimToken,
            heartbeatStop.Token,
            launchCancellation);

        WorkflowResolvedRuntimeRequest? resolvedRequest = null;
        try
        {
            resolvedRequest = await ResolveRuntimeRequestAsync(
                intent,
                inputJson,
                reservedRunId,
                launchCancellation.Token);
            var run = await runLauncher.StartAsync(resolvedRequest, launchCancellation.Token);
            var result = new WorkflowLaunchResult(
                run,
                resolvedRequest,
                WorkflowLaunchIdempotencyDisposition.EnforcedNewRun);
            if (heartbeat.IsFaulted)
            {
                await heartbeat;
            }

            var completed = await idempotencyStore.TryCompleteClaimAsync(
                scope,
                claimToken,
                new WorkflowLaunchIdempotencyCompletion(
                    result.Run,
                    result.ResolvedRequest,
                    timeProvider.GetUtcNow()),
                CancellationToken.None);
            if (!completed)
            {
                throw new WorkflowLaunchIdempotencyClaimLostException(scope);
            }

            return result;
        }
        catch (WorkflowLaunchIdempotencyClaimLostException)
        {
            throw;
        }
        catch (Exception launchException)
        {
            heartbeatStop.Cancel();
            if (heartbeat.IsFaulted)
            {
                await heartbeat;
            }

            if (resolvedRequest is not null &&
                await TryCompletePersistedRunAfterFailureAsync(
                    scope,
                    claimToken,
                    reservedRunId,
                    resolvedRequest))
            {
                ExceptionDispatchInfo.Capture(launchException).Throw();
            }

            await ReleaseFailedClaimAsync(scope, claimToken, launchException);
            ExceptionDispatchInfo.Capture(launchException).Throw();
            throw;
        }
        finally
        {
            heartbeatStop.Cancel();
            await ObserveHeartbeatCompletionAsync(heartbeat);
        }
    }

    private async Task<bool> TryCompletePersistedRunAfterFailureAsync(
        WorkflowLaunchIdempotencyScope scope,
        WorkflowLaunchIdempotencyClaimToken claimToken,
        WorkflowRunId reservedRunId,
        WorkflowResolvedRuntimeRequest resolvedRequest)
    {
        var persistedRun = await runStore.GetRunAsync(reservedRunId, CancellationToken.None);
        if (persistedRun is null)
        {
            return false;
        }

        if (persistedRun.WorkflowId != resolvedRequest.Definition.Id ||
            persistedRun.VersionId != resolvedRequest.Definition.VersionId)
        {
            throw new InvalidOperationException(
                $"Reserved workflow run '{reservedRunId}' does not match its resolved workflow definition.");
        }

        if (!await idempotencyStore.TryCompleteClaimAsync(
                scope,
                claimToken,
                new WorkflowLaunchIdempotencyCompletion(
                    persistedRun,
                    resolvedRequest,
                    timeProvider.GetUtcNow()),
                CancellationToken.None))
        {
            throw new WorkflowLaunchIdempotencyClaimLostException(scope);
        }

        return true;
    }

    private async Task MaintainClaimLeaseAsync(
        WorkflowLaunchIdempotencyScope scope,
        WorkflowLaunchIdempotencyClaimToken claimToken,
        CancellationToken stopToken,
        CancellationTokenSource launchCancellation)
    {
        try
        {
            while (true)
            {
                await Task.Delay(ClaimRenewalInterval, timeProvider, stopToken);
                var leaseExpiresAtUtc = timeProvider.GetUtcNow().Add(ClaimLeaseDuration);
                if (await idempotencyStore.TryRenewClaimAsync(
                        scope,
                        claimToken,
                        leaseExpiresAtUtc,
                        stopToken))
                {
                    continue;
                }

                launchCancellation.Cancel();
                throw new WorkflowLaunchIdempotencyClaimLostException(scope);
            }
        }
        catch (OperationCanceledException) when (stopToken.IsCancellationRequested)
        {
        }
        catch
        {
            launchCancellation.Cancel();
            throw;
        }
    }

    private async Task ReleaseFailedClaimAsync(
        WorkflowLaunchIdempotencyScope scope,
        WorkflowLaunchIdempotencyClaimToken claimToken,
        Exception launchException)
    {
        try
        {
            if (await idempotencyStore.TryReleaseClaimAsync(scope, claimToken, CancellationToken.None))
            {
                return;
            }
        }
        catch (Exception releaseException)
        {
            throw new WorkflowLaunchIdempotencyReleaseException(
                scope,
                launchException,
                releaseException);
        }

        throw new WorkflowLaunchIdempotencyReleaseException(scope, launchException);
    }

    private static async Task ObserveHeartbeatCompletionAsync(Task heartbeat)
    {
        try
        {
            await heartbeat;
        }
        catch
        {
        }
    }

    private static WorkflowLaunchResult CreateReplayResult(
        WorkflowLaunchIdempotencyScope scope,
        WorkflowLaunchIdempotencyCompletion? completion)
    {
        if (completion is null ||
            completion.Run.WorkflowId != scope.WorkflowId ||
            completion.ResolvedRequest.Definition.Id != scope.WorkflowId ||
            completion.ResolvedRequest.Definition.VersionId != completion.Run.VersionId ||
            completion.ResolvedRequest.Idempotency is not WorkflowLaunchIdempotency.CallerSupplied keyed ||
            keyed.Key != scope.CallerKey)
        {
            throw new InvalidOperationException(
                $"Stored workflow launch idempotency completion for key '{scope.CallerKey}' is inconsistent.");
        }

        if (scope.RequestedVersionId is { } requestedVersionId &&
            completion.Run.VersionId != requestedVersionId)
        {
            throw new InvalidOperationException(
                $"Stored workflow launch idempotency completion for exact version '{requestedVersionId}' resolved to '{completion.Run.VersionId}'.");
        }

        return new WorkflowLaunchResult(
            completion.Run,
            completion.ResolvedRequest,
            WorkflowLaunchIdempotencyDisposition.ReplayedExistingRun);
    }

    private async Task<WorkflowLaunchResult> LaunchNewAsync(
        WorkflowLaunchIntent intent,
        string inputJson,
        WorkflowLaunchIdempotencyDisposition disposition,
        CancellationToken cancellationToken)
    {
        var resolvedRequest = await ResolveRuntimeRequestAsync(
            intent,
            inputJson,
            requestedRunId: null,
            cancellationToken);
        var run = await runLauncher.StartAsync(resolvedRequest, cancellationToken);
        return new WorkflowLaunchResult(
            run,
            resolvedRequest,
            disposition);
    }

    private async Task<WorkflowResolvedRuntimeRequest> ResolveRuntimeRequestAsync(
        WorkflowLaunchIntent intent,
        string inputJson,
        WorkflowRunId? requestedRunId,
        CancellationToken cancellationToken)
    {
        var detail = await ResolveDefinitionAsync(intent.Selection, intent.Mode, cancellationToken);
        ThrowIfDefinitionInvalid(detail);
        ValidateDefinitionStatus(detail.Definition, intent.Mode);

        var backend = ResolveBackend(detail.Definition, intent);
        ValidateRuntimePolicy(detail.Definition, backend, intent.Mode);
        return new WorkflowResolvedRuntimeRequest(
            detail.Definition,
            inputJson,
            backend,
            intent.PreviewSimulationPlan,
            intent.Mode,
            intent.Origin,
            intent.CompletionPolicy,
            intent.Idempotency,
            timeProvider.GetUtcNow())
        {
            RequestedRunId = requestedRunId
        };
    }

    private async Task<WorkflowDefinitionDetail> ResolveDefinitionAsync(
        WorkflowDefinitionSelection selection,
        WorkflowLaunchMode mode,
        CancellationToken cancellationToken)
    {
        return selection switch
        {
            WorkflowDefinitionSelection.ExactSavedVersion exact =>
                await ResolveExactSavedVersionAsync(exact, cancellationToken),
            WorkflowDefinitionSelection.LatestActive latest =>
                await ResolveLatestActiveAsync(latest, cancellationToken),
            WorkflowDefinitionSelection.DraftPreview draft =>
                await ResolveDraftPreviewAsync(draft, mode, cancellationToken),
            _ => throw new InvalidOperationException(
                $"Workflow definition selection '{selection.GetType().Name}' is not supported.")
        };
    }

    private async Task<WorkflowDefinitionDetail> ResolveExactSavedVersionAsync(
        WorkflowDefinitionSelection.ExactSavedVersion selection,
        CancellationToken cancellationToken)
    {
        var current = await catalog.GetDefinitionAsync(
            selection.WorkflowId,
            versionId: null,
            cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Workflow '{selection.WorkflowId}' does not have a current definition.");
        if (current.Definition.Id != selection.WorkflowId)
        {
            throw new InvalidOperationException(
                $"Workflow catalog returned a different current definition for workflow '{selection.WorkflowId}'.");
        }

        if (current.Definition.Status is not (WorkflowLifecycleStatus.Draft or WorkflowLifecycleStatus.Active))
        {
            throw new InvalidOperationException(
                $"Workflow '{selection.WorkflowId}' cannot execute saved version '{selection.VersionId}' while its current definition is '{current.Definition.Status}'. " +
                "Saved-version execution requires a current Draft or Active definition.");
        }

        var detail = current.Definition.VersionId == selection.VersionId
            ? current
            : await catalog.GetDefinitionAsync(
                selection.WorkflowId,
                selection.VersionId,
                cancellationToken)
              ?? throw new KeyNotFoundException(
                  $"Workflow '{selection.WorkflowId}' version '{selection.VersionId}' was not found.");
        if (detail.Definition.Id != selection.WorkflowId ||
            detail.Definition.VersionId != selection.VersionId)
        {
            throw new InvalidOperationException(
                $"Workflow catalog returned a different definition for exact version '{selection.VersionId}'.");
        }

        return detail;
    }

    private async Task<WorkflowDefinitionDetail> ResolveLatestActiveAsync(
        WorkflowDefinitionSelection.LatestActive selection,
        CancellationToken cancellationToken)
    {
        var detail = await catalog.GetLatestDefinitionByStatusAsync(
            selection.WorkflowId,
            WorkflowLifecycleStatus.Active,
            cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Workflow '{selection.WorkflowId}' does not have an Active version.");
        if (detail.Definition.Id != selection.WorkflowId ||
            detail.Definition.Status != WorkflowLifecycleStatus.Active)
        {
            throw new InvalidOperationException(
                $"Workflow catalog returned an invalid result for latest Active workflow '{selection.WorkflowId}'.");
        }

        return detail;
    }

    private async Task<WorkflowDefinitionDetail> ResolveDraftPreviewAsync(
        WorkflowDefinitionSelection.DraftPreview selection,
        WorkflowLaunchMode mode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selection.Definition);
        if (mode != WorkflowLaunchMode.Preview)
        {
            throw new InvalidOperationException("DraftPreview selection is valid only for Preview launches.");
        }

        if (selection.Definition.Status != WorkflowLifecycleStatus.Draft)
        {
            throw new InvalidOperationException(
                $"DraftPreview selection requires a Draft definition, but version '{selection.Definition.VersionId}' is '{selection.Definition.Status}'.");
        }

        return new WorkflowDefinitionDetail(
            selection.Definition,
            await catalog.ValidateDefinitionAsync(selection.Definition, cancellationToken));
    }

    private WorkflowRuntimeBackendDescriptor ResolveBackend(
        WorkflowDefinition definition,
        WorkflowLaunchIntent intent)
    {
        var backendKind = intent.RequestedBackend ?? definition.RuntimePolicy.PreferredBackend;
        if (!Enum.IsDefined(backendKind))
        {
            throw new InvalidOperationException(
                $"Workflow runtime backend value '{Convert.ToInt32(backendKind)}' is not defined.");
        }

        var backend = backendCatalog.GetRequiredBackend(backendKind);
        if (!backend.IsRegistered ||
            !backend.IsRunnable ||
            backend.Availability != WorkflowRuntimeBackendAvailabilityKind.Registered)
        {
            throw new InvalidOperationException(
                $"Workflow runtime backend '{backend.Kind}' ({Convert.ToInt32(backend.Kind)}) is not registered and runnable. {backend.AvailabilityReason}");
        }

        return backend;
    }

    private static void ValidateIntent(WorkflowLaunchIntent intent)
    {
        if (!Enum.IsDefined(intent.Mode))
        {
            throw new InvalidOperationException($"Workflow launch mode '{intent.Mode}' is not defined.");
        }

        if (!Enum.IsDefined(intent.CompletionPolicy))
        {
            throw new InvalidOperationException(
                $"Workflow launch completion policy '{intent.CompletionPolicy}' is not defined.");
        }

        if (intent.CompletionPolicy == WorkflowLaunchCompletionPolicy.ReturnWhenAccepted)
        {
            throw new NotSupportedException(
                "Returning when a workflow run is accepted requires the incremental lifecycle boundary and is not supported yet.");
        }

        if (intent.Mode == WorkflowLaunchMode.Production && intent.PreviewSimulationPlan.HasSteps)
        {
            throw new InvalidOperationException("Production workflow launches cannot include a preview simulation plan.");
        }

        ValidateOrigin(intent.Origin);
        ValidateIdempotency(intent.Idempotency);
    }

    private static void ValidateOrigin(WorkflowLaunchOrigin origin)
    {
        if (string.IsNullOrWhiteSpace(origin.CorrelationId.Value))
        {
            throw new InvalidOperationException("Workflow launch origin requires a correlation id.");
        }

        switch (origin)
        {
            case WorkflowLaunchOrigin.Api { Actor: not null }:
            case WorkflowLaunchOrigin.Preview { Actor: not null }:
                return;
            case WorkflowLaunchOrigin.SchedulerPlanRun scheduler when
                scheduler.PlanId != Guid.Empty &&
                scheduler.PlanRunId != Guid.Empty &&
                scheduler.FireId.Value != Guid.Empty &&
                scheduler.FiredAtUtc != default:
                return;
            case WorkflowLaunchOrigin.ProjectStructureNode project when
                project.ProjectId != Guid.Empty &&
                !string.IsNullOrWhiteSpace(project.NodeId.Value) &&
                project.RequestingActor is { Kind: WorkflowLaunchActorKind.Agent } &&
                !string.IsNullOrWhiteSpace(project.SessionId.Value):
                return;
            case WorkflowLaunchOrigin.AgentRuntimeInvocation agent when
                agent.Agent is { Kind: WorkflowLaunchActorKind.Agent } &&
                !string.IsNullOrWhiteSpace(agent.RuntimeSessionId.Value) &&
                !string.IsNullOrWhiteSpace(agent.Purpose):
                return;
            case WorkflowLaunchOrigin.ProcessAssignment process when
                process.ProcessRunId != Guid.Empty &&
                process.AssignmentId != Guid.Empty:
                return;
            default:
                throw new InvalidOperationException(
                    $"Workflow launch origin '{origin.Kind}' has incomplete lineage identifiers.");
        }
    }

    private static void ValidateIdempotency(WorkflowLaunchIdempotency idempotency)
    {
        if (idempotency is WorkflowLaunchIdempotency.CallerSupplied keyed &&
            string.IsNullOrWhiteSpace(keyed.Key.Value))
        {
            throw new InvalidOperationException("Caller-supplied workflow launch idempotency key is required.");
        }
    }

    private static string ValidateAndNormalizeInput(string inputJson)
    {
        var normalized = string.IsNullOrWhiteSpace(inputJson) ? "{}" : inputJson.Trim();
        try
        {
            using var document = JsonDocument.Parse(normalized);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Workflow launch input must be a valid JSON object.", nameof(inputJson));
            }
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(
                "Workflow launch input must be a valid JSON object.",
                nameof(inputJson),
                exception);
        }

        return normalized;
    }

    private static void ThrowIfDefinitionInvalid(WorkflowDefinitionDetail detail)
    {
        if (detail.Validation.Succeeded)
        {
            return;
        }

        var issues = string.Join(
            " ",
            detail.Validation.Issues.Take(5).Select(issue => $"{issue.Code}: {issue.Message}"));
        throw new WorkflowLaunchValidationException(
            detail.Definition.Id,
            detail.Definition.VersionId,
            detail.Validation,
            $"Workflow '{detail.Definition.Id}' version '{detail.Definition.VersionId}' failed launch validation. {issues}");
    }

    private static void ValidateDefinitionStatus(
        WorkflowDefinition definition,
        WorkflowLaunchMode mode)
    {
        if (mode == WorkflowLaunchMode.Production &&
            definition.Status != WorkflowLifecycleStatus.Active)
        {
            throw new InvalidOperationException(
                $"Production workflow launches require an Active definition. Workflow '{definition.Id}' version '{definition.VersionId}' is '{definition.Status}'.");
        }
    }

    private static void ValidateRuntimePolicy(
        WorkflowDefinition definition,
        WorkflowRuntimeBackendDescriptor backend,
        WorkflowLaunchMode mode)
    {
        if (mode == WorkflowLaunchMode.Production &&
            definition.RuntimePolicy.RequireDurableProductionRuns &&
            !backend.IsDurable)
        {
            throw new InvalidOperationException(
                $"Workflow '{definition.Id}' requires a durable production backend, but '{backend.Kind}' is not durable.");
        }

        if (mode == WorkflowLaunchMode.Preview &&
            backend.Kind == WorkflowRuntimeBackendKind.InProcess &&
            !definition.RuntimePolicy.AllowInProcessPreviewRuns)
        {
            throw new InvalidOperationException(
                $"Workflow '{definition.Id}' does not allow in-process preview runs.");
        }
    }

}
