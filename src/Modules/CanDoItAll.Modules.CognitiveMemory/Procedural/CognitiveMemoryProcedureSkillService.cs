using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryProcedureSkillService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemoryScoreGeometryDriver scoreGeometryDriver,
    IClock clock,
    ILogger<CognitiveMemoryProcedureSkillService> logger) : ICognitiveMemoryProcedureSkillMemoryService, ICognitiveMemorySimulationSandboxService
{
    private const string AlgorithmVersion = "procedure-skill-v1";
    private const string SpeculationLabel = "speculative-hypothesis";
    private const string RejectionImmature = "ImmatureProcedure";
    private const string RejectionMissingReview = "ProcedureNotReviewed";
    private const string RejectionMissingEvidence = "MissingValidationEvidence";
    private const string RejectionHighRiskReview = "HighRiskRequiresReview";
    private static readonly IReadOnlyList<CognitiveMemoryScoreShapeSnapshot> ProcedureMaturityShapes = BuildProcedureMaturityShapes();
    private static readonly IReadOnlyList<CognitiveMemoryScoreShapeSnapshot> SimulationRiskShapes = BuildSimulationRiskShapes();

    public async ValueTask<CognitiveMemoryProcedureSkillRecord> ProposeSkillAsync(
        CognitiveMemoryProcedureSkillProposalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSkillProposal(request);
        ValidatePolicyTrace(request.ProjectId, request.PolicyContext);
        cancellationToken.ThrowIfCancellationRequested();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureContextFramesExistAsync(dbContext, request.ProjectId, request.ContextFrameIds ?? [], cancellationToken);
        await EnsureConsolidationCandidateCanSourceProcedureAsync(dbContext, request.ProjectId, request.SourceConsolidationCandidateId, cancellationToken);
        await EnsureEpisodeExistsAsync(dbContext, request.ProjectId, request.LastSuccessfulEpisodeId, cancellationToken);

        var validationEvidenceIds = request.ValidationEvidence.Select(evidence => evidence.EvidenceAnchorId).ToArray();
        var stepEvidenceIds = request.Steps
            .SelectMany(step => step.EvidenceAnchorIds ?? [])
            .ToArray();
        await EnsureEvidenceAnchorsExistAsync(dbContext, request.ProjectId, validationEvidenceIds.Concat(stepEvidenceIds), cancellationToken);
        await EnsureValidationEvidenceTargetsExistAsync(dbContext, request.ProjectId, request.ValidationEvidence, cancellationToken);
        await EnsureFailureModeTargetsExistAsync(dbContext, request.ProjectId, request.FailureModes, cancellationToken);

        var now = clock.GetUtcNow();
        var skillId = CognitiveMemoryProcedureSkillId.New();
        var maturityTrace = await EvaluateProcedureMaturityAsync(
            request.ProjectId,
            skillId.Value,
            request.InitialMaturity,
            request.RiskLevel,
            request.ValidationState,
            request.ValidationEvidence.Count,
            request.FailureModes.Count,
            validationEvidenceIds.Select(id => id.Value).ToArray(),
            now,
            cancellationToken);
        await CognitiveMemoryScoreTracePersistence.AddIfMissingAsync(dbContext, maturityTrace, now, cancellationToken);

        var skill = new CognitiveMemoryProcedureSkillRecord
        {
            Id = skillId.Value,
            ProjectId = request.ProjectId,
            Title = CognitiveMemoryGuard.EnsureText(request.Title, nameof(request.Title)),
            Purpose = CognitiveMemoryGuard.EnsureText(request.Purpose, nameof(request.Purpose)),
            Maturity = request.InitialMaturity,
            RiskLevel = request.RiskLevel,
            ValidationState = request.ValidationState,
            AccessLevel = request.AccessLevel,
            SourceConsolidationCandidateId = request.SourceConsolidationCandidateId?.Value,
            LastSuccessfulEpisodeId = request.LastSuccessfulEpisodeId?.Value,
            MaturityScoreEvaluationTraceId = maturityTrace.Id.Value,
            MaturityBucket = maturityTrace.ScalarProjection?.Bucket ?? CognitiveMemoryScoreProjectionBucket.Unknown,
            DisplayMaturityScore = maturityTrace.ScalarProjection?.DisplayScore,
            PreconditionsJson = SerializeStringArray(request.Preconditions),
            PostconditionsJson = SerializeStringArray(request.Postconditions),
            RequiredParticipantsJson = SerializeStringArray(request.RequiredRoles ?? []),
            RequiredToolKeysJson = SerializeStringArray(request.RequiredToolKeys ?? []),
            InputSchemaJson = NormalizeJsonObject(request.InputSchemaJson, nameof(request.InputSchemaJson)),
            OutputSchemaJson = NormalizeJsonObject(request.OutputSchemaJson, nameof(request.OutputSchemaJson)),
            StepCount = request.Steps.Count,
            FailureModeCount = request.FailureModes.Count,
            ValidationEvidenceCount = request.ValidationEvidence.Count,
            AlgorithmVersion = AlgorithmVersion,
            MetadataJson = SerializeMetadata(request.Metadata),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.Add(skill);
        AddSteps(dbContext, skill, request.Steps, now);
        AddFailureModes(dbContext, skill, request.FailureModes, now);
        AddValidationEvidence(dbContext, skill, request.ValidationEvidence, now);

        await dbContext.SaveChangesAsync(cancellationToken);
        return skill;
    }

    public async ValueTask<CognitiveMemoryProcedureSkillRecord> UpdateMaturityAsync(
        CognitiveMemoryProcedureMaturityUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TargetMaturity == CognitiveMemoryProcedureSkillMaturity.Unknown)
        {
            throw new ArgumentException("Procedure maturity target must be explicit.", nameof(request));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var skill = await dbContext.Set<CognitiveMemoryProcedureSkillRecord>()
            .FirstOrDefaultAsync(item => item.Id == request.SkillId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Procedure skill '{request.SkillId}' does not exist.");
        ValidatePolicyTrace(skill.ProjectId, request.PolicyContext);
        await EnsureEvidenceAnchorsExistAsync(
            dbContext,
            skill.ProjectId,
            request.AdditionalValidationEvidence.Select(evidence => evidence.EvidenceAnchorId),
            cancellationToken);
        await EnsureValidationEvidenceTargetsExistAsync(dbContext, skill.ProjectId, request.AdditionalValidationEvidence, cancellationToken);
        await EnsureEpisodeExistsAsync(dbContext, skill.ProjectId, request.LastSuccessfulEpisodeId, cancellationToken);

        var validationState = request.ValidationState ?? skill.ValidationState;
        var riskLevel = request.RiskLevel ?? skill.RiskLevel;
        var existingEvidenceCount = await dbContext.Set<CognitiveMemoryProcedureValidationEvidenceRecord>()
            .CountAsync(evidence => evidence.ProcedureSkillId == skill.Id, cancellationToken);
        var existingEvidenceKeys = await dbContext.Set<CognitiveMemoryProcedureValidationEvidenceRecord>()
            .Where(evidence => evidence.ProcedureSkillId == skill.Id)
            .Select(evidence => new { evidence.EvidenceRole, evidence.EvidenceAnchorId })
            .ToListAsync(cancellationToken);
        var existingEvidenceKeySet = existingEvidenceKeys
            .Select(evidence => (evidence.EvidenceRole, evidence.EvidenceAnchorId))
            .ToHashSet();
        var newEvidence = request.AdditionalValidationEvidence
            .Where(evidence => !existingEvidenceKeySet.Contains((evidence.EvidenceRole, evidence.EvidenceAnchorId.Value)))
            .ToArray();
        var validationEvidenceCount = existingEvidenceCount + newEvidence.Length;
        if (request.TargetMaturity == CognitiveMemoryProcedureSkillMaturity.Automatable &&
            !CanBecomeAutomatable(validationState, riskLevel, validationEvidenceCount))
        {
            throw new InvalidOperationException("Procedure skill cannot become automatable until it has validation evidence, approved or human-reviewed state, and non-high risk.");
        }

        var now = clock.GetUtcNow();
        foreach (var evidence in newEvidence)
        {
            dbContext.Add(CreateValidationEvidence(skill, evidence, now));
        }

        var maturityTrace = await EvaluateProcedureMaturityAsync(
            skill.ProjectId,
            skill.Id,
            request.TargetMaturity,
            riskLevel,
            validationState,
            validationEvidenceCount,
            skill.FailureModeCount,
            newEvidence.Select(evidence => evidence.EvidenceAnchorId.Value).ToArray(),
            now,
            cancellationToken);
        await CognitiveMemoryScoreTracePersistence.AddIfMissingAsync(dbContext, maturityTrace, now, cancellationToken);

        skill.Maturity = request.TargetMaturity;
        skill.RiskLevel = riskLevel;
        skill.ValidationState = validationState;
        skill.LastSuccessfulEpisodeId = request.LastSuccessfulEpisodeId?.Value ?? skill.LastSuccessfulEpisodeId;
        skill.ValidationEvidenceCount = validationEvidenceCount;
        skill.MaturityScoreEvaluationTraceId = maturityTrace.Id.Value;
        skill.MaturityBucket = maturityTrace.ScalarProjection?.Bucket ?? CognitiveMemoryScoreProjectionBucket.Unknown;
        skill.DisplayMaturityScore = maturityTrace.ScalarProjection?.DisplayScore;
        skill.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return skill;
    }

    public async ValueTask<CognitiveMemoryProcedureAutomationBindingRecord> RequestAutomationBindingAsync(
        CognitiveMemoryProcedureAutomationBindingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.BindingKind == CognitiveMemoryProcedureAutomationBindingKind.Unknown)
        {
            throw new ArgumentException("Procedure automation binding kind must be explicit.", nameof(request));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var skill = await dbContext.Set<CognitiveMemoryProcedureSkillRecord>()
            .FirstOrDefaultAsync(item => item.Id == request.SkillId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Procedure skill '{request.SkillId}' does not exist.");
        ValidatePolicyTrace(skill.ProjectId, request.PolicyContext);
        if (request.ReviewItemId is not null)
        {
            await EnsureReviewItemExistsAsync(dbContext, skill.ProjectId, request.ReviewItemId, cancellationToken);
        }

        var bindingKey = CognitiveMemoryGuard.EnsureText(request.BindingKey, nameof(request.BindingKey));
        var decision = ResolveAutomationBinding(skill, request);
        var now = clock.GetUtcNow();
        var binding = await dbContext.Set<CognitiveMemoryProcedureAutomationBindingRecord>()
            .FirstOrDefaultAsync(
                item => item.ProcedureSkillId == skill.Id &&
                        item.BindingKind == request.BindingKind &&
                        item.BindingKey == bindingKey,
                cancellationToken);
        if (binding is null)
        {
            binding = new CognitiveMemoryProcedureAutomationBindingRecord
            {
                ProcedureSkillId = skill.Id,
                ProjectId = skill.ProjectId,
                BindingKind = request.BindingKind,
                BindingKey = bindingKey,
                CreatedAtUtc = now,
                ConcurrencyToken = Guid.NewGuid()
            };
            dbContext.Add(binding);
            skill.AutomationBindingCount++;
        }

        binding.State = decision.State;
        binding.RequiresHumanReview = decision.RequiresHumanReview;
        binding.ReviewItemId = request.ReviewItemId?.Value;
        binding.RejectionCode = decision.RejectionCode;
        binding.RejectionReason = decision.RejectionReason;
        binding.UpdatedAtUtc = now;
        skill.UpdatedAtUtc = now;
        if (binding.State != CognitiveMemoryProcedureAutomationBindingState.Bound)
        {
            logger.LogInformation(
                "Procedure automation binding {BindingKind}:{BindingKey} for skill {ProcedureSkillId} is {State}: {RejectionCode}.",
                binding.BindingKind,
                binding.BindingKey,
                skill.Id,
                binding.State,
                binding.RejectionCode);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return binding;
    }

    public async ValueTask<CognitiveMemoryProcedureSimulationRecord> SimulateAsync(
        CognitiveMemoryProcedureSimulationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSimulationRequest(request);
        ValidatePolicyTrace(request.ProjectId, request.PolicyContext);
        cancellationToken.ThrowIfCancellationRequested();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var relatedSkills = await LoadAndValidateSimulationSkillsAsync(dbContext, request, cancellationToken);
        await EnsureEvidenceAnchorsExistAsync(dbContext, request.ProjectId, request.EvidenceAnchorIds, cancellationToken);

        var now = clock.GetUtcNow();
        var simulationId = CognitiveMemoryProcedureSimulationId.New();
        var crossProject = relatedSkills.Any(skill => skill.ProjectId != request.ProjectId);
        var riskTrace = await EvaluateSimulationRiskAsync(
            request,
            simulationId.Value,
            crossProject,
            now,
            cancellationToken);
        await CognitiveMemoryScoreTracePersistence.AddIfMissingAsync(dbContext, riskTrace, now, cancellationToken);

        var simulation = new CognitiveMemoryProcedureSimulationRecord
        {
            Id = simulationId.Value,
            ProjectId = request.ProjectId,
            OutputKind = request.OutputKind,
            Status = ResolveSimulationStatus(request.RiskLevel, crossProject),
            Summary = CognitiveMemoryGuard.EnsureText(request.Summary, nameof(request.Summary)),
            IsSpeculative = true,
            SpeculationLabel = SpeculationLabel,
            RiskLevel = request.RiskLevel,
            RiskScoreEvaluationTraceId = riskTrace.Id.Value,
            RiskBucket = riskTrace.ScalarProjection?.Bucket ?? CognitiveMemoryScoreProjectionBucket.Unknown,
            DisplayRiskScore = riskTrace.ScalarProjection?.DisplayScore,
            PolicyProfileId = request.PolicyContext.PolicyProfileId.Value,
            SourceScopeKey = NormalizeSourceScope(request.SourceScopeKey, request.ProjectId),
            RequiredValidationStepsJson = SerializeStringArray(request.RequiredValidationSteps),
            MetadataJson = SerializeMetadata(request.Metadata),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.Add(simulation);
        foreach (var skill in relatedSkills)
        {
            dbContext.Add(new CognitiveMemoryProcedureSimulationSkillRecord
            {
                SimulationId = simulation.Id,
                ProcedureSkillId = skill.Id,
                ProjectId = request.ProjectId,
                CreatedAtUtc = now
            });
        }

        foreach (var evidenceAnchorId in request.EvidenceAnchorIds.Select(id => id.Value).Distinct())
        {
            dbContext.Add(new CognitiveMemoryProcedureSimulationEvidenceRecord
            {
                SimulationId = simulation.Id,
                ProjectId = request.ProjectId,
                EvidenceAnchorId = evidenceAnchorId,
                CreatedAtUtc = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return simulation;
    }

    private async Task<CognitiveMemoryScoreEvaluationTrace> EvaluateProcedureMaturityAsync(
        Guid projectId,
        Guid skillId,
        CognitiveMemoryProcedureSkillMaturity maturity,
        CognitiveMemoryRiskLevel riskLevel,
        CognitiveMemoryValidationState validationState,
        int validationEvidenceCount,
        int failureModeCount,
        IReadOnlyList<Guid> evidenceAnchorIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var evidenceRefs = BuildEvidenceRefs(evidenceAnchorIds, CognitiveMemoryScoreEvidenceKind.EvidenceAnchor, now);
        if (evidenceRefs.Count == 0)
        {
            evidenceRefs = [new CognitiveMemoryScoreEvidenceRef(CognitiveMemoryScoreEvidenceKind.ProcedureSkill, skillId, 1, now)];
        }

        var vector = new CognitiveMemoryScoreVectorSnapshot(
            CognitiveMemoryScoreSpaceKind.ProcedureMaturity,
            CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion,
            CognitiveMemoryScoreSpaceRegistry.CurrentNormalizationProfile,
            [
                Component(CognitiveMemoryScoreDimensionKind.ProcedureMaturity, MaturityValue(maturity), 1, evidenceRefs),
                Component(CognitiveMemoryScoreDimensionKind.EvidenceStrength, EvidenceStrengthValue(validationEvidenceCount), 0.9, evidenceRefs),
                Component(CognitiveMemoryScoreDimensionKind.SourceReliability, EvidenceStrengthValue(validationEvidenceCount), 0.8, evidenceRefs),
                Component(CognitiveMemoryScoreDimensionKind.FailureRecurrence, Math.Clamp(failureModeCount / 5.0, 0, 1), 0.8, evidenceRefs),
                Component(CognitiveMemoryScoreDimensionKind.RiskImpact, RiskValue(riskLevel), 1, evidenceRefs),
                Component(CognitiveMemoryScoreDimensionKind.HumanValidation, HumanValidationValue(validationState), 1, evidenceRefs)
            ],
            new CognitiveMemoryAlgorithmVersion(AlgorithmVersion),
            now,
            CognitiveMemoryHash.FromUtf8($"{skillId:D}|{maturity}|{riskLevel}|{validationState}|{validationEvidenceCount}|{failureModeCount}"));

        return await scoreGeometryDriver.EvaluateAsync(
            new CognitiveMemoryScoreEvaluationRequest(
                projectId,
                CognitiveMemoryScoreOwnerKind.ProcedureSkill,
                skillId,
                CognitiveMemoryScoreSpaceKind.ProcedureMaturity,
                CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion,
                [vector],
                ProcedureMaturityShapes),
            cancellationToken);
    }

    private async Task<CognitiveMemoryScoreEvaluationTrace> EvaluateSimulationRiskAsync(
        CognitiveMemoryProcedureSimulationRequest request,
        Guid simulationId,
        bool crossProject,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var evidenceRefs = BuildEvidenceRefs(request.EvidenceAnchorIds.Select(id => id.Value).ToArray(), CognitiveMemoryScoreEvidenceKind.EvidenceAnchor, now);
        if (evidenceRefs.Count == 0)
        {
            evidenceRefs = [new CognitiveMemoryScoreEvidenceRef(CognitiveMemoryScoreEvidenceKind.ProcedureSimulation, simulationId, 1, now)];
        }

        var vector = new CognitiveMemoryScoreVectorSnapshot(
            CognitiveMemoryScoreSpaceKind.SimulationRisk,
            CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion,
            CognitiveMemoryScoreSpaceRegistry.CurrentNormalizationProfile,
            [
                Component(CognitiveMemoryScoreDimensionKind.RiskImpact, RiskValue(request.RiskLevel), 1, evidenceRefs),
                Component(CognitiveMemoryScoreDimensionKind.SourceSufficiency, EvidenceStrengthValue(request.EvidenceAnchorIds.Count), 0.9, evidenceRefs),
                Component(CognitiveMemoryScoreDimensionKind.AccessPolicyRisk, crossProject ? 0.65 : 0.1, 0.9, evidenceRefs),
                Component(CognitiveMemoryScoreDimensionKind.ContextSeparation, crossProject ? 0.75 : 0.15, 0.9, evidenceRefs),
                Component(CognitiveMemoryScoreDimensionKind.SourceReusePermission, request.AllowCrossProjectAnalogies ? 0.8 : 0.2, 0.8, evidenceRefs),
                Component(CognitiveMemoryScoreDimensionKind.PolicyCompatibility, request.PolicyContext.AccessLevel == CognitiveMemoryAccessLevel.Restricted ? 0.35 : 0.8, 0.8, evidenceRefs),
                Component(CognitiveMemoryScoreDimensionKind.HumanValidation, request.RiskLevel == CognitiveMemoryRiskLevel.High ? 0.2 : 0.6, 0.7, evidenceRefs)
            ],
            new CognitiveMemoryAlgorithmVersion(AlgorithmVersion),
            now,
            CognitiveMemoryHash.FromUtf8($"{simulationId:D}|{request.OutputKind}|{request.RiskLevel}|{crossProject}|{request.PolicyContext.PolicyProfileId.Value}|{request.EvidenceAnchorIds.Count}"));

        return await scoreGeometryDriver.EvaluateAsync(
            new CognitiveMemoryScoreEvaluationRequest(
                request.ProjectId,
                CognitiveMemoryScoreOwnerKind.ProcedureSimulation,
                simulationId,
                CognitiveMemoryScoreSpaceKind.SimulationRisk,
                CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion,
                [vector],
                SimulationRiskShapes),
            cancellationToken);
    }

    private static void AddSteps(
        AppDbContext dbContext,
        CognitiveMemoryProcedureSkillRecord skill,
        IReadOnlyList<CognitiveMemoryProcedureStepDraft> drafts,
        DateTimeOffset now)
    {
        foreach (var draft in drafts.OrderBy(step => step.Order))
        {
            var step = new CognitiveMemoryProcedureStepRecord
            {
                Id = CognitiveMemoryProcedureStepId.New().Value,
                ProcedureSkillId = skill.Id,
                ProjectId = skill.ProjectId,
                StepKey = CognitiveMemoryGuard.EnsureText(draft.StepKey, nameof(draft.StepKey)),
                SequenceIndex = draft.Order,
                Action = CognitiveMemoryGuard.EnsureText(draft.Action, nameof(draft.Action)),
                RequiredInput = draft.RequiredInput.Trim(),
                ExpectedOutput = CognitiveMemoryGuard.EnsureText(draft.ExpectedOutput, nameof(draft.ExpectedOutput)),
                ValidationCheck = CognitiveMemoryGuard.EnsureText(draft.ValidationCheck, nameof(draft.ValidationCheck)),
                FailureHandling = draft.FailureHandling.Trim(),
                ToolBindingKey = draft.ToolBindingKey.Trim(),
                TimeoutSeconds = draft.TimeoutSeconds,
                RetryLimit = draft.RetryLimit,
                IsRollbackStep = draft.IsRollbackStep,
                MetadataJson = SerializeMetadata(draft.Metadata),
                CreatedAtUtc = now,
                ConcurrencyToken = Guid.NewGuid()
            };
            dbContext.Add(step);
            foreach (var evidenceAnchorId in draft.EvidenceAnchorIds?.Select(id => id.Value).Distinct() ?? [])
            {
                dbContext.Add(new CognitiveMemoryProcedureStepEvidenceRecord
                {
                    ProcedureStepId = step.Id,
                    ProcedureSkillId = skill.Id,
                    ProjectId = skill.ProjectId,
                    EvidenceAnchorId = evidenceAnchorId,
                    CreatedAtUtc = now
                });
            }
        }
    }

    private static void AddFailureModes(
        AppDbContext dbContext,
        CognitiveMemoryProcedureSkillRecord skill,
        IReadOnlyList<CognitiveMemoryProcedureFailureModeDraft> drafts,
        DateTimeOffset now)
    {
        foreach (var draft in drafts)
        {
            var failureMode = new CognitiveMemoryProcedureFailureModeRecord
            {
                Id = CognitiveMemoryProcedureFailureModeId.New().Value,
                ProcedureSkillId = skill.Id,
                ProjectId = skill.ProjectId,
                FailureKey = CognitiveMemoryGuard.EnsureText(draft.FailureKey, nameof(draft.FailureKey)),
                Condition = CognitiveMemoryGuard.EnsureText(draft.Condition, nameof(draft.Condition)),
                DetectionSignal = CognitiveMemoryGuard.EnsureText(draft.DetectionSignal, nameof(draft.DetectionSignal)),
                LikelyCause = CognitiveMemoryGuard.EnsureText(draft.LikelyCause, nameof(draft.LikelyCause)),
                Mitigation = CognitiveMemoryGuard.EnsureText(draft.Mitigation, nameof(draft.Mitigation)),
                RollbackOrCompensation = draft.RollbackOrCompensation.Trim(),
                MetadataJson = SerializeMetadata(draft.Metadata),
                CreatedAtUtc = now,
                ConcurrencyToken = Guid.NewGuid()
            };
            dbContext.Add(failureMode);
            foreach (var predictionErrorId in draft.RelatedPredictionErrorIds?.Select(id => id.Value).Distinct() ?? [])
            {
                dbContext.Add(new CognitiveMemoryProcedureFailureModePredictionErrorRecord
                {
                    ProcedureFailureModeId = failureMode.Id,
                    ProcedureSkillId = skill.Id,
                    ProjectId = skill.ProjectId,
                    PredictionErrorId = predictionErrorId,
                    CreatedAtUtc = now
                });
            }

            foreach (var episodeId in draft.RelatedEpisodeIds?.Select(id => id.Value).Distinct() ?? [])
            {
                dbContext.Add(new CognitiveMemoryProcedureFailureModeEpisodeRecord
                {
                    ProcedureFailureModeId = failureMode.Id,
                    ProcedureSkillId = skill.Id,
                    ProjectId = skill.ProjectId,
                    EpisodeId = episodeId,
                    CreatedAtUtc = now
                });
            }
        }
    }

    private static void AddValidationEvidence(
        AppDbContext dbContext,
        CognitiveMemoryProcedureSkillRecord skill,
        IReadOnlyList<CognitiveMemoryProcedureValidationEvidenceDraft> drafts,
        DateTimeOffset now)
    {
        foreach (var draft in drafts)
        {
            dbContext.Add(CreateValidationEvidence(skill, draft, now));
        }
    }

    private static CognitiveMemoryProcedureValidationEvidenceRecord CreateValidationEvidence(
        CognitiveMemoryProcedureSkillRecord skill,
        CognitiveMemoryProcedureValidationEvidenceDraft draft,
        DateTimeOffset now)
    {
        if (draft.EvidenceRole == CognitiveMemoryProcedureValidationEvidenceRole.Unknown)
        {
            throw new ArgumentException("Procedure validation evidence role must be explicit.", nameof(draft));
        }

        return new CognitiveMemoryProcedureValidationEvidenceRecord
        {
            ProcedureSkillId = skill.Id,
            ProjectId = skill.ProjectId,
            EvidenceRole = draft.EvidenceRole,
            EvidenceAnchorId = draft.EvidenceAnchorId.Value,
            EpisodeId = draft.EpisodeId?.Value,
            ReviewItemId = draft.ReviewItemId?.Value,
            Summary = CognitiveMemoryGuard.EnsureText(draft.Summary, nameof(draft.Summary)),
            CreatedAtUtc = now
        };
    }

    private static CognitiveMemoryProcedureAutomationBindingDecision ResolveAutomationBinding(
        CognitiveMemoryProcedureSkillRecord skill,
        CognitiveMemoryProcedureAutomationBindingRequest request)
    {
        if (skill.Maturity != CognitiveMemoryProcedureSkillMaturity.Automatable)
        {
            return CognitiveMemoryProcedureAutomationBindingDecision.Rejected(RejectionImmature, "Procedure skill maturity is not Automatable.");
        }

        if (skill.ValidationEvidenceCount == 0)
        {
            return CognitiveMemoryProcedureAutomationBindingDecision.Rejected(RejectionMissingEvidence, "Procedure skill has no validation evidence.");
        }

        if (!IsValidationAccepted(skill.ValidationState))
        {
            return CognitiveMemoryProcedureAutomationBindingDecision.Rejected(RejectionMissingReview, "Procedure skill is not approved or human-reviewed.");
        }

        if (skill.RiskLevel == CognitiveMemoryRiskLevel.High && !request.HumanReviewApproved)
        {
            return CognitiveMemoryProcedureAutomationBindingDecision.NeedsReview(RejectionHighRiskReview, "High-risk procedure automation requires explicit human review approval.");
        }

        return CognitiveMemoryProcedureAutomationBindingDecision.Bound();
    }

    private static bool CanBecomeAutomatable(
        CognitiveMemoryValidationState validationState,
        CognitiveMemoryRiskLevel riskLevel,
        int validationEvidenceCount)
        => validationEvidenceCount > 0 &&
           riskLevel != CognitiveMemoryRiskLevel.High &&
           IsValidationAccepted(validationState);

    private static bool IsValidationAccepted(CognitiveMemoryValidationState validationState)
        => validationState is CognitiveMemoryValidationState.HumanReviewed or CognitiveMemoryValidationState.Approved;

    private static CognitiveMemoryProcedureSimulationStatus ResolveSimulationStatus(
        CognitiveMemoryRiskLevel riskLevel,
        bool crossProject)
        => riskLevel == CognitiveMemoryRiskLevel.High || crossProject
            ? CognitiveMemoryProcedureSimulationStatus.NeedsReview
            : CognitiveMemoryProcedureSimulationStatus.Speculative;

    private async Task<IReadOnlyList<CognitiveMemoryProcedureSkillRecord>> LoadAndValidateSimulationSkillsAsync(
        AppDbContext dbContext,
        CognitiveMemoryProcedureSimulationRequest request,
        CancellationToken cancellationToken)
    {
        var requestedSkillIds = request.RelatedProcedureSkillIds.Select(id => id.Value).Distinct().ToArray();
        if (requestedSkillIds.Length == 0)
        {
            return [];
        }

        var skills = await dbContext.Set<CognitiveMemoryProcedureSkillRecord>()
            .Where(skill => requestedSkillIds.Contains(skill.Id))
            .ToListAsync(cancellationToken);
        var foundIds = skills.Select(skill => skill.Id).ToHashSet();
        var missing = requestedSkillIds.FirstOrDefault(id => !foundIds.Contains(id));
        if (missing != Guid.Empty)
        {
            throw new InvalidOperationException($"Procedure skill '{missing:D}' does not exist.");
        }

        foreach (var skill in skills.Where(skill => skill.ProjectId != request.ProjectId))
        {
            if (!request.AllowCrossProjectAnalogies)
            {
                throw new InvalidOperationException($"Cross-project procedure analogy for skill '{skill.Id:D}' was not requested explicitly.");
            }

            if (skill.AccessLevel != CognitiveMemoryAccessLevel.Public)
            {
                throw new InvalidOperationException($"Cross-project procedure analogy for skill '{skill.Id:D}' is blocked by access policy.");
            }
        }

        return skills;
    }

    private static async Task EnsureEvidenceAnchorsExistAsync(
        AppDbContext dbContext,
        Guid projectId,
        IEnumerable<CognitiveMemoryEvidenceAnchorId> evidenceAnchorIds,
        CancellationToken cancellationToken)
        => await EnsureEvidenceAnchorsExistAsync(dbContext, projectId, evidenceAnchorIds.Select(id => id.Value), cancellationToken);

    private static async Task EnsureEvidenceAnchorsExistAsync(
        AppDbContext dbContext,
        Guid projectId,
        IEnumerable<Guid> evidenceAnchorIds,
        CancellationToken cancellationToken)
    {
        var ids = evidenceAnchorIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        var found = await dbContext.Set<CognitiveMemoryEvidenceAnchorRecord>()
            .Where(anchor => ids.Contains(anchor.Id) && (anchor.ProjectId == null || anchor.ProjectId == projectId))
            .Select(anchor => anchor.Id)
            .ToListAsync(cancellationToken);
        var missing = ids.Except(found).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException($"Evidence anchor '{missing[0]:D}' does not exist in project '{projectId:D}'.");
        }
    }

    private static async Task EnsureValidationEvidenceTargetsExistAsync(
        AppDbContext dbContext,
        Guid projectId,
        IReadOnlyList<CognitiveMemoryProcedureValidationEvidenceDraft> drafts,
        CancellationToken cancellationToken)
    {
        foreach (var draft in drafts)
        {
            await EnsureEpisodeExistsAsync(dbContext, projectId, draft.EpisodeId, cancellationToken);
            await EnsureReviewItemExistsAsync(dbContext, projectId, draft.ReviewItemId, cancellationToken);
        }
    }

    private static async Task EnsureFailureModeTargetsExistAsync(
        AppDbContext dbContext,
        Guid projectId,
        IReadOnlyList<CognitiveMemoryProcedureFailureModeDraft> drafts,
        CancellationToken cancellationToken)
    {
        var predictionErrorIds = drafts
            .SelectMany(draft => draft.RelatedPredictionErrorIds ?? [])
            .Select(id => id.Value)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        if (predictionErrorIds.Length > 0)
        {
            var found = await dbContext.Set<CognitiveMemoryPredictionErrorRecord>()
                .Where(error => error.ProjectId == projectId && predictionErrorIds.Contains(error.Id))
                .Select(error => error.Id)
                .ToListAsync(cancellationToken);
            var missing = predictionErrorIds.Except(found).ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidOperationException($"Prediction error '{missing[0]:D}' does not exist in project '{projectId:D}'.");
            }
        }

        foreach (var episodeId in drafts.SelectMany(draft => draft.RelatedEpisodeIds ?? []))
        {
            await EnsureEpisodeExistsAsync(dbContext, projectId, episodeId, cancellationToken);
        }
    }

    private static async Task EnsureContextFramesExistAsync(
        AppDbContext dbContext,
        Guid projectId,
        IReadOnlyList<Guid> contextFrameIds,
        CancellationToken cancellationToken)
    {
        var ids = contextFrameIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        var found = await dbContext.Set<CognitiveMemoryContextFrameRecord>()
            .Where(frame => ids.Contains(frame.Id) && (frame.ProjectId == null || frame.ProjectId == projectId))
            .Select(frame => frame.Id)
            .ToListAsync(cancellationToken);
        var missing = ids.Except(found).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException($"Context frame '{missing[0]:D}' does not exist in project '{projectId:D}'.");
        }
    }

    private static async Task EnsureConsolidationCandidateCanSourceProcedureAsync(
        AppDbContext dbContext,
        Guid projectId,
        CognitiveMemoryConsolidationCandidateId? candidateId,
        CancellationToken cancellationToken)
    {
        if (candidateId is null)
        {
            return;
        }

        var candidate = await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == candidateId.Value.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Consolidation candidate '{candidateId}' does not exist.");
        if (candidate.ProjectId != projectId || candidate.CandidateKind != CognitiveMemoryConsolidationCandidateKind.Procedure)
        {
            throw new InvalidOperationException($"Consolidation candidate '{candidateId}' is not a procedure candidate in project '{projectId:D}'.");
        }
    }

    private static async Task EnsureEpisodeExistsAsync(
        AppDbContext dbContext,
        Guid projectId,
        CognitiveMemoryTemporalEpisodeId? episodeId,
        CancellationToken cancellationToken)
    {
        if (episodeId is null)
        {
            return;
        }

        var exists = await dbContext.Set<CognitiveMemoryTemporalEpisodeRecord>()
            .AnyAsync(episode => episode.ProjectId == projectId && episode.Id == episodeId.Value.Value, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException($"Temporal episode '{episodeId}' does not exist in project '{projectId:D}'.");
        }
    }

    private static async Task EnsureReviewItemExistsAsync(
        AppDbContext dbContext,
        Guid projectId,
        CognitiveMemoryReviewItemId? reviewItemId,
        CancellationToken cancellationToken)
    {
        if (reviewItemId is null)
        {
            return;
        }

        var exists = await dbContext.Set<CognitiveMemoryReviewItemRecord>()
            .AnyAsync(review => review.ProjectId == projectId && review.Id == reviewItemId.Value.Value, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException($"Review item '{reviewItemId}' does not exist in project '{projectId:D}'.");
        }
    }

    private static void ValidateSkillProposal(CognitiveMemoryProcedureSkillProposalRequest request)
    {
        CognitiveMemoryGuard.EnsureNonEmpty(request.ProjectId, nameof(request.ProjectId));
        if (request.InitialMaturity == CognitiveMemoryProcedureSkillMaturity.Unknown)
        {
            throw new ArgumentException("Procedure skill maturity must be explicit.", nameof(request));
        }

        if (request.Steps.Count == 0)
        {
            throw new ArgumentException("Procedure skill requires at least one step.", nameof(request));
        }

        var orders = request.Steps.Select(step => step.Order).ToArray();
        if (orders.Any(order => order <= 0) || orders.Distinct().Count() != orders.Length)
        {
            throw new ArgumentException("Procedure step order values must be positive and unique.", nameof(request));
        }
    }

    private static void ValidateSimulationRequest(CognitiveMemoryProcedureSimulationRequest request)
    {
        CognitiveMemoryGuard.EnsureNonEmpty(request.ProjectId, nameof(request.ProjectId));
        if (request.OutputKind == CognitiveMemoryProcedureSimulationOutputKind.Unknown)
        {
            throw new ArgumentException("Simulation output kind must be explicit.", nameof(request));
        }

        if (request.RequiredValidationSteps.Count == 0)
        {
            throw new ArgumentException("Simulation output requires validation steps before it can be used.", nameof(request));
        }
    }

    private static void ValidatePolicyTrace(Guid projectId, CognitiveMemoryPolicyContext policyContext)
    {
        if (policyContext.ProjectId != projectId)
        {
            throw new ArgumentException($"Policy context project '{policyContext.ProjectId:D}' does not match project '{projectId:D}'.", nameof(policyContext));
        }
    }

    private static IReadOnlyList<CognitiveMemoryScoreEvidenceRef> BuildEvidenceRefs(
        IReadOnlyList<Guid> ids,
        CognitiveMemoryScoreEvidenceKind evidenceKind,
        DateTimeOffset now)
        => ids
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Select(id => new CognitiveMemoryScoreEvidenceRef(evidenceKind, id, 1, now))
            .ToArray();

    private static CognitiveMemoryScoreComponent Component(
        CognitiveMemoryScoreDimensionKind dimensionKind,
        double value,
        double confidence,
        IReadOnlyList<CognitiveMemoryScoreEvidenceRef> evidenceRefs)
        => new(dimensionKind, Math.Clamp(value, 0, 1), Math.Clamp(confidence, 0, 1), evidenceRefs);

    private static double MaturityValue(CognitiveMemoryProcedureSkillMaturity maturity)
        => maturity switch
        {
            CognitiveMemoryProcedureSkillMaturity.Draft => 0.1,
            CognitiveMemoryProcedureSkillMaturity.Observed => 0.35,
            CognitiveMemoryProcedureSkillMaturity.Reviewed => 0.55,
            CognitiveMemoryProcedureSkillMaturity.Validated => 0.75,
            CognitiveMemoryProcedureSkillMaturity.Automatable => 0.95,
            CognitiveMemoryProcedureSkillMaturity.Deprecated => 0,
            _ => 0
        };

    private static double EvidenceStrengthValue(int evidenceCount)
        => evidenceCount switch
        {
            <= 0 => 0.05,
            1 => 0.4,
            2 => 0.7,
            _ => 0.9
        };

    private static double HumanValidationValue(CognitiveMemoryValidationState validationState)
        => validationState switch
        {
            CognitiveMemoryValidationState.Approved => 1,
            CognitiveMemoryValidationState.HumanReviewed => 0.85,
            CognitiveMemoryValidationState.NeedsHumanReview => 0.3,
            CognitiveMemoryValidationState.Rejected => 0,
            _ => 0.1
        };

    private static double RiskValue(CognitiveMemoryRiskLevel riskLevel)
        => riskLevel switch
        {
            CognitiveMemoryRiskLevel.Low => 0.15,
            CognitiveMemoryRiskLevel.Medium => 0.55,
            CognitiveMemoryRiskLevel.High => 0.9,
            _ => 0.55
        };

    private static IReadOnlyList<CognitiveMemoryScoreShapeSnapshot> BuildProcedureMaturityShapes()
    {
        var algorithm = new CognitiveMemoryAlgorithmVersion(AlgorithmVersion);
        return
        [
            Shape(CognitiveMemoryScoreSpaceKind.ProcedureMaturity, CognitiveMemoryScoreProjectionBucket.StrongAccept, "Procedure is mature, evidenced, reviewed, and low risk.", [Higher(CognitiveMemoryScoreDimensionKind.ProcedureMaturity, 0.8), Higher(CognitiveMemoryScoreDimensionKind.EvidenceStrength, 0.7), Higher(CognitiveMemoryScoreDimensionKind.HumanValidation, 0.75), Lower(CognitiveMemoryScoreDimensionKind.RiskImpact, 0.35)], algorithm),
            Shape(CognitiveMemoryScoreSpaceKind.ProcedureMaturity, CognitiveMemoryScoreProjectionBucket.NeedsReview, "Procedure has some validation but still needs review before automation.", [Higher(CognitiveMemoryScoreDimensionKind.ProcedureMaturity, 0.5), Higher(CognitiveMemoryScoreDimensionKind.EvidenceStrength, 0.4)], algorithm),
            Shape(CognitiveMemoryScoreSpaceKind.ProcedureMaturity, CognitiveMemoryScoreProjectionBucket.Reject, "Procedure is draft, weakly evidenced, or too risky for automation.", [Lower(CognitiveMemoryScoreDimensionKind.ProcedureMaturity, 0.35)], algorithm)
        ];
    }

    private static IReadOnlyList<CognitiveMemoryScoreShapeSnapshot> BuildSimulationRiskShapes()
    {
        var algorithm = new CognitiveMemoryAlgorithmVersion(AlgorithmVersion);
        return
        [
            Shape(CognitiveMemoryScoreSpaceKind.SimulationRisk, CognitiveMemoryScoreProjectionBucket.NeedsReview, "Simulation has high risk or cross-context pressure and remains review-required.", [Higher(CognitiveMemoryScoreDimensionKind.RiskImpact, 0.75), Higher(CognitiveMemoryScoreDimensionKind.ContextSeparation, 0.6)], algorithm),
            Shape(CognitiveMemoryScoreSpaceKind.SimulationRisk, CognitiveMemoryScoreProjectionBucket.WeakAccept, "Simulation is low-risk but remains speculative until source-backed and reviewed.", [Lower(CognitiveMemoryScoreDimensionKind.RiskImpact, 0.35), Higher(CognitiveMemoryScoreDimensionKind.SourceSufficiency, 0.35)], algorithm)
        ];
    }

    private static CognitiveMemoryScoreShapeSnapshot Shape(
        CognitiveMemoryScoreSpaceKind spaceKind,
        CognitiveMemoryScoreProjectionBucket bucket,
        string explanation,
        IReadOnlyList<CognitiveMemoryScoreShapeComponent> components,
        CognitiveMemoryAlgorithmVersion algorithm)
        => new(
            CognitiveMemoryScoreShapeKind.ThresholdEnvelope,
            spaceKind,
            CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion,
            components,
            radius: null,
            bucket,
            explanation,
            [],
            algorithm);

    private static CognitiveMemoryScoreShapeComponent Higher(
        CognitiveMemoryScoreDimensionKind dimensionKind,
        double lowerBound)
        => new(dimensionKind, center: lowerBound, lowerBound, upperBound: null, weight: 1);

    private static CognitiveMemoryScoreShapeComponent Lower(
        CognitiveMemoryScoreDimensionKind dimensionKind,
        double upperBound)
        => new(dimensionKind, center: upperBound, lowerBound: null, upperBound, weight: 1);

    private static string SerializeStringArray(IEnumerable<string> values)
        => JsonSerializer.Serialize(
            values.Select(value => value.Trim()).Where(value => value.Length > 0).ToArray(),
            CognitiveMemoryJsonSerializerContext.Default.StringArray);

    private static string SerializeMetadata(IReadOnlyDictionary<string, string>? metadata)
        => metadata is null || metadata.Count == 0
            ? "{}"
            : JsonSerializer.Serialize(new Dictionary<string, string>(metadata, StringComparer.Ordinal), CognitiveMemoryJson.SerializerOptions);

    private static string NormalizeJsonObject(string json, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "{}";
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Procedure schema JSON must be an object.", parameterName);
        }

        return json.Trim();
    }

    private static string NormalizeSourceScope(string sourceScopeKey, Guid projectId)
        => string.IsNullOrWhiteSpace(sourceScopeKey)
            ? projectId.ToString("D")
            : sourceScopeKey.Trim();

    private sealed record CognitiveMemoryProcedureAutomationBindingDecision(
        CognitiveMemoryProcedureAutomationBindingState State,
        bool RequiresHumanReview,
        string RejectionCode,
        string RejectionReason)
    {
        public static CognitiveMemoryProcedureAutomationBindingDecision Bound()
            => new(CognitiveMemoryProcedureAutomationBindingState.Bound, RequiresHumanReview: false, string.Empty, string.Empty);

        public static CognitiveMemoryProcedureAutomationBindingDecision Rejected(string code, string reason)
            => new(CognitiveMemoryProcedureAutomationBindingState.Rejected, RequiresHumanReview: false, code, reason);

        public static CognitiveMemoryProcedureAutomationBindingDecision NeedsReview(string code, string reason)
            => new(CognitiveMemoryProcedureAutomationBindingState.NeedsReview, RequiresHumanReview: true, code, reason);
    }
}
