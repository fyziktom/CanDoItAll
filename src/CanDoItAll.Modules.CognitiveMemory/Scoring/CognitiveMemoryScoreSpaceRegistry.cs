namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryScoreSpaceRegistry : ICognitiveMemoryScoreSpaceRegistry
{
    public static CognitiveMemoryScoreSchemaVersion CurrentSchemaVersion { get; } = new("score-v1");

    public static CognitiveMemoryScoreNormalizationProfileId CurrentNormalizationProfile { get; } = new("normalized-0-1-v1");

    public static CognitiveMemoryAlgorithmVersion CurrentAlgorithmVersion { get; } = new("score-geometry-v1");

    public static IReadOnlyList<CognitiveMemoryScoreSpaceDefinition> InitialDefinitions { get; } =
    [
        Define(
            CognitiveMemoryScoreSpaceKind.RecallCandidate,
            CognitiveMemoryScoreScalarProjectionKind.UiSorting,
            [
                Required(CognitiveMemoryScoreDimensionKind.SemanticSimilarity, 1, "Semantic similarity as one recall signal."),
                Optional(CognitiveMemoryScoreDimensionKind.LexicalMatch, 0.6, "Lexical match evidence."),
                Optional(CognitiveMemoryScoreDimensionKind.GraphProximity, 0.7, "Graph proximity evidence."),
                Optional(CognitiveMemoryScoreDimensionKind.SpatialProximity, 0.4, "Workbench spatial proximity evidence."),
                Required(CognitiveMemoryScoreDimensionKind.ContextFit, 1.2, "Task and project context fit."),
                Required(CognitiveMemoryScoreDimensionKind.SourceSufficiency, 1.1, "Source backing sufficiency."),
                Optional(CognitiveMemoryScoreDimensionKind.MetadataFit, 0.5, "Memory kind and metadata fit for the recall intent."),
                Optional(CognitiveMemoryScoreDimensionKind.TemporalRecency, 0.4, "Temporal recency as a secondary recall signal."),
                Optional(CognitiveMemoryScoreDimensionKind.MemoryActivation, 0.8, "Recent activation or use."),
                Optional(CognitiveMemoryScoreDimensionKind.EvidenceSupport, 1, "Belief support contribution."),
                Optional(CognitiveMemoryScoreDimensionKind.ContradictionPressure, 1, "Contradiction pressure.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.StalenessPressure, 0.7, "Staleness pressure.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.AccessPolicyRisk, 1, "Access policy risk.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.RedactionPressure, 0.7, "Redaction pressure.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.WorkspaceFocusFit, 0.8, "Current workspace focus fit."),
                Optional(CognitiveMemoryScoreDimensionKind.ContextSeparation, 1.3, "Related but not substitutable context pressure.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.HumanValidation, 0.8, "Human validation or approved-record contribution.")
            ]),
        Define(
            CognitiveMemoryScoreSpaceKind.AttentionRouting,
            CognitiveMemoryScoreScalarProjectionKind.QueueOrdering,
            [
                Required(CognitiveMemoryScoreDimensionKind.SourceSufficiency, 1, "Available source support."),
                Required(CognitiveMemoryScoreDimensionKind.ContextAmbiguity, 1, "Context ambiguity pressure.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.CognitiveLoad, 0.8, "Workspace cognitive load.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.RiskImpact, 1, "Risk impact."),
                Optional(CognitiveMemoryScoreDimensionKind.AvailableWorkspaceEvidence, 1, "Workspace evidence availability."),
                Optional(CognitiveMemoryScoreDimensionKind.MissingKnowledgePressure, 1, "Missing knowledge pressure."),
                Optional(CognitiveMemoryScoreDimensionKind.CalibrationRisk, 1, "Calibration risk.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.ActionCost, 0.5, "Action cost.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.ExpectedValue, 0.8, "Expected value.")
            ]),
        Define(
            CognitiveMemoryScoreSpaceKind.BeliefState,
            CognitiveMemoryScoreScalarProjectionKind.DisplayOnly,
            [
                Required(CognitiveMemoryScoreDimensionKind.EvidenceSupport, 1.2, "Supporting evidence strength."),
                Required(CognitiveMemoryScoreDimensionKind.EvidenceAttack, 1.2, "Attacking evidence pressure.", higherIsBetter: false),
                Required(CognitiveMemoryScoreDimensionKind.SourceQuality, 1, "Source quality."),
                Required(CognitiveMemoryScoreDimensionKind.ContextFit, 1, "Context validity."),
                Optional(CognitiveMemoryScoreDimensionKind.TemporalValidity, 0.8, "Temporal validity."),
                Optional(CognitiveMemoryScoreDimensionKind.HumanValidation, 1.2, "Human validation."),
                Optional(CognitiveMemoryScoreDimensionKind.ContradictionPressure, 1, "Contradiction pressure.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.StalenessPressure, 0.7, "Staleness pressure.", higherIsBetter: false)
            ]),
        Define(
            CognitiveMemoryScoreSpaceKind.SalienceSignal,
            CognitiveMemoryScoreScalarProjectionKind.None,
            [
                Optional(CognitiveMemoryScoreDimensionKind.NoveltyRisk, 1, "Novelty signal."),
                Optional(CognitiveMemoryScoreDimensionKind.OutcomeMismatch, 1, "Surprise or mismatch."),
                Optional(CognitiveMemoryScoreDimensionKind.RiskImpact, 1, "Risk impact."),
                Optional(CognitiveMemoryScoreDimensionKind.Usefulness, 1, "Usefulness."),
                Optional(CognitiveMemoryScoreDimensionKind.Reward, 0.7, "Reward signal."),
                Optional(CognitiveMemoryScoreDimensionKind.ReworkCost, 0.7, "Rework cost."),
                Optional(CognitiveMemoryScoreDimensionKind.ContradictionPressure, 1, "Contradiction pressure."),
                Optional(CognitiveMemoryScoreDimensionKind.UserInterest, 0.8, "User interest."),
                Optional(CognitiveMemoryScoreDimensionKind.StrategicAlignment, 0.8, "Strategic alignment."),
                Optional(CognitiveMemoryScoreDimensionKind.StalenessPressure, 0.8, "Staleness pressure."),
                Optional(CognitiveMemoryScoreDimensionKind.SourceWeakness, 1, "Source weakness."),
                Optional(CognitiveMemoryScoreDimensionKind.CalibrationRisk, 1, "Calibration risk."),
                Optional(CognitiveMemoryScoreDimensionKind.ContextSeparation, 1, "Context separation pressure."),
                Optional(CognitiveMemoryScoreDimensionKind.WrongScopePressure, 1, "Wrong-scope pressure.")
            ],
            missingDimensionPolicy: CognitiveMemoryScoreMissingDimensionPolicy.MarkUnavailable),
        Define(
            CognitiveMemoryScoreSpaceKind.PredictionErrorSeverity,
            CognitiveMemoryScoreScalarProjectionKind.DisplayOnly,
            [
                Required(CognitiveMemoryScoreDimensionKind.PredictionErrorMagnitude, 1.3, "Expected-vs-observed mismatch magnitude."),
                Optional(CognitiveMemoryScoreDimensionKind.RiskImpact, 1, "Risk impact."),
                Optional(CognitiveMemoryScoreDimensionKind.ReworkCost, 0.9, "Manual rework or workflow failure cost."),
                Optional(CognitiveMemoryScoreDimensionKind.ContextSeparation, 1, "Context-boundary pressure."),
                Optional(CognitiveMemoryScoreDimensionKind.WrongScopePressure, 1, "Wrong-scope recurrence pressure."),
                Optional(CognitiveMemoryScoreDimensionKind.SourceWeakness, 0.8, "Source weakness."),
                Optional(CognitiveMemoryScoreDimensionKind.CalibrationRisk, 1, "Calibration risk."),
                Optional(CognitiveMemoryScoreDimensionKind.StalenessPressure, 0.8, "Staleness pressure."),
                Optional(CognitiveMemoryScoreDimensionKind.ContradictionPressure, 0.8, "Contradiction pressure.")
            ]),
        Define(
            CognitiveMemoryScoreSpaceKind.ConsolidationCandidate,
            CognitiveMemoryScoreScalarProjectionKind.QueueOrdering,
            [
                Required(CognitiveMemoryScoreDimensionKind.SourceSufficiency, 1.2, "Required source/evidence backing for generated consolidation output."),
                Required(CognitiveMemoryScoreDimensionKind.EvidenceStrength, 1.1, "Evidence strength for draft mutation or review handoff."),
                Optional(CognitiveMemoryScoreDimensionKind.SourceQuality, 0.8, "Source trust and usability."),
                Optional(CognitiveMemoryScoreDimensionKind.RiskImpact, 1, "Risk of acting on generated candidate.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.RedactionPressure, 1, "Redaction/access pressure.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.ContradictionPressure, 1, "Contradiction pressure.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.StalenessPressure, 0.7, "Stale-source pressure.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.TemporalRecency, 0.5, "Recent source contribution."),
                Optional(CognitiveMemoryScoreDimensionKind.ProcedureMaturity, 0.6, "Procedure maturity for procedure-mining candidates.")
            ]),
        Define(
            CognitiveMemoryScoreSpaceKind.ReplayPriority,
            CognitiveMemoryScoreScalarProjectionKind.QueueOrdering,
            [
                Required(CognitiveMemoryScoreDimensionKind.PredictionErrorMagnitude, 1.2, "Prediction error magnitude."),
                Required(CognitiveMemoryScoreDimensionKind.RiskImpact, 1, "Risk."),
                Optional(CognitiveMemoryScoreDimensionKind.StalenessPressure, 0.8, "Staleness."),
                Optional(CognitiveMemoryScoreDimensionKind.Usefulness, 0.8, "Usefulness."),
                Optional(CognitiveMemoryScoreDimensionKind.FailureRecurrence, 1, "Failure recurrence."),
                Optional(CognitiveMemoryScoreDimensionKind.ProcedureMaturity, 0.7, "Procedure maturity.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.SourceQuality, 0.8, "Source trust change."),
                Optional(CognitiveMemoryScoreDimensionKind.StrategicAlignment, 0.7, "Strategic alignment."),
                Optional(CognitiveMemoryScoreDimensionKind.RegressionFailure, 1, "Regression failure."),
                Optional(CognitiveMemoryScoreDimensionKind.WrongScopePressure, 1, "Wrong-scope pressure."),
                Optional(CognitiveMemoryScoreDimensionKind.SourceWeakness, 0.8, "Source weakness."),
                Optional(CognitiveMemoryScoreDimensionKind.ContradictionPressure, 1, "Contradiction pressure.")
            ]),
        Define(
            CognitiveMemoryScoreSpaceKind.ProbeAssessment,
            CognitiveMemoryScoreScalarProjectionKind.DisplayOnly,
            [
                Required(CognitiveMemoryScoreDimensionKind.EvidenceStrength, 1.2, "Answer correctness evidence."),
                Required(CognitiveMemoryScoreDimensionKind.SourceSufficiency, 1.1, "Source sufficiency."),
                Optional(CognitiveMemoryScoreDimensionKind.WrongScopePressure, 1, "Wrong-scope pressure.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.CalibrationRisk, 1, "Calibration risk.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.MissingKnowledgePressure, 1, "Missing knowledge pressure.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.ContradictionPressure, 1, "Contradiction pressure.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.RedactionPressure, 0.8, "Redaction limit.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.RegressionValue, 0.8, "Regression value.")
            ]),
        Define(
            CognitiveMemoryScoreSpaceKind.AnswerGate,
            CognitiveMemoryScoreScalarProjectionKind.DisplayOnly,
            [
                Required(CognitiveMemoryScoreDimensionKind.SourceSufficiency, 1.2, "Source sufficiency."),
                Required(CognitiveMemoryScoreDimensionKind.ContextFit, 1.1, "Context fit."),
                Required(CognitiveMemoryScoreDimensionKind.EvidenceSupport, 1, "Belief support."),
                Optional(CognitiveMemoryScoreDimensionKind.ContradictionPressure, 1.2, "Contradiction risk.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.StalenessPressure, 0.8, "Staleness.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.RedactionPressure, 1, "Redaction pressure.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.CalibrationRisk, 1, "Calibration risk.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.RiskImpact, 1, "Risk impact.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.ProcedureMaturity, 0.8, "Procedure maturity."),
                Optional(CognitiveMemoryScoreDimensionKind.AccessPolicyRisk, 1, "Access policy risk.", higherIsBetter: false)
            ]),
        Define(
            CognitiveMemoryScoreSpaceKind.EpistemicNeed,
            CognitiveMemoryScoreScalarProjectionKind.QueueOrdering,
            [
                Required(CognitiveMemoryScoreDimensionKind.MissingKnowledgePressure, 1.2, "Knowledge gap pressure."),
                Required(CognitiveMemoryScoreDimensionKind.SourceWeakness, 1, "Source weakness."),
                Optional(CognitiveMemoryScoreDimensionKind.UserInterest, 0.8, "User interest."),
                Optional(CognitiveMemoryScoreDimensionKind.StrategicAlignment, 1, "Strategic alignment."),
                Optional(CognitiveMemoryScoreDimensionKind.ExpectedReuse, 0.8, "Expected reuse."),
                Optional(CognitiveMemoryScoreDimensionKind.ExpectedEffort, 0.6, "Expected effort.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.RiskImpact, 0.8, "Risk impact."),
                Optional(CognitiveMemoryScoreDimensionKind.ExpectedLearningValue, 1, "Expected learning value.")
            ]),
        Define(
            CognitiveMemoryScoreSpaceKind.CrossProjectPromotion,
            CognitiveMemoryScoreScalarProjectionKind.QueueOrdering,
            [
                Required(CognitiveMemoryScoreDimensionKind.SemanticSimilarity, 1, "Semantic similarity."),
                Required(CognitiveMemoryScoreDimensionKind.EntityEquivalence, 1.2, "Entity equivalence."),
                Required(CognitiveMemoryScoreDimensionKind.ContextSeparation, 1.2, "Context separation.", higherIsBetter: false),
                Required(CognitiveMemoryScoreDimensionKind.SourceReusePermission, 1.2, "Source reuse permission."),
                Required(CognitiveMemoryScoreDimensionKind.PolicyCompatibility, 1.2, "Policy compatibility."),
                Optional(CognitiveMemoryScoreDimensionKind.EvidenceStrength, 1, "Evidence strength."),
                Optional(CognitiveMemoryScoreDimensionKind.PrivacyRisk, 1.2, "Privacy risk.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.GlobalReuseValue, 0.8, "Global reuse value.")
            ]),
        Define(
            CognitiveMemoryScoreSpaceKind.ProcedureMaturity,
            CognitiveMemoryScoreScalarProjectionKind.DisplayOnly,
            [
                Required(CognitiveMemoryScoreDimensionKind.ProcedureMaturity, 1.2, "Procedure maturity."),
                Required(CognitiveMemoryScoreDimensionKind.EvidenceStrength, 1, "Procedure evidence strength."),
                Optional(CognitiveMemoryScoreDimensionKind.SourceReliability, 1, "Source reliability."),
                Optional(CognitiveMemoryScoreDimensionKind.FailureRecurrence, 0.8, "Failure recurrence.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.RiskImpact, 1, "Procedure risk.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.HumanValidation, 1, "Human validation.")
            ]),
        Define(
            CognitiveMemoryScoreSpaceKind.SimulationRisk,
            CognitiveMemoryScoreScalarProjectionKind.DisplayOnly,
            [
                Required(CognitiveMemoryScoreDimensionKind.RiskImpact, 1.2, "Simulation or analogy risk.", higherIsBetter: false),
                Required(CognitiveMemoryScoreDimensionKind.SourceSufficiency, 1.1, "Source and validation support for the simulation hypothesis."),
                Optional(CognitiveMemoryScoreDimensionKind.AccessPolicyRisk, 1.2, "Access policy risk.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.ContextSeparation, 1, "Cross-context or cross-project separation pressure.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.SourceReusePermission, 1, "Permission to reuse source evidence across scopes."),
                Optional(CognitiveMemoryScoreDimensionKind.PolicyCompatibility, 1, "Policy compatibility for analogy reuse."),
                Optional(CognitiveMemoryScoreDimensionKind.HumanValidation, 0.8, "Human validation of simulation prerequisites.")
            ]),
        Define(
            CognitiveMemoryScoreSpaceKind.MindMapSimilarity,
            CognitiveMemoryScoreScalarProjectionKind.UiSorting,
            [
                Required(CognitiveMemoryScoreDimensionKind.SemanticSimilarity, 1, "Semantic similarity."),
                Required(CognitiveMemoryScoreDimensionKind.GraphProximity, 1, "Graph proximity."),
                Optional(CognitiveMemoryScoreDimensionKind.SpatialProximity, 0.8, "Spatial proximity."),
                Optional(CognitiveMemoryScoreDimensionKind.MetadataFit, 0.7, "Metadata fit."),
                Optional(CognitiveMemoryScoreDimensionKind.TemporalRecency, 0.4, "Temporal recency."),
                Optional(CognitiveMemoryScoreDimensionKind.ContextSeparation, 1, "Context separation.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.SourceQuality, 0.7, "Source quality.")
            ]),
        Define(
            CognitiveMemoryScoreSpaceKind.MemoryActivation,
            CognitiveMemoryScoreScalarProjectionKind.DisplayOnly,
            [
                Required(CognitiveMemoryScoreDimensionKind.MemoryActivation, 1.2, "Current activation."),
                Optional(CognitiveMemoryScoreDimensionKind.TemporalRecency, 0.8, "Recent use."),
                Optional(CognitiveMemoryScoreDimensionKind.UserInterest, 0.7, "User interest."),
                Optional(CognitiveMemoryScoreDimensionKind.WorkspaceFocusFit, 0.8, "Workspace fit."),
                Optional(CognitiveMemoryScoreDimensionKind.StalenessPressure, 0.7, "Staleness pressure.", higherIsBetter: false)
            ]),
        Define(
            CognitiveMemoryScoreSpaceKind.SelfRegulationAssessment,
            CognitiveMemoryScoreScalarProjectionKind.DisplayOnly,
            [
                Required(CognitiveMemoryScoreDimensionKind.EvidenceStrength, 1, "Evidence strength."),
                Required(CognitiveMemoryScoreDimensionKind.EvidenceCoverage, 1, "Evidence coverage."),
                Required(CognitiveMemoryScoreDimensionKind.SourceReliability, 1, "Source reliability."),
                Required(CognitiveMemoryScoreDimensionKind.ContextFit, 1, "Context fit."),
                Optional(CognitiveMemoryScoreDimensionKind.ContradictionPressure, 1, "Contradiction pressure.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.NoveltyRisk, 0.8, "Novelty risk.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.ConsequenceRisk, 0.8, "Consequence risk.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.HistoricalCalibrationFit, 1, "Historical calibration fit."),
                Optional(CognitiveMemoryScoreDimensionKind.DomainCompetenceFit, 1, "Domain competence fit."),
                Optional(CognitiveMemoryScoreDimensionKind.KnownFailurePatternSimilarity, 1, "Known failure pattern similarity.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.ScopeAmbiguity, 1, "Scope ambiguity.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.AccessPolicyRisk, 1, "Access risk.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.RedactionPressure, 1, "Redaction pressure.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.CognitiveLoad, 0.7, "Cognitive load.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.ModelUncertainty, 1, "Model uncertainty.", higherIsBetter: false)
            ]),
        Define(
            CognitiveMemoryScoreSpaceKind.SelfModelCompetence,
            CognitiveMemoryScoreScalarProjectionKind.DisplayOnly,
            [
                Required(CognitiveMemoryScoreDimensionKind.SourceSufficiency, 1, "Source coverage."),
                Required(CognitiveMemoryScoreDimensionKind.RegressionFailure, 1, "Regression success signal.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.HumanReviewAgreement, 1, "Human review agreement."),
                Optional(CognitiveMemoryScoreDimensionKind.UserCorrectionPressure, 1, "Correction pressure.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.ConfidenceBias, 1, "Confidence bias.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.SelfModelStability, 1, "Profile stability.")
            ]),
        Define(
            CognitiveMemoryScoreSpaceKind.CalibrationHealth,
            CognitiveMemoryScoreScalarProjectionKind.DisplayOnly,
            [
                Required(CognitiveMemoryScoreDimensionKind.OverconfidenceRate, 1, "Overconfidence rate.", higherIsBetter: false),
                Required(CognitiveMemoryScoreDimensionKind.UnderconfidenceRate, 1, "Underconfidence rate.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.CalibrationRisk, 1, "Calibration error.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.AbstentionQuality, 0.8, "Abstention quality."),
                Optional(CognitiveMemoryScoreDimensionKind.WrongScopeRecurrence, 1, "Wrong-scope recurrence.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.SourceInsufficientRecurrence, 1, "Source-insufficient recurrence.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.CalibrationDrift, 1, "Calibration drift.", higherIsBetter: false)
            ]),
        Define(
            CognitiveMemoryScoreSpaceKind.ProfessorReviewRouting,
            CognitiveMemoryScoreScalarProjectionKind.QueueOrdering,
            [
                Required(CognitiveMemoryScoreDimensionKind.ProfessorReviewValue, 1.2, "Expected professor review value."),
                Required(CognitiveMemoryScoreDimensionKind.ConsequenceRisk, 1, "Consequence risk."),
                Optional(CognitiveMemoryScoreDimensionKind.NoveltyRisk, 0.8, "Novelty."),
                Optional(CognitiveMemoryScoreDimensionKind.DomainCompetenceFit, 1, "Domain competence.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.ContradictionPressure, 0.8, "Contradiction pressure."),
                Optional(CognitiveMemoryScoreDimensionKind.SourceSufficiency, 1, "Source sufficiency.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.EscalationCost, 0.7, "Escalation cost.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.AccessPolicyRisk, 1, "Access risk.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.RedactionPressure, 1, "Redaction pressure.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.ExpectedLearningValue, 0.8, "Expected learning value.")
            ]),
        Define(
            CognitiveMemoryScoreSpaceKind.AnswerPosture,
            CognitiveMemoryScoreScalarProjectionKind.DisplayOnly,
            [
                Required(CognitiveMemoryScoreDimensionKind.SourceSufficiency, 1.2, "Source sufficiency."),
                Required(CognitiveMemoryScoreDimensionKind.ContextFit, 1.1, "Context fit."),
                Required(CognitiveMemoryScoreDimensionKind.HistoricalCalibrationFit, 1, "Calibration fit."),
                Optional(CognitiveMemoryScoreDimensionKind.DomainCompetenceFit, 1, "Domain competence."),
                Optional(CognitiveMemoryScoreDimensionKind.ContradictionPressure, 1, "Contradiction pressure.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.HumilityTriggerPressure, 1, "Humility trigger pressure.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.ConfidenceReinforcementPressure, 0.6, "Confidence reinforcement."),
                Optional(CognitiveMemoryScoreDimensionKind.ProfessorReviewValue, 0.8, "Professor review value.", higherIsBetter: false),
                Optional(CognitiveMemoryScoreDimensionKind.AbstentionCost, 0.6, "Abstention cost.")
            ])
    ];

    private static readonly IReadOnlyDictionary<(CognitiveMemoryScoreSpaceKind Kind, CognitiveMemoryScoreSchemaVersion SchemaVersion), CognitiveMemoryScoreSpaceDefinition> DefinitionsByKey =
        InitialDefinitions.ToDictionary(
            definition => (definition.Kind, definition.SchemaVersion),
            definition => definition);

    public ValueTask<CognitiveMemoryScoreSpaceDefinition> GetDefinitionAsync(
        CognitiveMemoryScoreSpaceKind kind,
        CognitiveMemoryScoreSchemaVersion schemaVersion,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (DefinitionsByKey.TryGetValue((kind, schemaVersion), out var definition))
        {
            return ValueTask.FromResult(definition);
        }

        throw new KeyNotFoundException($"No Cognitive Memory score space definition exists for '{kind}' schema '{schemaVersion}'.");
    }

    private static CognitiveMemoryScoreSpaceDefinition Define(
        CognitiveMemoryScoreSpaceKind kind,
        CognitiveMemoryScoreScalarProjectionKind scalarProjectionKind,
        IReadOnlyList<CognitiveMemoryScoreDimensionDefinition> dimensions,
        CognitiveMemoryScoreMissingDimensionPolicy missingDimensionPolicy = CognitiveMemoryScoreMissingDimensionPolicy.RejectEvaluation)
        => new(
            kind,
            CurrentSchemaVersion,
            CurrentNormalizationProfile,
            missingDimensionPolicy,
            scalarProjectionKind,
            dimensions,
            CurrentAlgorithmVersion);

    private static CognitiveMemoryScoreDimensionDefinition Required(
        CognitiveMemoryScoreDimensionKind kind,
        double defaultWeight,
        string description,
        bool higherIsBetter = true)
        => Dimension(kind, required: true, defaultWeight, description, higherIsBetter);

    private static CognitiveMemoryScoreDimensionDefinition Optional(
        CognitiveMemoryScoreDimensionKind kind,
        double defaultWeight,
        string description,
        bool higherIsBetter = true)
        => Dimension(kind, required: false, defaultWeight, description, higherIsBetter);

    private static CognitiveMemoryScoreDimensionDefinition Dimension(
        CognitiveMemoryScoreDimensionKind kind,
        bool required,
        double defaultWeight,
        string description,
        bool higherIsBetter)
        => new(
            kind,
            minimum: 0,
            maximum: 1,
            higherIsBetter,
            required,
            defaultWeight,
            description);
}
