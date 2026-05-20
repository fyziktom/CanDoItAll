using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryCuratorConversationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemoryRecallOrchestrator recallOrchestrator,
    ICognitiveMemoryAutomationSettingsService settingsService,
    IAgentFrameworkWorkspaceService workspaceService,
    ICognitiveMemoryConsolidationCandidateApplicator consolidationCandidateApplicator,
    IClock clock) : ICognitiveMemoryCuratorConversationService
{
    private const string CuratorSourceSystem = "CuratorConversation";
    private const string CuratorSourceItemType = "CuratorTrustedTurn";
    private const string CuratorAlgorithmVersion = "curator-conversation-v1";
    private const string ApprovalBypassReason = "Trusted curator conversation mode accepts direct operator corrections without manual review.";
    private const int MaximumTitleLength = 300;
    private const int MaximumUserMessageLength = 8000;
    private const int MaximumCuratorResponseLength = 12000;
    private const int MaximumSummaryLength = 4000;
    private const int MaximumCorrectionLength = 8000;
    private const double TrustedConfidenceScore = 0.95;
    private const double TrustedPriorityScore = 0.95;

    public async ValueTask<CognitiveMemoryCuratorSessionRecord> StartAsync(
        CognitiveMemoryCuratorSessionStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ProjectId == Guid.Empty)
        {
            throw new ArgumentException("Curator conversation requires a project id.", nameof(request));
        }

        var now = clock.GetUtcNow();
        var session = new CognitiveMemoryCuratorSessionRecord
        {
            ProjectId = request.ProjectId,
            Status = CognitiveMemoryCuratorSessionStatus.Active,
            RuntimeMode = request.RuntimeMode,
            ConversationDepth = request.ConversationDepth,
            Title = TrimText(CognitiveMemoryGuard.EnsureText(request.Title, nameof(request.Title)), MaximumTitleLength),
            ActorId = CognitiveMemoryGuard.EnsureText(request.PolicyContext.ActorId, nameof(request.PolicyContext.ActorId)),
            PolicyProfileId = request.PolicyContext.PolicyProfileId.Value,
            AccessLevel = request.PolicyContext.AccessLevel,
            RiskLevel = request.PolicyContext.RiskLevel,
            AllowRestrictedContent = request.PolicyContext.AllowRestrictedContent,
            AgentId = request.AgentId,
            ProviderProfileId = request.ProviderProfileId,
            ModelId = request.ModelId,
            AlgorithmVersion = CuratorAlgorithmVersion,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async ValueTask<CognitiveMemoryCuratorSendResult> SendAsync(
        CognitiveMemoryCuratorSendRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SessionId == Guid.Empty)
        {
            throw new ArgumentException("Curator send requires a session id.", nameof(request));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var session = await dbContext.Set<CognitiveMemoryCuratorSessionRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.SessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Curator session '{request.SessionId:D}' was not found.");
        if (session.Status != CognitiveMemoryCuratorSessionStatus.Active)
        {
            throw new InvalidOperationException($"Curator session '{session.Id:D}' is not active.");
        }

        var conversationDepth = request.ConversationDepth ?? session.ConversationDepth;
        var policyContext = CreatePolicyContext(session);
        var recallResult = await recallOrchestrator.RecallAsync(
            new CognitiveMemoryRecallRequest(
                session.ProjectId,
                CognitiveMemoryGuard.EnsureText(request.Message, nameof(request.Message)),
                request.Intent,
                CognitiveMemoryRecallMode.DeepSourceGrounded,
                policyContext,
                request.Budget ?? ResolveCuratorRecallBudget(conversationDepth),
                Metadata: new Dictionary<string, string>
                {
                    ["curatorSessionId"] = session.Id.ToString("D"),
                    ["curatorRuntimeMode"] = session.RuntimeMode.ToString(),
                    ["curatorConversationDepth"] = conversationDepth.ToString()
                }),
            cancellationToken);
        var includedMemoryRecordIds = ResolveIncludedMemoryRecordIds(recallResult);
        var response = session.RuntimeMode switch
        {
            CognitiveMemoryCuratorRuntimeMode.DirectLlm => await SendDirectLlmAsync(session, recallResult.ContextPack, request.Message, conversationDepth, cancellationToken),
            CognitiveMemoryCuratorRuntimeMode.Agent => await SendAgentAsync(session, recallResult.ContextPack, request.Message, conversationDepth, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported curator runtime mode '{session.RuntimeMode}'.")
        };

        if (string.IsNullOrWhiteSpace(response.ResponseText))
        {
            throw new InvalidOperationException($"Curator runtime mode '{session.RuntimeMode}' produced an empty response.");
        }

        var turnResult = await RecordTurnAsync(
            new CognitiveMemoryCuratorTurnCaptureRequest(
                session.Id,
                request.Message,
                response.ResponseText,
                session.RuntimeMode,
                ConversationDepth: conversationDepth,
                RecallTraceId: recallResult.TraceId,
                ContextPackId: recallResult.ContextPack.Id.Value,
                AffectedMemoryRecordIds: includedMemoryRecordIds,
                ExplicitCaptureKind: request.ExplicitCaptureKind,
                AgentId: response.AgentId,
                ProviderProfileId: response.ProviderProfileId,
                ModelId: response.ModelId),
            cancellationToken);

        return new CognitiveMemoryCuratorSendResult(
            turnResult.Session,
            turnResult.Turn,
            session.RuntimeMode,
            response.ResponseText,
            response.AgentId,
            response.ProviderProfileId,
            response.ModelId,
            recallResult.TraceId,
            recallResult.ContextPack.Id.Value,
            includedMemoryRecordIds,
            turnResult.CapturedImprovements,
            recallResult.Warnings);
    }

    public async ValueTask<CognitiveMemoryCuratorTurnCaptureResult> RecordTurnAsync(
        CognitiveMemoryCuratorTurnCaptureRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SessionId == Guid.Empty)
        {
            throw new ArgumentException("Curator turn capture requires a session id.", nameof(request));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var session = await dbContext.Set<CognitiveMemoryCuratorSessionRecord>()
            .SingleOrDefaultAsync(item => item.Id == request.SessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Curator session '{request.SessionId:D}' was not found.");
        if (session.Status != CognitiveMemoryCuratorSessionStatus.Active)
        {
            throw new InvalidOperationException($"Curator session '{session.Id:D}' is not active.");
        }

        var now = clock.GetUtcNow();
        var includedMemoryRecordIds = await ResolveIncludedMemoryRecordIdsAsync(dbContext, request, cancellationToken);
        var conversationDepth = request.ConversationDepth ?? session.ConversationDepth;
        var sequence = session.TurnCount + 1;
        var turn = new CognitiveMemoryCuratorTurnRecord
        {
            CuratorSessionId = session.Id,
            ProjectId = session.ProjectId,
            Sequence = sequence,
            RuntimeMode = request.RuntimeMode,
            ConversationDepth = conversationDepth,
            UserMessage = TrimText(CognitiveMemoryGuard.EnsureText(request.UserMessage, nameof(request.UserMessage)), MaximumUserMessageLength),
            CuratorResponse = TrimText(request.CuratorResponse, MaximumCuratorResponseLength),
            RecallTraceId = request.RecallTraceId,
            ContextPackId = request.ContextPackId,
            IncludedMemoryRecordIdsJson = SerializeGuidList(includedMemoryRecordIds),
            AgentId = request.AgentId ?? session.AgentId,
            ProviderProfileId = request.ProviderProfileId ?? session.ProviderProfileId,
            ModelId = request.ModelId ?? session.ModelId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };

        session.TurnCount = sequence;
        session.UpdatedAtUtc = now;
        dbContext.Add(turn);
        await dbContext.SaveChangesAsync(cancellationToken);

        var captureKind = ResolveCaptureKind(request, includedMemoryRecordIds);
        var captured = new List<CognitiveMemoryCuratorCapturedImprovementRecord>();
        if (captureKind is { } kind)
        {
            var capture = await CreateTrustedImprovementAsync(
                dbContext,
                session,
                turn,
                request,
                kind,
                includedMemoryRecordIds,
                now,
                cancellationToken);
            captured.Add(capture);
            turn.CaptureCount = captured.Count;
            turn.UpdatedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new CognitiveMemoryCuratorTurnCaptureResult(session, turn, captured);
    }

    public async ValueTask<IReadOnlyList<CognitiveMemoryCuratorTurnRecord>> GetRecentTurnsAsync(
        Guid sessionId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("Curator turn query requires a session id.", nameof(sessionId));
        }

        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "Curator turn query take must be positive.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<CognitiveMemoryCuratorTurnRecord>()
            .AsNoTracking()
            .Where(item => item.CuratorSessionId == sessionId)
            .OrderByDescending(item => item.Sequence)
            .Take(Math.Min(take, 200))
            .OrderBy(item => item.Sequence)
            .ToListAsync(cancellationToken);
    }

    private async ValueTask<CuratorRuntimeResponse> SendDirectLlmAsync(
        CognitiveMemoryCuratorSessionRecord session,
        CognitiveMemoryRecallContextPack contextPack,
        string message,
        CognitiveMemoryCuratorConversationDepth conversationDepth,
        CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync(cancellationToken);
        var profile = ResolveCuratorModelProfile(settings);
        var providerProfileId = session.ProviderProfileId ??
                                profile.ProviderProfileId ??
                                settings.DefaultProviderProfileId ??
                                throw new InvalidOperationException("Direct curator mode requires a configured provider profile. Set the Cognitive Memory default provider or the curator conversation execution profile provider.");
        var modelId = session.ModelId ?? profile.ModelId;
        var depthProfile = ResolveDepthProfile(conversationDepth);
        var previousTurns = await GetRecentTurnsAsync(session.Id, take: depthProfile.HistoryTurnLimit, cancellationToken);
        var history = previousTurns
            .SelectMany(turn => new[]
            {
                new ProviderTestChatMessage(ChatMessageRole.User, turn.UserMessage, turn.CreatedAtUtc),
                new ProviderTestChatMessage(ChatMessageRole.Assistant, turn.CuratorResponse, turn.UpdatedAtUtc)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Content))
            .ToList();
        var result = await workspaceService.RunProviderTestChatAsync(
            providerProfileId,
            new ProviderTestChatRequest(
                modelId.Value,
                BuildCuratorSystemPrompt(session, contextPack, conversationDepth),
                history,
                message.Trim()),
            cancellationToken);
        return new CuratorRuntimeResponse(
            result.ResponseText,
            null,
            providerProfileId,
            CreateModelIdOrNull(result.Model) ?? modelId);
    }

    private async ValueTask<CuratorRuntimeResponse> SendAgentAsync(
        CognitiveMemoryCuratorSessionRecord session,
        CognitiveMemoryRecallContextPack contextPack,
        string message,
        CognitiveMemoryCuratorConversationDepth conversationDepth,
        CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync(cancellationToken);
        var agentId = session.AgentId ??
                      settings.DefaultAgentId ??
                      throw new InvalidOperationException("Agent curator mode requires a configured agent. Set the Cognitive Memory default agent or start the curator session with an explicit agent id.");
        var chatSession = await workspaceService.GetOrCreateChatSessionAsync(
            agentId,
            session.AgentChatSessionId,
            cancellationToken);
        if (session.AgentChatSessionId != chatSession.Id)
        {
            await PersistAgentChatSessionIdAsync(session.Id, chatSession.Id, cancellationToken);
        }

        var result = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agentId,
                BuildCuratorAgentPrompt(session, contextPack, message, conversationDepth),
                ChatSessionId: chatSession.Id,
                AutoApprovePendingToolCalls: true),
            cancellationToken);
        return new CuratorRuntimeResponse(
            result.ResponseText,
            agentId,
            null,
            CreateModelIdOrNull(result.Metric.Model) ?? session.ModelId);
    }

    private async Task PersistAgentChatSessionIdAsync(
        Guid sessionId,
        Guid chatSessionId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var session = await dbContext.Set<CognitiveMemoryCuratorSessionRecord>()
            .SingleAsync(item => item.Id == sessionId, cancellationToken);
        session.AgentChatSessionId = chatSessionId;
        session.UpdatedAtUtc = clock.GetUtcNow();
        session.ConcurrencyToken = Guid.NewGuid();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static CognitiveMemoryPolicyContext CreatePolicyContext(CognitiveMemoryCuratorSessionRecord session)
        => new(
            session.ProjectId,
            session.ActorId,
            session.AccessLevel,
            new CognitiveMemoryPolicyProfileId(session.PolicyProfileId),
            session.RiskLevel,
            session.AllowRestrictedContent);

    private static CognitiveMemoryModelExecutionProfile ResolveCuratorModelProfile(CognitiveMemoryAutomationSettings settings)
        => settings.ModelExecutionProfiles.FirstOrDefault(profile => profile.Role == CognitiveMemoryModelExecutionRole.CuratorConversation) ??
           CognitiveMemoryModelExecutionProfileDefaults.CreateOpenAi(CognitiveMemoryModelExecutionRole.CuratorConversation);

    private static CognitiveMemoryRecallBudget ResolveCuratorRecallBudget(CognitiveMemoryCuratorConversationDepth conversationDepth)
        => ResolveDepthProfile(conversationDepth).RecallBudget;

    private static CuratorConversationDepthProfile ResolveDepthProfile(CognitiveMemoryCuratorConversationDepth conversationDepth)
        => conversationDepth switch
        {
            CognitiveMemoryCuratorConversationDepth.Short => new CuratorConversationDepthProfile(
                new CognitiveMemoryRecallBudget(
                    coarseCandidateLimit: 24,
                    graphExpansionDepth: 1,
                    vectorResultLimit: 8,
                    focusLimit: 8,
                    detailItemLimit: 6,
                    contextCharacterBudget: 8_000,
                    maxSourceBytes: 64_000),
                ContextSectionLimit: 3,
                SourceRefLimit: 4,
                ContextContentLength: 2_500,
                HistoryTurnLimit: 4,
                ResponseInstruction: "Keep the response short: answer in one to three focused sentences unless the user explicitly asks for detail."),
            CognitiveMemoryCuratorConversationDepth.Long => new CuratorConversationDepthProfile(
                new CognitiveMemoryRecallBudget(
                    coarseCandidateLimit: 96,
                    graphExpansionDepth: 3,
                    vectorResultLimit: 32,
                    focusLimit: 32,
                    detailItemLimit: 32,
                    contextCharacterBudget: 64_000,
                    maxSourceBytes: 512_000),
                ContextSectionLimit: 12,
                SourceRefLimit: 16,
                ContextContentLength: 10_000,
                HistoryTurnLimit: 24,
                ResponseInstruction: "Use a detailed response: include relevant evidence, uncertainty, competing hypotheses, and memory implications when they help the operator validate knowledge."),
            _ => new CuratorConversationDepthProfile(
                new CognitiveMemoryRecallBudget(
                    coarseCandidateLimit: 48,
                    graphExpansionDepth: 2,
                    vectorResultLimit: 16,
                    focusLimit: 16,
                    detailItemLimit: 16,
                    contextCharacterBudget: 24_000,
                    maxSourceBytes: 192_000),
                ContextSectionLimit: 6,
                SourceRefLimit: 8,
                ContextContentLength: 5_000,
                HistoryTurnLimit: 12,
                ResponseInstruction: "Use a balanced response: answer directly, cite relevant memory context, and keep caveats proportional to the evidence.")
        };

    private static IReadOnlyList<CognitiveMemoryRecordId> ResolveIncludedMemoryRecordIds(CognitiveMemoryRecallResult recallResult)
    {
        var values = recallResult.ContextPack.SourceRefs
            .Where(sourceRef => sourceRef.IncludedInContext)
            .Select(sourceRef => sourceRef.MemoryRecordId)
            .Concat(recallResult.ContextPack.Sections.SelectMany(section => section.MemoryRecordIds))
            .Concat(recallResult.Candidates
                .Where(candidate => candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected)
                .Select(candidate => candidate.MemoryRecordId))
            .Distinct()
            .ToArray();
        return values;
    }

    private static string BuildCuratorSystemPrompt(
        CognitiveMemoryCuratorSessionRecord session,
        CognitiveMemoryRecallContextPack contextPack,
        CognitiveMemoryCuratorConversationDepth conversationDepth)
        => string.Join(
            Environment.NewLine,
            [
                "You are the curator of Cognitive Memory.",
                "Answer conversationally, using the supplied memory context when it is relevant.",
                ResolveDepthProfile(conversationDepth).ResponseInstruction,
                "When the user gives a correction or new knowledge, acknowledge it naturally. The application will persist the memory improvement separately.",
                "Do not claim that unsupported memory is certain. If context is thin, say what is missing.",
                $"Project id: {session.ProjectId:D}",
                $"Conversation depth: {conversationDepth}",
                "Memory context:",
                RenderContextPack(contextPack, conversationDepth)
            ]);

    private static string BuildCuratorAgentPrompt(
        CognitiveMemoryCuratorSessionRecord session,
        CognitiveMemoryRecallContextPack contextPack,
        string message,
        CognitiveMemoryCuratorConversationDepth conversationDepth)
        => string.Join(
            Environment.NewLine,
            [
                "Curator conversation request.",
                "Use the Cognitive Memory context below as the source for the answer.",
                ResolveDepthProfile(conversationDepth).ResponseInstruction,
                "Manual tool approvals are disabled for this trusted curator conversation run; only use tools that can run under the configured auto-approval policy.",
                $"Project id: {session.ProjectId:D}",
                $"Conversation depth: {conversationDepth}",
                "Memory context:",
                RenderContextPack(contextPack, conversationDepth),
                "User message:",
                message.Trim()
            ]);

    private static string RenderContextPack(
        CognitiveMemoryRecallContextPack contextPack,
        CognitiveMemoryCuratorConversationDepth conversationDepth)
    {
        var depthProfile = ResolveDepthProfile(conversationDepth);
        var lines = new List<string>
        {
            $"Context pack: {contextPack.Id.Value:D}",
            $"Title: {contextPack.Title}",
            $"Summary: {contextPack.Summary}"
        };
        foreach (var section in contextPack.Sections.Take(depthProfile.ContextSectionLimit))
        {
            lines.Add($"Section: {section.Title}");
            lines.Add(TrimText(section.Content, depthProfile.ContextContentLength));
        }

        var sources = contextPack.SourceRefs
            .Where(sourceRef => sourceRef.IncludedInContext)
            .Take(depthProfile.SourceRefLimit)
            .ToArray();
        if (sources.Length > 0)
        {
            lines.Add("Sources:");
            foreach (var source in sources)
            {
                lines.Add($"- {source.SourceSystem} {source.Locator}: {source.Summary}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static CognitiveMemoryExecutionModelId? CreateModelIdOrNull(string value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : new CognitiveMemoryExecutionModelId(value.Trim());

    private async ValueTask<CognitiveMemoryCuratorCapturedImprovementRecord> CreateTrustedImprovementAsync(
        AppDbContext dbContext,
        CognitiveMemoryCuratorSessionRecord session,
        CognitiveMemoryCuratorTurnRecord turn,
        CognitiveMemoryCuratorTurnCaptureRequest request,
        CognitiveMemoryCuratorCaptureKind captureKind,
        IReadOnlyList<Guid> affectedMemoryRecordIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var summary = CreateCaptureSummary(captureKind, turn.UserMessage);
        var content = CreateSourceContent(session, turn, captureKind, affectedMemoryRecordIds);
        var contentHash = CognitiveMemoryHash.FromUtf8(content).Value;
        var outputHash = CognitiveMemoryHash.FromUtf8(summary).Value;
        var captureId = Guid.NewGuid();
        var locator = $"curator-session/{session.Id:D}/turn/{turn.Id:D}/capture/{captureId:D}";
        var idempotencyKey = $"curator-conversation:{captureId:D}";
        var title = TrimText(CreateCaptureTitle(captureKind, turn.UserMessage), MaximumTitleLength);
        var sourceManifest = new CognitiveMemorySourceManifestRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = session.ProjectId,
            SourceSystem = CuratorSourceSystem,
            SourceScopeKey = $"project:{session.ProjectId:D}",
            SourceSnapshotId = $"curator-turn:{turn.Id:D}:capture:{captureId:D}",
            SnapshotHash = contentHash,
            ProviderVersion = CuratorAlgorithmVersion,
            ScanStatus = CognitiveMemoryRunStatus.Succeeded,
            ObservedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var sourceItem = new CognitiveMemorySourceItemRecord
        {
            Id = Guid.NewGuid(),
            SourceManifestId = sourceManifest.Id,
            ProjectId = session.ProjectId,
            SourceSystem = CuratorSourceSystem,
            SourceItemKey = $"curator-turn:{turn.Id:D}:capture:{captureId:D}",
            SourceItemType = CuratorSourceItemType,
            Title = title,
            ContentText = content,
            Locator = locator,
            ContentHash = contentHash,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            AccessLevel = session.AccessLevel,
            AccessScope = session.ProjectId.ToString("D"),
            ProvenanceJson = CreateProvenanceJson(session, turn, captureKind, affectedMemoryRecordIds),
            ObservedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var evidenceAnchor = new CognitiveMemoryEvidenceAnchorRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = session.ProjectId,
            AnchorKind = CognitiveMemoryEvidenceAnchorKind.CuratorConversationTurn,
            SourceManifestId = sourceManifest.Id,
            SourceItemId = sourceItem.Id,
            SourceSystem = CuratorSourceSystem,
            Locator = locator,
            StructuredPath = "$.curatorConversationTurn",
            TextStart = 0,
            TextEnd = content.Length,
            QuoteHash = outputHash,
            TrustLevel = CognitiveMemorySourceTrustLevel.HumanReview,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            SourceHash = contentHash,
            ObservedAtUtc = now,
            CreatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var mutationCommand = new CognitiveMemoryMutationCommandRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = session.ProjectId,
            CommandKind = ResolveMutationCommandKind(captureKind, affectedMemoryRecordIds),
            Status = CognitiveMemoryMutationCommandStatus.Accepted,
            ActorKind = CognitiveMemoryActorKind.User,
            ActorId = session.ActorId,
            IdempotencyKey = idempotencyKey,
            AffectedMemoryRecordIdsJson = SerializeGuidList(affectedMemoryRecordIds),
            EvidenceAnchorIdsJson = SerializeGuidList([evidenceAnchor.Id]),
            PayloadJson = content,
            RequiresHumanReview = false,
            ReviewReason = string.Empty,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var runId = Guid.NewGuid();
        var run = new CognitiveMemoryRunRecord
        {
            Id = runId,
            ProjectId = session.ProjectId,
            RunKind = CognitiveMemoryRunKind.Consolidation,
            Status = CognitiveMemoryRunStatus.Succeeded,
            OperationMode = CognitiveMemoryOperationMode.Consolidate,
            IdempotencyKey = idempotencyKey,
            InputHash = contentHash,
            AlgorithmVersion = CuratorAlgorithmVersion,
            StartedAtUtc = now,
            CompletedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var consolidationRun = new CognitiveMemoryConsolidationRunRecord
        {
            Id = runId,
            ProjectId = session.ProjectId,
            Mode = ResolveConsolidationMode(captureKind),
            TriggerKind = CognitiveMemoryConsolidationTriggerKind.Manual,
            Status = CognitiveMemoryRunStatus.Succeeded,
            ProfileName = "curator-conversation-trusted",
            IdempotencyKey = idempotencyKey,
            InputHash = contentHash,
            OutputHash = outputHash,
            AlgorithmVersion = CuratorAlgorithmVersion,
            LeaseOwnerId = session.ActorId,
            LeaseExpiresAtUtc = now,
            SourceItemsScanned = 1,
            CandidatesCreated = 1,
            MutationCommandsSubmitted = 1,
            StartedAtUtc = now,
            CompletedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var payload = new CognitiveMemoryConsolidationCandidatePayload(
            ResolveCandidateKind(captureKind),
            sourceItem.Id,
            evidenceAnchor.Id,
            mutationCommand.Id,
            null,
            CuratorSourceSystem,
            CuratorSourceItemType,
            title,
            summary,
            contentHash,
            CreateCandidateReason(captureKind, affectedMemoryRecordIds));
        var candidate = new CognitiveMemoryConsolidationCandidateRecord
        {
            Id = Guid.NewGuid(),
            RunId = consolidationRun.Id,
            ProjectId = session.ProjectId,
            CandidateKind = payload.CandidateKind,
            Status = CognitiveMemoryConsolidationCandidateStatus.Draft,
            SourceItemId = sourceItem.Id,
            EvidenceAnchorId = evidenceAnchor.Id,
            MutationCommandId = mutationCommand.Id,
            ScoreBucket = CognitiveMemoryScoreProjectionBucket.StrongAccept,
            DisplayPriorityProjection = TrustedPriorityScore,
            SourceContentHash = contentHash,
            OutputHash = outputHash,
            AlgorithmVersion = CuratorAlgorithmVersion,
            ReasonCode = "CuratorTrustedConversation",
            ReasonText = payload.Reason,
            PayloadJson = JsonSerializer.Serialize(
                payload,
                CognitiveMemoryJsonSerializerContext.Default.CognitiveMemoryConsolidationCandidatePayload),
            CreatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        var capture = new CognitiveMemoryCuratorCapturedImprovementRecord
        {
            Id = captureId,
            CuratorSessionId = session.Id,
            CuratorTurnId = turn.Id,
            ProjectId = session.ProjectId,
            CaptureKind = captureKind,
            ConversationDepth = turn.ConversationDepth,
            Status = CognitiveMemoryCuratorCaptureStatus.Captured,
            RecallTraceId = turn.RecallTraceId,
            ContextPackId = turn.ContextPackId,
            AffectedMemoryRecordIdsJson = SerializeGuidList(affectedMemoryRecordIds),
            SourceItemId = sourceItem.Id,
            EvidenceAnchorId = evidenceAnchor.Id,
            MutationCommandId = mutationCommand.Id,
            ConsolidationCandidateId = candidate.Id,
            ActorId = session.ActorId,
            ConfidenceScore = TrustedConfidenceScore,
            PriorityScore = TrustedPriorityScore,
            Summary = TrimText(summary, MaximumSummaryLength),
            CorrectionText = TrimText(turn.UserMessage, MaximumCorrectionLength),
            CreatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };

        dbContext.AddRange(sourceManifest, sourceItem, evidenceAnchor, mutationCommand, run, consolidationRun, candidate, capture);
        dbContext.Add(new CognitiveMemoryMutationAuditEventRecord
        {
            Id = Guid.NewGuid(),
            MutationCommandId = mutationCommand.Id,
            ProjectId = session.ProjectId,
            Sequence = 1,
            EventKind = CognitiveMemoryMutationAuditEventKind.Submitted,
            Message = $"Trusted curator capture '{capture.Id:D}' accepted for memory mutation.",
            CreatedAtUtc = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        var applyResult = await consolidationCandidateApplicator.ApplyAsync(
            dbContext,
            candidate,
            payload,
            CognitiveMemoryValidationState.Approved,
            CognitiveMemoryStabilityState.Active,
            session.ActorId,
            now,
            cancellationToken);

        await MarkAffectedMemoryRecordsAsync(dbContext, captureKind, affectedMemoryRecordIds, applyResult.MemoryRecordId, session, now, cancellationToken);
        await PreserveMutationTargetingAsync(dbContext, mutationCommand.Id, affectedMemoryRecordIds, applyResult.MemoryRecordId, now, cancellationToken);

        capture.Status = CognitiveMemoryCuratorCaptureStatus.Applied;
        capture.AppliedMemoryRecordId = applyResult.MemoryRecordId;
        capture.ConcurrencyToken = Guid.NewGuid();
        return capture;
    }

    private static CognitiveMemoryCuratorCaptureKind? ResolveCaptureKind(
        CognitiveMemoryCuratorTurnCaptureRequest request,
        IReadOnlyList<Guid> affectedMemoryRecordIds)
    {
        if (request.ExplicitCaptureKind is { } explicitKind)
        {
            return explicitKind;
        }

        var message = request.UserMessage.Trim();
        if (ContainsAny(message, ["wrong scope", "wrong context", "different scope", "narrow that", "only applies to"]))
        {
            return CognitiveMemoryCuratorCaptureKind.WrongScope;
        }

        if (ContainsAny(message, ["not correct", "incorrect", "wrong", "right version", "actually it is", "actually, it is", "different than", "instead"]) ||
            affectedMemoryRecordIds.Count > 0 && ContainsAny(message, ["actually", "no,"]))
        {
            return CognitiveMemoryCuratorCaptureKind.Correction;
        }

        if (ContainsAny(message, ["remember", "add this", "learn this", "store this", "save this", "you should know", "memory should know"]))
        {
            return CognitiveMemoryCuratorCaptureKind.NewKnowledge;
        }

        return null;
    }

    private static bool ContainsAny(string value, IReadOnlyList<string> candidates)
        => candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));

    private async ValueTask<IReadOnlyList<Guid>> ResolveIncludedMemoryRecordIdsAsync(
        AppDbContext dbContext,
        CognitiveMemoryCuratorTurnCaptureRequest request,
        CancellationToken cancellationToken)
    {
        var values = new List<Guid>();
        if (request.AffectedMemoryRecordIds is not null)
        {
            values.AddRange(request.AffectedMemoryRecordIds
                .Select(item => item.Value)
                .Where(item => item != Guid.Empty));
        }

        if (request.RecallTraceId is { } recallTraceId)
        {
            values.AddRange(await dbContext.Set<CognitiveMemoryRecallSourceRefRecord>()
                .AsNoTracking()
                .Where(item => item.RecallTraceId == recallTraceId && item.IncludedInContext)
                .Select(item => item.MemoryRecordId)
                .ToListAsync(cancellationToken));
            values.AddRange(await dbContext.Set<CognitiveMemoryRecallCandidateRecord>()
                .AsNoTracking()
                .Where(item =>
                    item.RecallTraceId == recallTraceId &&
                    item.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected)
                .Select(item => item.MemoryRecordId)
                .ToListAsync(cancellationToken));
            values.AddRange(await dbContext.Set<CognitiveMemoryRecallContextSectionRecord>()
                .AsNoTracking()
                .Where(item => item.RecallTraceId == recallTraceId && item.MemoryRecordId.HasValue)
                .Select(item => item.MemoryRecordId!.Value)
                .ToListAsync(cancellationToken));
        }

        return values
            .Where(item => item != Guid.Empty)
            .Distinct()
            .ToArray();
    }

    private static async Task MarkAffectedMemoryRecordsAsync(
        AppDbContext dbContext,
        CognitiveMemoryCuratorCaptureKind captureKind,
        IReadOnlyList<Guid> affectedMemoryRecordIds,
        Guid appliedMemoryRecordId,
        CognitiveMemoryCuratorSessionRecord session,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (captureKind == CognitiveMemoryCuratorCaptureKind.NewKnowledge || affectedMemoryRecordIds.Count == 0)
        {
            return;
        }

        var affectedRecords = await dbContext.Set<CognitiveMemoryRecord>()
            .Where(item => affectedMemoryRecordIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        foreach (var affectedRecord in affectedRecords)
        {
            affectedRecord.ValidationState = CognitiveMemoryValidationState.Superseded;
            affectedRecord.StabilityState = CognitiveMemoryStabilityState.Stale;
            affectedRecord.UpdatedAtUtc = now;
            affectedRecord.ConcurrencyToken = Guid.NewGuid();
            dbContext.Add(new CognitiveMemoryRelationRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = session.ProjectId,
                SourceMemoryRecordId = appliedMemoryRecordId,
                TargetMemoryRecordId = affectedRecord.Id,
                RelationKind = captureKind == CognitiveMemoryCuratorCaptureKind.WrongScope
                    ? CognitiveMemoryRelationKind.Refines
                    : CognitiveMemoryRelationKind.Supersedes,
                EvidenceCount = 1,
                RelationBucket = CognitiveMemoryScoreProjectionBucket.StrongAccept,
                DisplayStrengthProjection = TrustedConfidenceScore,
                Reason = captureKind == CognitiveMemoryCuratorCaptureKind.WrongScope
                    ? ApprovalBypassReason
                    : "Trusted curator correction superseded memory used in the previous answer.",
                AlgorithmVersion = CuratorAlgorithmVersion,
                CreatedAtUtc = now,
                ConcurrencyToken = Guid.NewGuid()
            });
        }
    }

    private static async Task PreserveMutationTargetingAsync(
        AppDbContext dbContext,
        Guid mutationCommandId,
        IReadOnlyList<Guid> affectedMemoryRecordIds,
        Guid appliedMemoryRecordId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var mutation = await dbContext.Set<CognitiveMemoryMutationCommandRecord>()
            .SingleAsync(item => item.Id == mutationCommandId, cancellationToken);
        mutation.AffectedMemoryRecordIdsJson = SerializeGuidList(affectedMemoryRecordIds
            .Append(appliedMemoryRecordId)
            .Distinct()
            .ToArray());
        mutation.UpdatedAtUtc = now;
        mutation.ConcurrencyToken = Guid.NewGuid();
    }

    private static CognitiveMemoryMutationCommandKind ResolveMutationCommandKind(
        CognitiveMemoryCuratorCaptureKind captureKind,
        IReadOnlyList<Guid> affectedMemoryRecordIds)
        => captureKind switch
        {
            CognitiveMemoryCuratorCaptureKind.WrongScope => CognitiveMemoryMutationCommandKind.NarrowScope,
            CognitiveMemoryCuratorCaptureKind.Correction when affectedMemoryRecordIds.Count > 0 => CognitiveMemoryMutationCommandKind.SupersedeClaim,
            CognitiveMemoryCuratorCaptureKind.Correction => CognitiveMemoryMutationCommandKind.AttackClaim,
            _ => CognitiveMemoryMutationCommandKind.ProposeClaim
        };

    private static CognitiveMemoryConsolidationMode ResolveConsolidationMode(CognitiveMemoryCuratorCaptureKind captureKind)
        => captureKind == CognitiveMemoryCuratorCaptureKind.NewKnowledge
            ? CognitiveMemoryConsolidationMode.LearningOpportunityReview
            : CognitiveMemoryConsolidationMode.ContradictionReview;

    private static CognitiveMemoryConsolidationCandidateKind ResolveCandidateKind(CognitiveMemoryCuratorCaptureKind captureKind)
        => captureKind == CognitiveMemoryCuratorCaptureKind.NewKnowledge
            ? CognitiveMemoryConsolidationCandidateKind.Knowledge
            : CognitiveMemoryConsolidationCandidateKind.Contradiction;

    private static string CreateSourceContent(
        CognitiveMemoryCuratorSessionRecord session,
        CognitiveMemoryCuratorTurnRecord turn,
        CognitiveMemoryCuratorCaptureKind captureKind,
        IReadOnlyList<Guid> affectedMemoryRecordIds)
        => string.Join(
            Environment.NewLine,
            [
                $"Session: {session.Id:D}",
                $"Turn: {turn.Id:D}",
                $"Actor: {session.ActorId}",
                $"Runtime mode: {turn.RuntimeMode}",
                $"Conversation depth: {turn.ConversationDepth}",
                $"Capture kind: {captureKind}",
                $"Recall trace: {turn.RecallTraceId?.ToString("D") ?? "none"}",
                $"Affected memory records: {string.Join(", ", affectedMemoryRecordIds.Select(item => item.ToString("D")))}",
                "User message:",
                turn.UserMessage,
                "Curator response before capture:",
                FirstNonEmpty(turn.CuratorResponse, "No curator response was recorded.")
            ]);

    private static string CreateProvenanceJson(
        CognitiveMemoryCuratorSessionRecord session,
        CognitiveMemoryCuratorTurnRecord turn,
        CognitiveMemoryCuratorCaptureKind captureKind,
        IReadOnlyList<Guid> affectedMemoryRecordIds)
    {
        var payload = new Dictionary<string, string>
        {
            ["sourceSystem"] = CuratorSourceSystem,
            ["sessionId"] = session.Id.ToString("D"),
            ["turnId"] = turn.Id.ToString("D"),
            ["actorId"] = session.ActorId,
            ["runtimeMode"] = turn.RuntimeMode.ToString(),
            ["conversationDepth"] = turn.ConversationDepth.ToString(),
            ["captureKind"] = captureKind.ToString(),
            ["confidenceScore"] = TrustedConfidenceScore.ToString("0.00"),
            ["priorityScore"] = TrustedPriorityScore.ToString("0.00"),
            ["approvalBypass"] = "true",
            ["affectedMemoryRecordIds"] = string.Join(",", affectedMemoryRecordIds.Select(item => item.ToString("D")))
        };
        if (turn.RecallTraceId is { } recallTraceId)
        {
            payload["recallTraceId"] = recallTraceId.ToString("D");
        }

        if (turn.ContextPackId is { } contextPackId)
        {
            payload["contextPackId"] = contextPackId.ToString("D");
        }

        return JsonSerializer.Serialize(
            payload,
            CognitiveMemoryJsonSerializerContext.Default.DictionaryStringString);
    }

    private static string CreateCaptureSummary(CognitiveMemoryCuratorCaptureKind captureKind, string userMessage)
    {
        var normalized = NormalizeUserKnowledgeText(userMessage);
        return TrimText(captureKind switch
        {
            CognitiveMemoryCuratorCaptureKind.NewKnowledge => normalized,
            CognitiveMemoryCuratorCaptureKind.WrongScope => $"Scope correction from trusted curator conversation: {normalized}",
            _ => $"Correction from trusted curator conversation: {normalized}"
        }, MaximumSummaryLength);
    }

    private static string CreateCaptureTitle(CognitiveMemoryCuratorCaptureKind captureKind, string userMessage)
    {
        var prefix = captureKind switch
        {
            CognitiveMemoryCuratorCaptureKind.NewKnowledge => "Curator knowledge",
            CognitiveMemoryCuratorCaptureKind.WrongScope => "Curator scope correction",
            _ => "Curator correction"
        };
        return $"{prefix}: {TrimText(NormalizeUserKnowledgeText(userMessage), 220)}";
    }

    private static string CreateCandidateReason(
        CognitiveMemoryCuratorCaptureKind captureKind,
        IReadOnlyList<Guid> affectedMemoryRecordIds)
    {
        var targetSummary = affectedMemoryRecordIds.Count == 0
            ? "No prior memory record was targeted."
            : $"Targets memory records: {string.Join(", ", affectedMemoryRecordIds.Select(item => item.ToString("D")))}.";
        return $"{ApprovalBypassReason} Capture kind: {captureKind}. {targetSummary}";
    }

    private static string NormalizeUserKnowledgeText(string userMessage)
    {
        var value = userMessage.Trim();
        foreach (var prefix in new[]
        {
            "please remember that ",
            "remember that ",
            "you should remember that ",
            "memory should know that ",
            "you should know that ",
            "add this: ",
            "learn this: ",
            "store this: ",
            "save this: "
        })
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return value[prefix.Length..].Trim();
            }
        }

        return value;
    }

    private static string SerializeGuidList(IReadOnlyList<Guid> values)
        => JsonSerializer.Serialize(
            values.ToArray(),
            CognitiveMemoryJsonSerializerContext.Default.GuidArray);

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string TrimText(string? value, int maxLength)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private sealed record CuratorRuntimeResponse(
        string ResponseText,
        Guid? AgentId,
        Guid? ProviderProfileId,
        CognitiveMemoryExecutionModelId? ModelId);

    private sealed record CuratorConversationDepthProfile(
        CognitiveMemoryRecallBudget RecallBudget,
        int ContextSectionLimit,
        int SourceRefLimit,
        int ContextContentLength,
        int HistoryTurnLimit,
        string ResponseInstruction);
}
