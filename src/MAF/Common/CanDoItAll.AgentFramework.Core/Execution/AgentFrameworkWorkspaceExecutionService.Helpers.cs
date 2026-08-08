using CanDoItAll.AgentFramework.Models;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

using CanDoItAll.AgentFramework.Runtime.Abstractions;
namespace CanDoItAll.AgentFramework.Core;

internal sealed partial class AgentFrameworkWorkspaceExecutionService
{
    private sealed record AgentExecutionStartupAggregate(
        AgentExecutionPreparationBlueprint Blueprint,
        SandboxWorkspaceCatalogSnapshot CatalogSnapshot,
        AgentDefinition Agent,
        ProviderProfile Provider,
        ChatSessionRecord? Session,
        ExecutionRunRecord Run,
        ChatMessageRecord? UserMessage,
        IReadOnlyList<AgentRuntimeInputAttachment> InputAttachments);

    private sealed record ExecutionStateMutation(
        ExecutionRunRecord? Run = null,
        ChatSessionRecord? Session = null,
        IReadOnlyList<ExecutionApprovalRecord>? RunApprovals = null,
        AgentRunMetric? Metric = null,
        IReadOnlyList<ProviderUsageObservation>? UsageObservations = null,
        IReadOnlyList<ToolExecutionReceiptRecord>? ToolReceipts = null);

    private Task<SandboxWorkspaceExecutionState> UpdateExecutionStateAsync(
        Func<SandboxWorkspaceExecutionState, SandboxWorkspaceExecutionState> update,
        CancellationToken cancellationToken)
    {
        return store.UpdateExecutionAsync(update, cancellationToken);
    }

    private ISandboxWorkspaceExecutionRunStore? TryGetExecutionRunStore()
        => store as ISandboxWorkspaceExecutionRunStore;

    private ISandboxWorkspaceExecutionRunMutationStore?
        TryGetExecutionRunMutationStore()
        => store as ISandboxWorkspaceExecutionRunMutationStore;

    private async Task AppendExecutionLogAsync(
        Guid executionRunId,
        Guid agentId,
        Guid? chatSessionId,
        ExecutionState state,
        string phase,
        string message,
        CancellationToken cancellationToken)
    {
        var entry = new ExecutionLogEntry(
            Id: Guid.NewGuid(),
            AgentId: agentId,
            ChatSessionId: chatSessionId,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            State: state,
            Phase: phase,
            Message: message)
        {
            ExecutionRunId = executionRunId
        };

        if (TryGetExecutionRunMutationStore() is { } mutationStore)
        {
            var persistedDetail =
                await mutationStore.UpdateExecutionRunDetailAsync(
                    executionRunId,
                    currentDetail =>
                    {
                        var updatedRun = UpdateRunProgressFromLog(
                            currentDetail.Run,
                            entry);
                        var updatedSession =
                            currentDetail.ChatSession is null
                                ? null
                                : UpdateChatSessionProgressFromRun(
                                    currentDetail.ChatSession,
                                    updatedRun);
                        return CreateExecutionRunDetail(
                            updatedRun,
                            updatedSession,
                            InsertExecutionLogEntry(
                                currentDetail.ExecutionLog,
                                entry),
                            currentDetail.Metrics,
                            currentDetail.UsageObservations,
                            currentDetail.Approvals,
                            currentDetail.Artifacts,
                            currentDetail.Checkpoints,
                            currentDetail.ToolReceipts);
                    },
                    cancellationToken);

            NotifyExecutionUpdated(entry);
            await executionEventSink.PublishAsync(
                CreateExecutionEvent(persistedDetail.Run, entry),
                cancellationToken);
            return;
        }

        if (TryGetExecutionRunStore() is { } executionRunStore)
        {
            var currentDetail = await LoadExecutionRunDetailAsync(executionRunId, cancellationToken);
            var updatedRun = UpdateRunProgressFromLog(currentDetail.Run, entry);
            var updatedSession = currentDetail.ChatSession is null
                ? null
                : UpdateChatSessionProgressFromRun(currentDetail.ChatSession, updatedRun);
            var persistedDetail = await executionRunStore.SaveExecutionRunDetailAsync(
                CreateExecutionRunDetail(
                    updatedRun,
                    updatedSession,
                    InsertExecutionLogEntry(currentDetail.ExecutionLog, entry),
                    currentDetail.Metrics,
                    currentDetail.UsageObservations,
                    currentDetail.Approvals,
                    currentDetail.Artifacts,
                    currentDetail.Checkpoints,
                    currentDetail.ToolReceipts),
                cancellationToken);

            NotifyExecutionUpdated(entry);
            await executionEventSink.PublishAsync(CreateExecutionEvent(persistedDetail.Run, entry), cancellationToken);
            return;
        }

        var persistedExecutionState = await UpdateExecutionStateAsync(executionState => executionState with
        {
            ExecutionRuns = ReplaceExecutionRunProgress(executionState.ExecutionRuns, executionRunId, entry),
            ChatSessions = ReplaceChatSessionProgress(executionState.ChatSessions, executionState.ExecutionRuns, executionRunId, entry),
            ExecutionLog = InsertExecutionLogEntry(executionState.ExecutionLog, entry)
        }, cancellationToken);

        NotifyExecutionUpdated(entry);

        var run = persistedExecutionState.ExecutionRuns.FirstOrDefault(item => item.Id == executionRunId);
        if (run is null)
        {
            return;
        }

        await executionEventSink.PublishAsync(CreateExecutionEvent(run, entry), cancellationToken);
    }

    private async Task PersistExecutionMutationAsync(
        ExecutionStateMutation mutation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        if (mutation.Run is not null &&
            TryGetExecutionRunMutationStore() is { } mutationStore)
        {
            await mutationStore.UpdateExecutionRunDetailAsync(
                mutation.Run.Id,
                currentDetail =>
                {
                    var chatSession = mutation.Run.ChatSessionId.HasValue
                        ? mutation.Session ?? currentDetail.ChatSession
                        : null;
                    return CreateExecutionRunDetail(
                        mutation.Run,
                        chatSession,
                        currentDetail.ExecutionLog,
                        mutation.Metric is null
                            ? currentDetail.Metrics
                            : InsertMetric(
                                currentDetail.Metrics,
                                mutation.Metric),
                        mutation.UsageObservations is null
                            ? currentDetail.UsageObservations
                            : InsertUsageObservations(
                                currentDetail.UsageObservations,
                                mutation.UsageObservations),
                        mutation.RunApprovals ??
                        currentDetail.Approvals,
                        currentDetail.Artifacts,
                        currentDetail.Checkpoints,
                        mutation.ToolReceipts is null
                            ? currentDetail.ToolReceipts
                            : InsertToolReceipts(
                                currentDetail.ToolReceipts,
                                mutation.ToolReceipts));
                },
                cancellationToken);
            return;
        }

        if (mutation.Run is not null &&
            TryGetExecutionRunStore() is { } executionRunStore)
        {
            var currentDetail = await executionRunStore.GetExecutionRunDetailAsync(mutation.Run.Id, cancellationToken);
            var chatSession = mutation.Run.ChatSessionId.HasValue
                ? mutation.Session ?? currentDetail?.ChatSession
                : null;

            await executionRunStore.SaveExecutionRunDetailAsync(
                CreateExecutionRunDetail(
                    mutation.Run,
                    chatSession,
                    currentDetail?.ExecutionLog ?? [],
                    mutation.Metric is null
                        ? currentDetail?.Metrics ?? []
                        : InsertMetric(currentDetail?.Metrics ?? [], mutation.Metric),
                    mutation.UsageObservations is null
                        ? currentDetail?.UsageObservations ?? []
                        : InsertUsageObservations(currentDetail?.UsageObservations ?? [], mutation.UsageObservations),
                    mutation.RunApprovals ?? currentDetail?.Approvals ?? [],
                    currentDetail?.Artifacts ?? [],
                    currentDetail?.Checkpoints ?? [],
                    mutation.ToolReceipts is null
                        ? currentDetail?.ToolReceipts ?? []
                        : InsertToolReceipts(currentDetail?.ToolReceipts ?? [], mutation.ToolReceipts)),
                cancellationToken);
            return;
        }

        await UpdateExecutionStateAsync(executionState => executionState with
        {
            ExecutionRuns = mutation.Run is null
                ? executionState.ExecutionRuns
                : ReplaceExecutionRun(executionState.ExecutionRuns, mutation.Run),
            ChatSessions = mutation.Session is null
                ? executionState.ChatSessions
                : ReplaceChatSession(executionState.ChatSessions, mutation.Session),
            ExecutionApprovals = mutation.RunApprovals is null
                ? executionState.ExecutionApprovals
                : ReplaceRunApprovals(
                    executionState.ExecutionApprovals,
                    mutation.Run?.Id ?? throw new InvalidOperationException("Run approvals require an execution run."),
                    mutation.RunApprovals),
            Metrics = mutation.Metric is null
                ? executionState.Metrics
                : InsertMetric(executionState.Metrics, mutation.Metric),
            ProviderUsageObservations = mutation.UsageObservations is null
                ? executionState.ProviderUsageObservations
                : InsertUsageObservations(executionState.ProviderUsageObservations, mutation.UsageObservations),
            ToolExecutionReceipts = mutation.ToolReceipts is null
                ? executionState.ToolExecutionReceipts
                : InsertToolReceipts(executionState.ToolExecutionReceipts, mutation.ToolReceipts)
        }, cancellationToken);
    }

    private sealed record TerminalFailurePersistenceResult(
        bool TerminalRunPersisted,
        Exception? ExecutionLogFailure);

    private async Task<TerminalFailurePersistenceResult> PersistFailedExecutionRunAsync(
        ExecutionRunRecord failedRun,
        ChatSessionRecord? session,
        AgentRunMetric metric,
        IReadOnlyList<ProviderUsageObservation> usageObservations,
        IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts,
        Guid agentId,
        string phase,
        string logMessage,
        CancellationToken cancellationToken)
    {
        var updatedSession = session is null
            ? null
            : ChatSessionRuntimeCompatibilityAdapter.ClearCompatibility(
                session,
                failedRun.UpdatedAtUtc,
                failedRun.Id);
        await PersistExecutionMutationAsync(
            new ExecutionStateMutation(
                Run: failedRun,
                Session: updatedSession,
                Metric: metric,
                UsageObservations: usageObservations,
                ToolReceipts: toolReceipts),
            cancellationToken);
        AgentFrameworkTelemetry.RecordRunOutcome(failedRun);
        try
        {
            await AppendExecutionLogAsync(
                failedRun.Id,
                agentId,
                failedRun.ChatSessionId,
                ExecutionState.Failed,
                phase,
                logMessage,
                cancellationToken);
            return new TerminalFailurePersistenceResult(
                TerminalRunPersisted: true,
                ExecutionLogFailure: null);
        }
        catch (Exception exception)
        {
            return new TerminalFailurePersistenceResult(
                TerminalRunPersisted: true,
                ExecutionLogFailure: exception);
        }
    }

    private async Task PersistNewExecutionRunAsync(
        ExecutionRunRecord run,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (TryGetExecutionRunStore() is { } executionRunStore)
        {
            await executionRunStore.SaveExecutionRunDetailAsync(
                CreateExecutionRunDetail(
                    run,
                    session: null,
                    executionLog: [],
                    metrics: [],
                    usageObservations: [],
                    approvals: [],
                    artifacts: [],
                    checkpoints: [],
                    toolReceipts: []),
                cancellationToken);
            return;
        }

        await UpdateExecutionStateAsync(
            executionState => executionState with
            {
                ExecutionRuns = ReplaceExecutionRun(
                    executionState.ExecutionRuns,
                    run)
            },
            cancellationToken);
    }

    private async Task SaveChatSessionAsync(
        ChatSessionRecord session,
        CancellationToken cancellationToken)
    {
        if (store is ISandboxWorkspaceChatSessionStore chatSessionStore)
        {
            await chatSessionStore.UpdateChatSessionAsync(session, cancellationToken);
            return;
        }

        await PersistExecutionMutationAsync(new ExecutionStateMutation(Session: session), cancellationToken);
    }

    private async Task<ExecutionRunDetail> LoadExecutionRunDetailAsync(
        Guid executionRunId,
        CancellationToken cancellationToken)
    {
        if (TryGetExecutionRunStore() is { } executionRunStore)
        {
            return await executionRunStore.GetExecutionRunDetailAsync(executionRunId, cancellationToken)
                ?? throw new InvalidOperationException("Execution run was not found.");
        }

        var executionState = await store.LoadExecutionAsync(cancellationToken);
        var run = executionState.ExecutionRuns.FirstOrDefault(item => item.Id == executionRunId)
            ?? throw new InvalidOperationException("Execution run was not found.");
        var session = run.ChatSessionId.HasValue
            ? executionState.ChatSessions.FirstOrDefault(item => item.Id == run.ChatSessionId.Value)
            : null;

        return CreateExecutionRunDetail(
            run,
            session,
            executionState.ExecutionLog.Where(item => item.ExecutionRunId == executionRunId).ToList(),
            executionState.Metrics.Where(item => item.ExecutionRunId == executionRunId).ToList(),
            executionState.ProviderUsageObservations.Where(item => item.ExecutionRunId == executionRunId).ToList(),
            executionState.ExecutionApprovals.Where(item => item.ExecutionRunId == executionRunId).ToList(),
            executionState.ExecutionArtifacts.Where(item => item.ExecutionRunId == executionRunId).ToList(),
            executionState.ExecutionWorkflowCheckpoints.Where(item => item.ExecutionRunId == executionRunId).ToList(),
            executionState.ToolExecutionReceipts.Where(item => item.ExecutionRunId == executionRunId).ToList());
    }

    private static ExecutionRunDetail CreateExecutionRunDetail(
        ExecutionRunRecord run,
        ChatSessionRecord? session,
        IReadOnlyList<ExecutionLogEntry> executionLog,
        IReadOnlyList<AgentRunMetric> metrics,
        IReadOnlyList<ProviderUsageObservation> usageObservations,
        IReadOnlyList<ExecutionApprovalRecord> approvals,
        IReadOnlyList<ExecutionArtifactRecord> artifacts,
        IReadOnlyList<ExecutionWorkflowCheckpointRecord> checkpoints,
        IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts)
    {
        return new ExecutionRunDetail(
            Run: run,
            ChatSession: session,
            ExecutionLog: executionLog.OrderByDescending(item => item.CreatedAtUtc).ToList(),
            Metrics: metrics.OrderByDescending(item => item.CreatedAtUtc).ToList())
        {
            UsageObservations = usageObservations.OrderByDescending(item => item.CreatedAtUtc).ToList(),
            Approvals = approvals.OrderByDescending(item => item.DecidedAtUtc ?? item.RequestedAtUtc).ToList(),
            Artifacts = artifacts.OrderByDescending(item => item.CreatedAtUtc).ToList(),
            Checkpoints = checkpoints.OrderByDescending(item => item.CapturedAtUtc).ToList(),
            ToolReceipts = toolReceipts.OrderByDescending(item => item.CompletedAtUtc).ToList()
        };
    }

    private async Task<AgentExecutionStartupAggregate> BeginChatBackedRunAsync(
        AgentExecutionPreparationSnapshot preparation,
        ProviderProfile provider,
        Guid? chatSessionId,
        string prompt,
        ExecutionInvocationContext context,
        bool autoApprovePendingToolCalls,
        AgentStructuredOutputContract? structuredOutput,
        PreparedAgentJsonSchemaOutputContract? jsonSchemaOutput,
        IReadOnlyList<AgentRuntimeInputAttachment> inputAttachments,
        AgentExecutionOperationId initialActivityOperationId,
        CancellationToken cancellationToken)
    {
        if (store is ISandboxWorkspaceChatRunStartStore chatRunStartStore)
        {
            return await BeginChatBackedRunWithSplitStoreAsync(
                chatRunStartStore,
                preparation,
                provider,
                chatSessionId,
                prompt,
                context,
                autoApprovePendingToolCalls,
                structuredOutput,
                jsonSchemaOutput,
                inputAttachments,
                initialActivityOperationId,
                cancellationToken);
        }

        if (store is ISandboxWorkspaceExecutionRunStore)
        {
            throw new NotSupportedException(
                "A split execution store must implement atomic chat-backed run start.");
        }

        var runtimeModel = ResolveEffectiveManagedSeedModel(
            preparation.Blueprint.Agent,
            provider);
        AgentExecutionStartupAggregate? prepared = null;

        await store.UpdateWorkspaceAsync(document =>
        {
            var catalog = document.ToCatalog();
            var catalogSnapshot = new SandboxWorkspaceCatalogSnapshot(
                catalog,
                catalog.CatalogDataRevision);
            EnsurePreparationCurrentForUse(
                preparation.Blueprint,
                catalogSnapshot);
            var executionState = document.ToExecutionState();
            var agent = EnsureAgentExists(
                catalog,
                preparation.Blueprint.Agent.Id);
            if (!agent.ProviderProfileId.HasValue)
            {
                throw new InvalidOperationException("The selected agent does not have a provider profile.");
            }

            agent = CreateProviderCompatibleRuntimeAgent(
                agent,
                provider,
                runtimeModel);

            var existingSession = chatSessionId.HasValue
                ? EnsureAgentOwnsSession(
                    executionState,
                    agent.Id,
                    chatSessionId.Value)
                : null;

            if (existingSession is not null && TryGetBlockingSessionRun(executionState, existingSession, out _))
            {
                throw new InvalidOperationException(DescribeSessionBusyMessage(executionState, existingSession));
            }

            var now = DateTimeOffset.UtcNow;
            var userMessage = new ChatMessageRecord(
                Id: Guid.NewGuid(),
                Role: ChatMessageRole.User,
                Content: prompt,
                CreatedAtUtc: now,
                TokenEstimate: EstimateTokens(prompt));
            var session = existingSession ?? new ChatSessionRecord(
                Id: Guid.NewGuid(),
                AgentId: agent.Id,
                Title: CreateSessionTitle(prompt),
                CreatedAtUtc: now,
                UpdatedAtUtc: now,
                RuntimeSessionKey: string.Empty,
                SerializedSessionStateJson: null,
                Messages: [],
                PendingApprovals: []);
            var run = CreatePreparingRun(
                agent,
                provider,
                session.Id,
                session.Title,
                context,
                prompt,
                now,
                autoApprovePendingToolCalls,
                initialActivityOperationId,
                structuredOutput,
                jsonSchemaOutput);
            var updatedSession = ChatSessionRuntimeCompatibilityAdapter.ClearCompatibility(
                session with
                {
                    Title = string.IsNullOrWhiteSpace(session.Title) ? CreateSessionTitle(prompt) : session.Title,
                    UpdatedAtUtc = now,
                    Messages = session.Messages.Append(userMessage).ToList()
                },
                now,
                run.Id);

            prepared = new AgentExecutionStartupAggregate(
                preparation.Blueprint,
                catalogSnapshot,
                agent,
                provider,
                updatedSession,
                run,
                userMessage,
                inputAttachments);

            return SandboxWorkspaceDocument.Combine(
                catalog,
                executionState with
                {
                    ChatSessions = ReplaceChatSession(executionState.ChatSessions, updatedSession),
                    ExecutionRuns = ReplaceExecutionRun(executionState.ExecutionRuns, run)
                });
        }, cancellationToken);

        return prepared ?? throw new InvalidOperationException("Chat-backed execution run start could not be prepared.");
    }

    private async Task<AgentExecutionStartupAggregate>
        BeginChatBackedRunWithSplitStoreAsync(
        ISandboxWorkspaceChatRunStartStore chatRunStartStore,
        AgentExecutionPreparationSnapshot preparation,
        ProviderProfile provider,
        Guid? chatSessionId,
        string prompt,
        ExecutionInvocationContext context,
        bool autoApprovePendingToolCalls,
        AgentStructuredOutputContract? structuredOutput,
        PreparedAgentJsonSchemaOutputContract? jsonSchemaOutput,
        IReadOnlyList<AgentRuntimeInputAttachment> inputAttachments,
        AgentExecutionOperationId initialActivityOperationId,
        CancellationToken cancellationToken)
    {
        EnsurePreparationCurrentForUse(
            preparation.Blueprint,
            preparation.CatalogSnapshot);
        var blueprint = preparation.Blueprint;
        var runtimeModel = ResolveEffectiveManagedSeedModel(
            blueprint.Agent,
            provider);
        var result = await chatRunStartStore.BeginChatBackedRunAsync(
            new ChatBackedRunStartRequest(
                blueprint.Agent.Id,
                blueprint.Provider.Id,
                blueprint.Request.Version.CatalogRevision,
                chatSessionId),
            current =>
            {
                EnsurePreparationCurrentForUse(
                    blueprint,
                    current.CatalogSnapshot);
                var now = DateTimeOffset.UtcNow;
                var runtimeAgent = CreateProviderCompatibleRuntimeAgent(
                    current.Agent,
                    provider,
                    runtimeModel);
                var userMessage = new ChatMessageRecord(
                    Id: Guid.NewGuid(),
                    Role: ChatMessageRole.User,
                    Content: prompt,
                    CreatedAtUtc: now,
                    TokenEstimate: EstimateTokens(prompt));
                var session = current.Session ?? new ChatSessionRecord(
                    Id: Guid.NewGuid(),
                    AgentId: runtimeAgent.Id,
                    Title: CreateSessionTitle(prompt),
                    CreatedAtUtc: now,
                    UpdatedAtUtc: now,
                    RuntimeSessionKey: string.Empty,
                    SerializedSessionStateJson: null,
                    Messages: [],
                    PendingApprovals: []);
                var run = CreatePreparingRun(
                    runtimeAgent,
                    provider,
                    session.Id,
                    session.Title,
                    context,
                    prompt,
                    now,
                    autoApprovePendingToolCalls,
                    initialActivityOperationId,
                    structuredOutput,
                    jsonSchemaOutput);
                var updatedSession =
                    ChatSessionRuntimeCompatibilityAdapter.ClearCompatibility(
                        session with
                        {
                            Title = string.IsNullOrWhiteSpace(session.Title)
                                ? CreateSessionTitle(prompt)
                                : session.Title,
                            UpdatedAtUtc = now,
                            Messages = session.Messages
                                .Append(userMessage)
                                .ToList()
                        },
                        now,
                        run.Id);

                return new ChatBackedRunStartMutation(
                    CreateExecutionRunDetail(
                        run,
                        updatedSession,
                        [],
                        [],
                        [],
                        [],
                        [],
                        [],
                        []),
                    userMessage);
            },
            cancellationToken).ConfigureAwait(false);

        if (result is ChatBackedRunBlocked blocked)
        {
            throw new InvalidOperationException(
                DescribeSessionBusyMessage(blocked.BlockingRun));
        }

        var started = (ChatBackedRunStarted)result;
        var persistedSession = started.Detail.ChatSession
            ?? throw new InvalidOperationException(
                "Atomic chat-backed run start did not return its persisted session.");
        var persistedAgent = CreateProviderCompatibleRuntimeAgent(
            started.Agent,
            provider,
            runtimeModel);

        return new AgentExecutionStartupAggregate(
            blueprint,
            started.CatalogSnapshot,
            persistedAgent,
            provider,
            persistedSession,
            started.Detail.Run,
            started.UserMessage,
            inputAttachments);
    }

    private void EnsurePreparationCurrentForUse(
        AgentExecutionPreparationBlueprint blueprint,
        SandboxWorkspaceCatalogSnapshot catalogSnapshot)
    {
        var validation = executionPreparationService.ValidateForUse(
            blueprint,
            catalogSnapshot);
        if (validation != AgentExecutionPreparationUseValidation.Current)
        {
            throw new AgentExecutionPreparationStaleException(
                blueprint.Request.Key,
                validation);
        }
    }

    private static string CreateSessionTitle(string prompt)
    {
        var cleaned = prompt.Trim();
        return cleaned.Length <= 48
            ? cleaned
            : $"{cleaned[..45]}...";
    }

    private static string NormalizeChatSessionTitle(string title)
    {
        var normalized = string.Join(
            ' ',
            title.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Thread title is required.");
        }

        const int maxLength = 96;
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength].TrimEnd();
    }

    private ExecutionRunRecord CreatePreparingRun(
        AgentDefinition agent,
        ProviderProfile provider,
        Guid? chatSessionId,
        string title,
        ExecutionInvocationContext context,
        string prompt,
        DateTimeOffset now,
        bool autoApprovePendingToolCalls,
        AgentExecutionOperationId initialActivityOperationId,
        AgentStructuredOutputContract? structuredOutput = null,
        PreparedAgentJsonSchemaOutputContract? jsonSchemaOutput = null)
    {
        var sourceKind = string.IsNullOrWhiteSpace(context.SourceKind)
            ? ExecutionInvocationContext.Empty.SourceKind
            : context.SourceKind;
        var sourceId = string.IsNullOrWhiteSpace(context.SourceId) && string.Equals(sourceKind, "chat-session", StringComparison.OrdinalIgnoreCase)
            ? chatSessionId?.ToString("N") ?? string.Empty
            : context.SourceId ?? string.Empty;
        var metadataJson = ExecutionInvocationMetadata.Build(context.MetadataJson, context.Policy);
        if (ShouldGroundPromptExternalTargetAliases(sourceKind, context))
        {
            metadataJson = ExecutionInvocationMetadata.GroundPromptExternalTargetAliases(
                metadataJson,
                prompt,
                AgentWorkspaceToolAccessMetadata.Read(agent.ConfigurationJson));
        }

        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: agent.Id,
            ChatSessionId: chatSessionId,
            Title: string.IsNullOrWhiteSpace(title) ? CreateSessionTitle(prompt) : title,
            SourceKind: sourceKind,
            SourceId: sourceId,
            CorrelationId: context.CorrelationId ?? string.Empty,
            CausationId: context.CausationId ?? string.Empty,
            RequestedBy: context.RequestedBy ?? string.Empty,
            RequestedByKind: context.RequestedByKind ?? string.Empty,
            MetadataJson: metadataJson,
            InputSummary: CreateExecutionSummary(prompt),
            ResultSummary: string.Empty,
            ProviderName: provider.Name,
            Model: agent.Model,
            State: ExecutionState.Preparing,
            Outcome: null,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: now,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: [],
            AutoApprovePendingToolCalls: autoApprovePendingToolCalls,
            ProcessRunId: context.ProcessRunId ?? string.Empty,
            ProcessStepId: context.ProcessStepId ?? string.Empty,
            SchedulerRunId: context.SchedulerRunId ?? string.Empty,
            MessageId: context.MessageId ?? string.Empty,
            Revision: 1L,
            StructuredOutputContractKey: structuredOutput?.ContractKey ??
                                         (jsonSchemaOutput is null
                                             ? string.Empty
                                             : $"{jsonSchemaOutput.Kind}:{jsonSchemaOutput.Version}:{jsonSchemaOutput.Name}"),
            StructuredOutputTypeName: structuredOutput?.OutputType.AssemblyQualifiedName ?? string.Empty,
            StructuredOutputSchemaName: structuredOutput?.SchemaName ?? jsonSchemaOutput?.Name ?? string.Empty,
            StructuredOutputSchemaDescription: structuredOutput?.SchemaDescription ?? string.Empty,
            ProviderProfileId: provider.Id,
            StructuredOutputJsonSchema: jsonSchemaOutput?.SchemaJson ?? string.Empty,
            StructuredOutputSchemaHash: jsonSchemaOutput?.SchemaHash ?? string.Empty,
            StructuredOutputSchemaVersion: jsonSchemaOutput?.Version ?? string.Empty,
            StructuredOutputSchemaStrict: jsonSchemaOutput?.Strict ?? false)
        {
            InitialActivityOperationId = initialActivityOperationId
        };
    }

    private static void EnsureRequiredActivityOperationId(
        AgentExecutionOperationId operationId,
        string parameterName)
    {
        if (operationId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "An activity operation id is required.",
                parameterName);
        }
    }

    private static bool ShouldGroundPromptExternalTargetAliases(
        string sourceKind,
        ExecutionInvocationContext context)
    {
        return !string.Equals(sourceKind, "process-step", StringComparison.OrdinalIgnoreCase) &&
               string.IsNullOrWhiteSpace(context.ProcessRunId) &&
               string.IsNullOrWhiteSpace(context.ProcessStepId);
    }

    private static AgentDefinition EnsureAgentExists(
        SandboxWorkspaceCatalog catalog,
        Guid agentId)
    {
        return catalog.Agents.FirstOrDefault(item => item.Id == agentId)
            ?? throw new InvalidOperationException($"Agent '{agentId:N}' was not found.");
    }

    private async Task<ProviderProfile> ResolveFailureProviderAsync(
        ProviderProfile fallbackProvider,
        Exception exception)
    {
        var identity = ResolveProviderFailureIdentity(exception);
        if (identity is null)
        {
            return fallbackProvider;
        }

        if (identity.ProviderProfileId == fallbackProvider.Id)
        {
            return fallbackProvider;
        }

        try
        {
            var resolvedProvider = await providerSource
                .GetProviderAsync(identity.ProviderProfileId, CancellationToken.None)
                .ConfigureAwait(false);
            if (resolvedProvider is not null)
            {
                return resolvedProvider;
            }
        }
        catch (Exception resolutionException)
        {
            logger.LogWarning(
                "Failed to resolve the provider profile that produced a runtime failure. ProviderProfileId={ProviderProfileId}, FailureType={FailureType}.",
                identity.ProviderProfileId,
                resolutionException.GetType().FullName ?? resolutionException.GetType().Name);
        }

        return fallbackProvider with
        {
            Id = identity.ProviderProfileId,
            Name = string.IsNullOrWhiteSpace(identity.ProviderName)
                ? $"provider profile {identity.ProviderProfileId:D}"
                : identity.ProviderName,
            Kind = identity.ProviderKind,
            Transport = identity.Transport,
            DefaultModel = identity.Model,
            ModelPrices = []
        };
    }

    private static string ResolveFailureProviderModel(
        string fallbackModel,
        Exception exception)
        => ResolveProviderFailureIdentity(exception)?.Model ?? fallbackModel;

    private static AgentRuntimeProviderFailureIdentity? ResolveProviderFailureIdentity(
        Exception exception)
    {
        var current = exception;
        for (var depth = 0; current is not null && depth < 16; depth++)
        {
            if (current is AgentRuntimeUsageException { ProviderFailureIdentity: { } identity })
            {
                return identity;
            }

            if (current is AggregateException)
            {
                return null;
            }

            current = current.InnerException;
        }

        return null;
    }

    private async Task<ProviderProfile> ResolveProviderForAgentAsync(
        AgentDefinition agent,
        SandboxWorkspaceCatalog? catalog,
        CancellationToken cancellationToken)
    {
        if (!agent.ProviderProfileId.HasValue)
        {
            throw new InvalidOperationException("The selected agent does not have a provider profile.");
        }

        var registryProvider = await providerSource.GetProviderAsync(agent.ProviderProfileId.Value, cancellationToken);
        var catalogShadowProvider = catalog is not null &&
                                    providerSource is ICatalogShadowProviderProfileRegistry catalogShadowProviderRegistry
            ? catalogShadowProviderRegistry.TryGetProviderFromCatalog(catalog, agent.ProviderProfileId.Value)
            : null;

        var preferredProvider = ManagedSeedProviderFallbacks.ResolvePreferredProvider(
            agent,
            registryProvider,
            catalogShadowProvider);
        return ApplyCredentialAwareManagedSeedFallback(agent, preferredProvider);
    }

    private ProviderProfile ApplyCredentialAwareManagedSeedFallback(
        AgentDefinition agent,
        ProviderProfile provider)
    {
        if (!ManagedSeedProviderFallbacks.ShouldUseFallback(agent, provider))
        {
            return provider;
        }

        return ManagedSeedProviderFallbacks.Apply(
            agent,
            provider,
            ResolveOpenAiCredentialOverride(provider));
    }

    private string ResolveEffectiveManagedSeedModel(
        AgentDefinition agent,
        ProviderProfile provider)
    {
        var openAiCredentialOverride =
            ManagedSeedProviderFallbacks.ShouldUseFallback(agent, provider)
                ? ResolveOpenAiCredentialOverride(provider)
                : null;
        var resolvedModel = ManagedSeedProviderFallbacks.ResolveModel(
            agent,
            provider,
            openAiCredentialOverride);
        return ResolveProviderCompatibleRuntimeModel(agent, provider, resolvedModel);
    }

    private string ResolveOpenAiCredentialOverride(
        ProviderProfile provider)
    {
        if (provider.Kind is not (ProviderKind.OpenAi or ProviderKind.AzureOpenAi))
        {
            return "resolved";
        }

        return providerCredentialResolver.Resolve(provider).IsResolved
            ? "resolved"
            : string.Empty;
    }

    private static ChatSessionRecord EnsureAgentOwnsSession(
        SandboxWorkspaceExecutionState executionState,
        Guid agentId,
        Guid chatSessionId)
    {
        var session = executionState.ChatSessions.FirstOrDefault(item => item.Id == chatSessionId)
            ?? throw new InvalidOperationException("Chat session was not found.");
        if (session.AgentId != agentId)
        {
            throw new InvalidOperationException(
                $"Chat session '{chatSessionId:N}' does not belong to agent '{agentId:N}'.");
        }

        return session;
    }

    private static ChatSessionRecord EnsureAgentOwnsSession(
        ChatSessionRecord? session,
        Guid agentId,
        Guid chatSessionId)
    {
        if (session is null)
        {
            throw new InvalidOperationException("Chat session was not found.");
        }

        if (session.AgentId != agentId)
        {
            throw new InvalidOperationException(
                $"Chat session '{chatSessionId:N}' does not belong to agent '{agentId:N}'.");
        }

        return session;
    }

    private static int EstimateTokens(string value)
    {
        return Math.Max(1, value.Length / 4);
    }

    private static ExecutionEvent CreateExecutionEvent(
        ExecutionRunRecord run,
        ExecutionLogEntry entry)
    {
        var activity = Activity.Current;
        return new ExecutionEvent(
            EventId: entry.Id,
            ExecutionRunId: run.Id,
            AgentId: run.AgentId,
            ChatSessionId: run.ChatSessionId,
            SourceKind: run.SourceKind,
            SourceId: run.SourceId,
            CorrelationId: run.CorrelationId,
            CausationId: run.CausationId,
            RequestedBy: run.RequestedBy,
            RequestedByKind: run.RequestedByKind,
            MetadataJson: run.MetadataJson,
            State: entry.State,
            Phase: entry.Phase,
            Message: entry.Message,
            OccurredAtUtc: entry.CreatedAtUtc,
            Outcome: run.Outcome,
            ProcessRunId: run.ProcessRunId,
            ProcessStepId: run.ProcessStepId,
            SchedulerRunId: run.SchedulerRunId,
            MessageId: run.MessageId,
            TraceId: activity?.TraceId.ToString() ?? string.Empty,
            SpanId: activity?.SpanId.ToString() ?? string.Empty);
    }

    private static IReadOnlyList<ExecutionRunRecord> ReplaceExecutionRun(
        IReadOnlyList<ExecutionRunRecord> runs,
        ExecutionRunRecord run)
    {
        return InsertOrReplaceDescending(
            runs,
            run,
            item => item.Id == run.Id,
            item => item.UpdatedAtUtc);
    }

    private static IReadOnlyList<ChatSessionRecord> ReplaceChatSession(
        IReadOnlyList<ChatSessionRecord> sessions,
        ChatSessionRecord session)
    {
        return InsertOrReplaceDescending(
            sessions,
            session,
            item => item.Id == session.Id,
            item => item.UpdatedAtUtc);
    }

    private static IReadOnlyList<ExecutionApprovalRecord> ReplaceRunApprovals(
        IReadOnlyList<ExecutionApprovalRecord> approvals,
        Guid executionRunId,
        IReadOnlyList<ExecutionApprovalRecord> runApprovals)
    {
        return approvals
            .Where(item => item.ExecutionRunId != executionRunId)
            .Concat(runApprovals)
            .OrderByDescending(item => item.DecidedAtUtc ?? item.RequestedAtUtc)
            .ToList();
    }

    private static IReadOnlyList<ExecutionLogEntry> InsertExecutionLogEntry(
        IReadOnlyList<ExecutionLogEntry> executionLog,
        ExecutionLogEntry entry)
    {
        return InsertOrReplaceDescending(
            executionLog,
            entry,
            item => item.Id == entry.Id,
            item => item.CreatedAtUtc);
    }

    private static IReadOnlyList<ExecutionRunRecord> ReplaceExecutionRunProgress(
        IReadOnlyList<ExecutionRunRecord> runs,
        Guid executionRunId,
        ExecutionLogEntry entry)
    {
        var currentRun = runs.FirstOrDefault(item => item.Id == executionRunId);
        if (currentRun is null)
        {
            return runs;
        }

        return ReplaceExecutionRun(runs, UpdateRunProgressFromLog(currentRun, entry));
    }

    private static IReadOnlyList<ChatSessionRecord> ReplaceChatSessionProgress(
        IReadOnlyList<ChatSessionRecord> sessions,
        IReadOnlyList<ExecutionRunRecord> runs,
        Guid executionRunId,
        ExecutionLogEntry entry)
    {
        var currentRun = runs.FirstOrDefault(item => item.Id == executionRunId);
        if (currentRun?.ChatSessionId is not Guid chatSessionId)
        {
            return sessions;
        }

        var currentSession = sessions.FirstOrDefault(item => item.Id == chatSessionId);
        if (currentSession is null)
        {
            return sessions;
        }

        var updatedRun = UpdateRunProgressFromLog(currentRun, entry);
        return ReplaceChatSession(sessions, UpdateChatSessionProgressFromRun(currentSession, updatedRun));
    }

    private static IReadOnlyList<AgentRunMetric> InsertMetric(
        IReadOnlyList<AgentRunMetric> metrics,
        AgentRunMetric metric)
    {
        return InsertOrReplaceDescending(
            metrics,
            metric,
            item => item.Id == metric.Id,
            item => item.CreatedAtUtc);
    }

    private static IReadOnlyList<ProviderUsageObservation> InsertUsageObservations(
        IReadOnlyList<ProviderUsageObservation> usageObservations,
        IReadOnlyList<ProviderUsageObservation> newObservations)
    {
        if (newObservations.Count == 0)
        {
            return usageObservations;
        }

        var result = usageObservations;
        foreach (var observation in newObservations)
        {
            result = InsertOrReplaceDescending(
                result,
                observation,
                item => item.Id == observation.Id,
                item => item.CreatedAtUtc);
        }

        return result;
    }

    private static IReadOnlyList<ToolExecutionReceiptRecord> InsertToolReceipts(
        IReadOnlyList<ToolExecutionReceiptRecord> receipts,
        IReadOnlyList<ToolExecutionReceiptRecord> newReceipts)
    {
        if (newReceipts.Count == 0)
        {
            return receipts;
        }

        var result = receipts;
        foreach (var receipt in newReceipts)
        {
            result = InsertOrReplaceDescending(
                result,
                receipt,
                item => item.Id == receipt.Id,
                item => item.CompletedAtUtc);
        }

        return result;
    }

    private static IReadOnlyList<T> InsertOrReplaceDescending<T>(
        IReadOnlyList<T> items,
        T item,
        Func<T, bool> isSameItem,
        Func<T, DateTimeOffset> orderSelector)
    {
        var updated = new List<T>(items.Count + 1);
        var inserted = false;
        var itemOrder = orderSelector(item);

        foreach (var current in items)
        {
            if (isSameItem(current))
            {
                continue;
            }

            if (!inserted && itemOrder >= orderSelector(current))
            {
                updated.Add(item);
                inserted = true;
            }

            updated.Add(current);
        }

        if (!inserted)
        {
            updated.Add(item);
        }

        return updated;
    }

    private static ExecutionRunRecord UpdateRunProgressFromLog(
        ExecutionRunRecord run,
        ExecutionLogEntry entry)
    {
        if (run.State is ExecutionState.Completed or ExecutionState.Failed)
        {
            return run;
        }

        var nextState = entry.State == ExecutionState.Idle
            ? run.State
            : entry.State;
        var nextUpdatedAtUtc = entry.CreatedAtUtc > run.UpdatedAtUtc
            ? entry.CreatedAtUtc
            : run.UpdatedAtUtc;
        if (nextState == run.State && nextUpdatedAtUtc == run.UpdatedAtUtc)
        {
            return run;
        }

        return run with
        {
            State = nextState,
            UpdatedAtUtc = nextUpdatedAtUtc
        };
    }

    private static ChatSessionRecord UpdateChatSessionProgressFromRun(
        ChatSessionRecord session,
        ExecutionRunRecord run)
    {
        return ChatSessionRuntimeCompatibilityAdapter.ClearCompatibility(
            session,
            run.UpdatedAtUtc,
            run.Id);
    }

    private static bool TryGetBlockingSessionRun(
        SandboxWorkspaceExecutionState executionState,
        ChatSessionRecord session,
        out ExecutionRunRecord? blockingRun)
    {
        blockingRun = null;

        if (session.LatestExecutionRunId.HasValue)
        {
            var latestRun = executionState.ExecutionRuns.FirstOrDefault(item => item.Id == session.LatestExecutionRunId.Value);
            if (latestRun is not null && ExecutionRunBlocksSession(latestRun))
            {
                blockingRun = latestRun;
                return true;
            }
        }

        blockingRun = executionState.ExecutionRuns
            .Where(item => item.ChatSessionId == session.Id && ExecutionRunBlocksSession(item))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefault();
        return blockingRun is not null;
    }

    private static async Task<ExecutionRunRecord?> TryGetBlockingSessionRunAsync(
        ISandboxWorkspaceExecutionRunStore executionRunStore,
        ISandboxWorkspaceChatQueryStore chatQueryStore,
        Guid agentId,
        ChatSessionRecord session,
        CancellationToken cancellationToken)
    {
        if (session.LatestExecutionRunId.HasValue)
        {
            var latestRun = await executionRunStore.GetExecutionRunAsync(session.LatestExecutionRunId.Value, cancellationToken);
            if (latestRun is not null && ExecutionRunBlocksSession(latestRun))
            {
                return latestRun;
            }
        }

        var summaries = await chatQueryStore.ListChatRunSummariesAsync(agentId, session.Id, cancellationToken);
        var blockingSummary = summaries
            .Where(item => item.ChatSessionId == session.Id && ExecutionRunBlocksSession(item.State))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefault();
        if (blockingSummary is null)
        {
            return null;
        }

        var blockingRun = await executionRunStore.GetExecutionRunAsync(blockingSummary.ExecutionRunId, cancellationToken);
        return blockingRun is not null && ExecutionRunBlocksSession(blockingRun)
            ? blockingRun
            : null;
    }

    private static string DescribeSessionBusyMessage(
        SandboxWorkspaceExecutionState executionState,
        ChatSessionRecord session)
    {
        if (!TryGetBlockingSessionRun(executionState, session, out var blockingRun) || blockingRun is null)
        {
            return "This session already has an active execution run. Wait for it to finish before sending a new prompt.";
        }

        return blockingRun.PendingApprovals.Count > 0 || blockingRun.State == ExecutionState.WaitingOnTool
            ? "This session has pending tool approvals. Approve or reject them before sending a new prompt."
            : "This session already has an active execution run. Wait for it to finish before sending a new prompt.";
    }

    private static string DescribeSessionBusyMessage(ExecutionRunRecord blockingRun)
    {
        return blockingRun.PendingApprovals.Count > 0 || blockingRun.State == ExecutionState.WaitingOnTool
            ? "This session has pending tool approvals. Approve or reject them before sending a new prompt."
            : "This session already has an active execution run. Wait for it to finish before sending a new prompt.";
    }

    private static bool ExecutionRunBlocksSession(ExecutionRunRecord run)
        => run.PendingApprovals.Count > 0 || ExecutionRunBlocksSession(run.State);

    private static bool ExecutionRunBlocksSession(ExecutionState state)
        => state is ExecutionState.Preparing or ExecutionState.Running or ExecutionState.WaitingOnTool or ExecutionState.Persisting;
}
