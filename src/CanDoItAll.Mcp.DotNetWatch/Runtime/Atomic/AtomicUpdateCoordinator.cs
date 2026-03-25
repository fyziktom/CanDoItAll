using System.Diagnostics;
using System.Text;
using CanDoItAll.Mcp.DotNetWatch.Configuration;
using CanDoItAll.Mcp.DotNetWatch.Health;
using CanDoItAll.Mcp.DotNetWatch.Runtime.Coordination;
using CanDoItAll.Mcp.DotNetWatch.Runtime.Events;
using CanDoItAll.Mcp.DotNetWatch.Security;

namespace CanDoItAll.Mcp.DotNetWatch.Runtime.Atomic;

public sealed class AtomicUpdateCoordinator(
    RuntimeConfiguration configuration,
    PathGuard pathGuard,
    EnvironmentOverlayFilter environmentOverlayFilter,
    AppRuntimeManager appRuntimeManager,
    HttpHealthProbe healthProbe,
    RuntimeSlotRegistry slotRegistry,
    RuntimeEndpointAllocator endpointAllocator,
    SessionEventJournal eventJournal,
    ILogger<AtomicUpdateCoordinator> logger)
{
    public async Task<AtomicUpdateData> UpdateAsync(
        string? logicalAppId,
        string? projectPath,
        string configurationName,
        string? framework,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?>? environmentOverlay,
        bool activateOnSuccess,
        bool keepPreviousRuntimeWarm,
        bool allowRollback,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var resolvedProjectPath = pathGuard.ResolveProjectPath(projectPath);
        var resolvedLogicalAppId = string.IsNullOrWhiteSpace(logicalAppId)
            ? Path.GetFileNameWithoutExtension(resolvedProjectPath)
            : logicalAppId.Trim();

        var slotState = slotRegistry.GetState(resolvedLogicalAppId);
        var targetSlotId = slotRegistry.SelectInactiveSlot(slotState);
        var transactionId = $"txn_{Guid.NewGuid():N}";
        var previousSession = appRuntimeManager.GetByLogicalAppId(resolvedLogicalAppId) ?? appRuntimeManager.GetActiveSession();
        var previousStatus = previousSession?.ToStatusData();
        var previousRevision = previousStatus?.Revision ?? slotState.App.ActiveRevision;
        var sourceSignature = ComputeSourceSignature(resolvedProjectPath);

        var transaction = new AtomicTransactionRecord(
            TransactionId: transactionId,
            LogicalAppId: resolvedLogicalAppId,
            SourceSignature: sourceSignature,
            TargetSlotId: targetSlotId,
            PreviousActiveSessionId: previousStatus?.SessionId ?? slotState.App.ActiveSessionId,
            PreviousActiveRevision: previousRevision,
            CandidateSessionId: null,
            CandidateRevision: null,
            State: AtomicTransactionState.PreparingCandidate,
            CreatedUtc: DateTimeOffset.UtcNow);
        slotRegistry.SaveTransaction(slotState, transaction);

        var payloadRoot = slotRegistry.GetSlotPayloadPath(slotState, targetSlotId);
        var artifactsRoot = slotRegistry.GetSlotArtifactsPath(slotState, targetSlotId);
        ResetDirectory(payloadRoot);
        ResetDirectory(artifactsRoot);

        try
        {
            await PublishCandidateAsync(
                resolvedProjectPath,
                configurationName,
                framework,
                payloadRoot,
                artifactsRoot,
                environmentOverlay,
                timeout,
                cancellationToken);
        }
        catch (Exception ex)
        {
            var failedPrepare = transaction with
            {
                State = AtomicTransactionState.FailedPrepare,
                FailureSummary = ex.Message
            };
            slotRegistry.SaveTransaction(slotState, failedPrepare);
            throw new ToolInvocationException("CandidatePrepareFailed", ex.Message, new { transactionId, targetSlotId });
        }

        var entryPath = ResolvePublishedDllEntryPath(resolvedProjectPath, payloadRoot);
        var endpointLease = endpointAllocator.Acquire(transactionId);
        var urls = new[] { $"http://127.0.0.1:{endpointLease.HttpPort}" };
        var healthUrls = urls.Select(static url => new Uri($"{url}/_dev/runtime", UriKind.Absolute)).ToArray();
        var filteredEnvironment = environmentOverlayFilter.Merge(
            defaults: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            requested: environmentOverlay,
            includePollingWatcher: false);
        var revision = slotRegistry.CreatePublishedRevision(resolvedLogicalAppId, targetSlotId, payloadRoot);
        var manifest = new SlotManifest(
            SlotId: targetSlotId,
            LogicalAppId: resolvedLogicalAppId,
            PublishHash: revision.Value,
            EntryPath: entryPath,
            WorkingDirectory: payloadRoot,
            HealthUrls: healthUrls.Select(static url => url.ToString()).ToArray(),
            CreatedUtc: DateTimeOffset.UtcNow)
        {
            ProjectPath = resolvedProjectPath
        };
        slotRegistry.SaveSlotManifest(slotState, manifest);

        var template = new AppStartTemplate(
            ProjectPath: resolvedProjectPath,
            WorkingDirectory: payloadRoot,
            Mode: AppRunMode.RunOnce,
            Configuration: configurationName,
            Framework: framework,
            LaunchProfile: null,
            Arguments: arguments,
            EnvironmentOverlay: filteredEnvironment,
            Urls: urls)
        {
            LogicalAppId = resolvedLogicalAppId,
            LaunchType = AppLaunchType.PublishedDll,
            LaneKind = RuntimeLaneKind.PublishedCandidate,
            EntryPath = entryPath,
            SlotId = targetSlotId,
            ActiveTransactionId = transactionId,
            InitialRevision = revision,
            HealthUrls = healthUrls,
            EndpointLeaseId = endpointLease.LeaseId
        };

        var (candidateSession, _) = await appRuntimeManager.StartAsync(template, reuseIfCompatible: false, AppStartConflictPolicy.Fail, cancellationToken);
        candidateSession.UpdateAtomicState(RuntimeLaneKind.PublishedCandidate, targetSlotId, transactionId, revision, rollbackAvailable: false);
        eventJournal.Append(resolvedLogicalAppId, candidateSession.SessionId, "candidate-prepared", $"Candidate session prepared in {targetSlotId}.", revision, transactionId, targetSlotId);

        if (!await WaitForHealthyAsync(candidateSession, timeout, cancellationToken))
        {
            await appRuntimeManager.StopAsync(candidateSession.SessionId, "Atomic candidate health failed.", force: true, CancellationToken.None);
            var failedHealth = transaction with
            {
                CandidateSessionId = candidateSession.SessionId,
                CandidateRevision = revision,
                State = AtomicTransactionState.FailedPrepare,
                FailureSummary = "Candidate runtime did not become healthy."
            };
            slotRegistry.SaveTransaction(slotState, failedHealth);
            throw new ToolInvocationException("CandidateHealthFailed", "Candidate runtime did not become healthy within timeout.", new { transactionId, targetSlotId });
        }

        transaction = transaction with
        {
            CandidateSessionId = candidateSession.SessionId,
            CandidateRevision = revision,
            CandidateReadyUtc = DateTimeOffset.UtcNow,
            State = AtomicTransactionState.CandidateReady
        };
        slotRegistry.SaveTransaction(slotState, transaction);
        eventJournal.Append(resolvedLogicalAppId, candidateSession.SessionId, "candidate-healthy", "Candidate runtime is healthy.", revision, transactionId, targetSlotId);

        var committed = false;
        var rollbackAvailable = allowRollback &&
                                keepPreviousRuntimeWarm &&
                                previousRevision is not null &&
                                !string.IsNullOrWhiteSpace(previousStatus?.SessionId ?? slotState.App.ActiveSessionId);
        if (activateOnSuccess)
        {
            try
            {
                committed = true;
                var logicalApp = new LogicalAppRecord(
                    LogicalAppId: resolvedLogicalAppId,
                    ActiveSessionId: candidateSession.SessionId,
                    ActiveRevision: revision,
                    PreviousSessionId: previousStatus?.SessionId ?? slotState.App.ActiveSessionId,
                    PreviousRevision: previousRevision,
                    CurrentSlotId: targetSlotId,
                    LastCommittedTransactionId: transactionId,
                    RollbackAvailable: rollbackAvailable);
                slotRegistry.SaveLogicalApp(slotState, logicalApp);
                slotRegistry.SaveSlotManifest(slotState, manifest with { LastActivatedUtc = DateTimeOffset.UtcNow });
                transaction = transaction with
                {
                    State = AtomicTransactionState.Committed,
                    CommittedUtc = DateTimeOffset.UtcNow
                };
                slotRegistry.SaveTransaction(slotState, transaction);
                candidateSession.UpdateAtomicState(RuntimeLaneKind.PublishedActive, targetSlotId, transactionId, revision, rollbackAvailable);
                appRuntimeManager.SetDefaultSession(candidateSession.SessionId);

                if (!keepPreviousRuntimeWarm &&
                    previousStatus is not null &&
                    !string.Equals(previousStatus.SessionId, candidateSession.SessionId, StringComparison.OrdinalIgnoreCase))
                {
                    await appRuntimeManager.StopAsync(previousStatus.SessionId, "Atomic commit replaced previous runtime.", force: false, CancellationToken.None);
                }

                eventJournal.Append(resolvedLogicalAppId, candidateSession.SessionId, "transaction-committed", "Candidate runtime committed.", revision, transactionId, targetSlotId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Atomic commit failed for logical app {LogicalAppId}", resolvedLogicalAppId);
                transaction = transaction with
                {
                    State = AtomicTransactionState.FailedCommit,
                    FailureSummary = ex.Message
                };
                slotRegistry.SaveTransaction(slotState, transaction);
                throw new ToolInvocationException("CommitFailed", ex.Message, new { transactionId, targetSlotId });
            }
        }

        return new AtomicUpdateData(
            TransactionId: transactionId,
            LogicalAppId: resolvedLogicalAppId,
            CandidateSessionId: candidateSession.SessionId,
            CandidateSlotId: targetSlotId,
            State: transaction.State.ToString(),
            CandidateRevision: revision,
            ActiveRevision: committed ? revision : previousRevision,
            ObservedUrls: urls,
            Committed: committed,
            RollbackAvailable: committed && rollbackAvailable);
    }

    public Task<AtomicStatusSnapshot> GetSnapshotAsync(string logicalAppId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(slotRegistry.GetSnapshot(logicalAppId));
    }

    public async Task<AtomicRollbackData> RollbackAsync(string? logicalAppId, string? transactionId, CancellationToken cancellationToken)
    {
        var resolvedLogicalAppId = string.IsNullOrWhiteSpace(logicalAppId)
            ? throw new ToolInvocationException("RollbackFailed", "logicalAppId is required when rolling back without an active committed logical app.")
            : logicalAppId.Trim();
        var slotState = slotRegistry.GetState(resolvedLogicalAppId);
        var app = slotState.App;

        if (!app.RollbackAvailable || string.IsNullOrWhiteSpace(app.PreviousSessionId) || app.PreviousRevision is null)
        {
            throw new ToolInvocationException("RollbackFailed", "No previous committed revision is available for rollback.", new { logicalAppId = resolvedLogicalAppId, transactionId });
        }

        var restoredSession = appRuntimeManager.GetById(app.PreviousSessionId)
            ?? throw new ToolInvocationException("RollbackFailed", "The previous committed session is no longer available.", new { logicalAppId = resolvedLogicalAppId, app.PreviousSessionId });

        var next = new LogicalAppRecord(
            LogicalAppId: resolvedLogicalAppId,
            ActiveSessionId: restoredSession.SessionId,
            ActiveRevision: app.PreviousRevision,
            PreviousSessionId: app.ActiveSessionId,
            PreviousRevision: app.ActiveRevision,
            CurrentSlotId: app.CurrentSlotId,
            LastCommittedTransactionId: transactionId ?? app.LastCommittedTransactionId,
            RollbackAvailable: app.ActiveRevision is not null && !string.IsNullOrWhiteSpace(app.ActiveSessionId));
        slotRegistry.SaveLogicalApp(slotState, next);

        if (!string.IsNullOrWhiteSpace(transactionId ?? app.LastCommittedTransactionId))
        {
            var existingTransaction = slotRegistry.ReadTransaction(slotState, transactionId ?? app.LastCommittedTransactionId);
            if (existingTransaction is not null)
            {
                slotRegistry.SaveTransaction(slotState, existingTransaction with
                {
                    State = AtomicTransactionState.RolledBack,
                    RolledBackUtc = DateTimeOffset.UtcNow
                });
            }
        }

        restoredSession.SetRollbackAvailable(next.RollbackAvailable);
        appRuntimeManager.SetDefaultSession(restoredSession.SessionId);

        if (!string.IsNullOrWhiteSpace(app.ActiveSessionId) &&
            !string.Equals(app.ActiveSessionId, restoredSession.SessionId, StringComparison.OrdinalIgnoreCase))
        {
            await appRuntimeManager.StopAsync(app.ActiveSessionId, "Atomic rollback restored previous runtime.", force: false, CancellationToken.None);
        }

        eventJournal.Append(resolvedLogicalAppId, restoredSession.SessionId, "rollback-committed", "Previous committed runtime restored.", app.PreviousRevision, transactionId, app.CurrentSlotId);

        return new AtomicRollbackData(
            LogicalAppId: resolvedLogicalAppId,
            TransactionId: transactionId ?? app.LastCommittedTransactionId ?? string.Empty,
            RestoredSessionId: restoredSession.SessionId,
            RestoredRevision: app.PreviousRevision,
            PreviousRevision: app.ActiveRevision,
            RollbackAvailable: next.RollbackAvailable);
    }

    private async Task PublishCandidateAsync(
        string projectPath,
        string configurationName,
        string? framework,
        string outputPath,
        string artifactsRoot,
        IReadOnlyDictionary<string, string?>? environmentOverlay,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        List<string> arguments =
        [
            "publish",
            projectPath,
            "--configuration",
            configurationName,
            "--output",
            outputPath,
            "--artifacts-path",
            artifactsRoot,
            "--property:UseAppHost=false"
        ];

        if (!string.IsNullOrWhiteSpace(framework))
        {
            arguments.Add("--framework");
            arguments.Add(framework);
        }

        var environment = environmentOverlayFilter.Merge(
            defaults: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["DOTNET_CLI_UI_LANGUAGE"] = "en",
                ["DOTNET_NOLOGO"] = "1",
                ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1",
                ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0",
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["DOTNET_ENVIRONMENT"] = "Development"
            },
            requested: environmentOverlay,
            includePollingWatcher: false);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = configuration.WorkspaceRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        foreach (var variable in environment)
        {
            process.StartInfo.Environment[variable.Key] = variable.Value;
        }

        var output = new StringBuilder();
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                output.AppendLine(args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                output.AppendLine(args.Data);
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Could not start dotnet publish.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        await process.WaitForExitAsync(timeoutCts.Token);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"dotnet publish failed with exit code {process.ExitCode}.{Environment.NewLine}{output}");
        }
    }

    private async Task<bool> WaitForHealthyAsync(AppSession session, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = session.ToStatusData();
            if (status.State is AppLifecycleState.Failed or AppLifecycleState.ExitedUnexpectedly or AppLifecycleState.Stopped)
            {
                return false;
            }

            var probe = await healthProbe.ProbeAsync(session.HealthUrls.Count > 0 ? session.HealthUrls : configuration.HealthUrls, cancellationToken);
            if (probe.IsReady)
            {
                if (!session.ConfirmsCurrentGeneration(probe))
                {
                    session.MarkHealthObserved(probe, "Waiting for the prepared candidate runtime to answer the health probe.");
                    await Task.Delay(configuration.DefaultPollInterval, cancellationToken);
                    continue;
                }

                session.MarkHealthy(probe);
                return true;
            }

            session.MarkHealthFailure(probe);
            await Task.Delay(configuration.DefaultPollInterval, cancellationToken);
        }

        return false;
    }

    private static void ResetDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        Directory.CreateDirectory(path);
    }

    private static string ResolvePublishedDllEntryPath(string projectPath, string payloadRoot)
    {
        var assemblyName = Path.GetFileNameWithoutExtension(projectPath);
        var candidate = Path.Combine(payloadRoot, $"{assemblyName}.dll");
        if (File.Exists(candidate))
        {
            return candidate;
        }

        var publishedDll = Directory.GetFiles(payloadRoot, "*.dll", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        return publishedDll ?? throw new InvalidOperationException($"Could not find the published entry assembly under '{payloadRoot}'.");
    }

    private static string ComputeSourceSignature(string projectPath)
    {
        var file = new FileInfo(projectPath);
        return $"{file.Name}:{file.LastWriteTimeUtc.Ticks}:{file.Length}";
    }
}
