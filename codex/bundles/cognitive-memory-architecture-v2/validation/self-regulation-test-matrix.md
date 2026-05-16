# Self-Regulation Test Matrix

| Scenario | Expected state | Expected posture | Required evidence |
|---|---|---|---|
| Stable project decision with multiple source anchors | `Calibrated` | `DirectConfident` | Claim/evidence, belief, assessment, and calibration trace. |
| Useful but source-light low-risk answer | `Exploratory` | `DirectWithCaveats` or `Hypothesis` | Source sufficiency warning and posture trace. |
| High-risk unvalidated procedure | `HighRiskUnverified` | `ReviewRequired` or `Abstain` | Procedure maturity, risk, and answer-gate trace. |
| Production/test context ambiguity | `Fragmented` | `ClarifyingQuestion` | Inhibited candidates and context-boundary trace. |
| Generated summary primary support | `SourcePoor` | `SourceAuditRequest` or `Hypothesis` | Source audit humility trigger. |
| Recent wrong-scope correction | `Overconfident` risk | `ClarifyingQuestion` or `ProbeQuestion` | Failure pattern and calibration event. |
| Weak domain with high-impact novelty | `ProfessorReviewNeeded` | `ProfessorReviewRequired` | Competence profile and escalation trace. |
| Repeated low-confidence confirmed probes | `Underconfident` | `DirectWithCaveats` or scoped `DirectConfident` | Calibration aggregate and reinforcement record. |
| Redacted evidence prevents proof | `SourcePoor` | `DirectWithCaveats` or `Abstain` | Redaction pressure trace. |
| Contradicted claim selected | `Defensive` or `Fragmented` | `ReviewRequired` or `DirectWithCaveats` | Belief state and attack evidence. |

## Negative Cases

- Prompt persona cannot replace `CognitiveSelfModelRecord`.
- Professor review cannot directly mutate canonical truth.
- Display confidence cannot drive answer posture without score trace.
- User praise cannot create competence evidence.
- Salience/reward cannot increase belief without evidence.
- A single correction cannot silently retune calibration profile thresholds.
- Answer gate cannot become looser than self-regulation without a new trace.
