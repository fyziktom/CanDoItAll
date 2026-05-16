# Self-Regulation Loop And Answer Postures

## Purpose

Define how Self-Regulation selects behavior before recall, before answer rendering, and after feedback.

## Self-Regulation Assessment

Every important answer, tool, workflow, or memory mutation decision should be preceded by a `SelfRegulationAssessment` containing:

- workspace frame id,
- self-model id,
- domain competence profile ids,
- matched known failure patterns,
- score evaluation trace,
- self-regulation state,
- humility triggers fired,
- confidence reinforcement factors,
- required operations,
- selected answer posture,
- escalation decision,
- warnings,
- audit metadata.

## Assessment Dimensions

The Self-Regulation score space should include dimensions such as evidence strength, evidence coverage, source reliability, context fit, recency fit, contradiction pressure, novelty risk, consequence risk, model uncertainty, historical calibration fit, domain competence fit, known failure pattern similarity, scope ambiguity, source availability, procedure maturity, access/redaction pressure, user correction pressure, and cognitive load.

## Humility Triggers

Humility triggers downgrade the allowed answer posture or force additional operations.

| Trigger | Typical Outcome |
|---|---|
| Source-poor high-risk answer | Source audit, review, or abstain. |
| High contradiction pressure | Compare claims, request review, or warn. |
| Wrong-scope failure pattern matched | Clarify scope and inhibit related-wrong candidates. |
| Similar recent correction | Lower confidence and create probe/regression candidate. |
| Generated summary is the main support | Caveat, source audit, or abstain. |
| Domain outside competence profile | Professor review or external learning proposal. |
| High-impact unvalidated procedure | Human review or abstention. |
| Redaction prevents proof | Warn, limit answer, or abstain. |
| Stale source for volatile topic | Source audit before final answer. |

## Confidence Reinforcement

Confidence may increase only through reviewable evidence:

- repeated probe confirmations,
- repeated regression pass,
- human review approval,
- workflow/test success,
- multiple independent source anchors,
- stable project decision records,
- absence of contradictions after defined observation window.

Reinforcement must not erase uncertainty dimensions. It can reduce calibration risk or raise allowed answer posture for the relevant feature pattern.

## Answer Posture Decision

Use first-class answer postures instead of only confidence labels.

| Posture | Meaning |
|---|---|
| `DirectConfident` | Evidence, context, calibration, and risk support a direct answer. |
| `DirectWithCaveats` | Useful answer with explicit limitations. |
| `PreliminaryReaction` | Fast low-detail response from active workspace; must be labeled preliminary. |
| `Hypothesis` | Plausible but not source-backed enough for belief. |
| `ClarifyingQuestion` | Scope/intent/context is too ambiguous. |
| `SourceAuditRequest` | Evidence must be checked before answer can be trusted. |
| `ProbeQuestion` | Interactive probing is the cheapest way to resolve uncertainty. |
| `ReviewRequired` | Human or expert review is required. |
| `ProfessorReviewRequired` | Large LLM/challenger review is required before final synthesis. |
| `Abstain` | Answer would be misleading, unsafe, unsourced, or policy-blocked. |

## Relationship To Metamemory Answer Gate

Self-Regulation chooses the posture and required operations. Metamemory Answer Gate enforces them at answer time.

The answer gate may become stricter than Self-Regulation if last-minute answer synthesis introduces unsupported claims, contradiction, redaction, or source insufficiency. It must never become looser than Self-Regulation without a new evaluation trace.

## Post-Outcome Recovery

After an answer/tool/workflow outcome, Self-Regulation must compare predicted confidence with outcome, detect overconfidence/underconfidence, update calibration ledger, publish prediction errors and salience signals, propose known failure pattern updates, create regression tests, create probe questions, request replay or review, and update self-model only through reviewable evidence.
