# Score Geometry Patch For Self-Regulation

This file is not intended to compile as-is. It describes required additions to `CognitiveMemory.ScoringContracts.cs` in the main bundle.

## Add To `ScoreSpaceKind`

```csharp
SelfRegulationAssessment = 13,
SelfModelCompetence = 14,
CalibrationHealth = 15,
ProfessorReviewRouting = 16,
AnswerPosture = 17
```

Use the next available numeric values if the enum has changed.

## Add To `ScoreDimensionKind`

```csharp
EvidenceStrength,
EvidenceCoverage,
SourceReliability,
RecencyFit,
NoveltyRisk,
ConsequenceRisk,
ModelUncertainty,
HistoricalCalibrationFit,
DomainCompetenceFit,
KnownFailurePatternSimilarity,
ScopeAmbiguity,
UserCorrectionPressure,
SelfModelStability,
ProfessorReviewValue,
EscalationCost,
AbstentionCost,
ConfidenceBias,
OverconfidenceRate,
UnderconfidenceRate,
HumanReviewAgreement,
ProfessorReviewAgreement,
HumilityTriggerPressure,
ConfidenceReinforcementPressure
```

Use explicit numeric values when patching the actual enum.

## Add To `ScoreEvidenceKind`

```csharp
SelfModel = 17,
DomainCompetenceProfile = 18,
KnownFailurePattern = 19,
CalibrationAggregate = 20,
SelfRegulationAssessment = 21,
AnswerPostureDecision = 22,
ProfessorReview = 23,
HumilityTrigger = 24,
ConfidenceReinforcement = 25
```

Use the next available numeric values if the enum has changed.

## Required Contract Tests

- Self-Regulation Assessment rejects scalar-only decisions.
- Answer Posture selection stores score vector, matched shapes, missing dimensions, and evidence refs.
- Professor Review routing stores model profile, review mode, access context, and score trace.
- Calibration Health aggregates cannot reinterpret old traces when profile version changes.
