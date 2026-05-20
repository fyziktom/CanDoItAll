# Curator Professor Learning Model

## Analogy

A student discusses a topic with a professor. The student may have learned something but has it partially confused. The professor clarifies the topic. The student temporarily remembers the professor's exact correction and uses it to compare against existing knowledge. Over time, once the corrected knowledge is applied across many related memories and reasoning episodes, the student no longer needs the exact quote as an active crutch, but the system can still trace where the lesson came from if asked.

## Data Model Concept

Introduce or adapt records for professor anchors with these conceptual fields:

- `ProfessorAnchorId`
- `ProjectId`
- `CuratorSessionId`
- `CuratorTurnId`
- `SourceItemId`
- `EvidenceAnchorId`
- `AssertionText`
- `NormalizedClaimText`
- `TargetMemoryRecordIds`
- `TargetClaimIds`
- `ScopeKey`
- `CaptureKind`
- `TargetConfidence`
- `AssimilationState`: `ActiveAnchor`, `Comparing`, `Integrated`, `Assimilated`, `Faded`, `Rejected`
- `AssimilationScore`
- `DerivedMemoryRecordIds`
- `DerivedAggregateCandidateIds`
- `LastComparedAtUtc`
- `FadeEligibleAtUtc`

The implementation may choose names that fit existing entity conventions, but it must preserve these semantics.

## Curator Capture Contract

A curator turn should not always become one memory record. It may produce:

- New knowledge assertion.
- Correction of a specific memory/claim.
- Scope correction for a specific memory/claim.
- Conflict note requiring review.
- Clarification question when target is ambiguous.

## Targeting Rules

- Explicit UI/API targets win.
- If exactly one high-confidence memory/claim target is inferred, the correction may proceed automatically.
- If multiple possible targets exist, create a pending professor anchor plus review/clarification item; do not supersede all candidates.
- If the message is general new knowledge, do not mark existing memories stale.
- If the message is a scope correction, refine scope without destroying unrelated source records.

## Assimilation Rules

A professor anchor can fade only when:

- At least one stable derived memory or aggregate captures the normalized lesson.
- The derived memory has claim-level provenance back to the professor anchor and at least one additional independent source or successful repeated usage event, unless the professor anchor is explicitly configured as permanently authoritative.
- No active contradictions remain unresolved.
- Recent recall/dream validation shows the corrected knowledge is retrieved without relying solely on the raw professor turn.

Fading does not mean deleting. It means lowering active retrieval weight while preserving provenance.
