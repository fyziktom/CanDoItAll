# 30 Professor Review And Escalation

## Purpose

Formalize large-model or expert professor review as challenge, critique, contradiction hunt, architecture review, calibration review, and learning expansion.

Professor review can broaden perspective and catch weak reasoning. It is not source truth and cannot bypass memory governance.

## Review Modes

| Mode | Purpose |
|---|---|
| `SocraticChallenge` | Ask hard questions and expose missing assumptions. |
| `ContradictionHunt` | Search for conflicts in claims, sources, and scope. |
| `ArchitectureReview` | Review design coherence, boundaries, dependencies, and risks. |
| `CalibrationReview` | Judge whether confidence/posture matches evidence. |
| `SourceSufficiencyReview` | Identify missing or weak evidence anchors. |
| `AlternativeHypothesisReview` | Propose alternative interpretations or designs. |
| `FailureModeReview` | Identify likely system/process failure modes. |
| `LearningExpansion` | Suggest what should be studied next and why. |

## Escalation Triggers

Professor review should be considered when:

- domain competence is weak,
- topic is novel and high-impact,
- contradiction pressure is high,
- calibration health is poor for the feature pattern,
- answer requires complex architecture synthesis,
- local model and source evidence disagree,
- repeated probing failures occur,
- high-risk workflow/tool action is proposed,
- user explicitly asks for professor-like review,
- a major architecture decision is about to be promoted.

## Review Request

A review request records:

- project id,
- workspace frame id,
- self-regulation assessment id,
- review mode,
- review question,
- input claim ids,
- input evidence anchor ids,
- input memory item ids,
- access context,
- model profile,
- prompt/profile version,
- policy options.

## Review Output

A review output should include:

- summary,
- critique,
- missing evidence,
- risks,
- alternative hypotheses,
- confidence/posture assessment,
- suggested probes,
- suggested regression tests,
- suggested source audits,
- recommended answer posture,
- evidence refs,
- trace refs,
- model profile,
- output hash,
- human-review requirement.

## Governance Rules

- Professor review output is evidence/challenge/review input, not canonical truth.
- It can propose claim operations only through mutation authority and review policy.
- It can generate probing questions and regression candidates.
- It can update calibration evidence only after an outcome is validated.
- It must identify whether it used only supplied context or external knowledge.
- It must not inspect redacted content unless access policy allows it.
- It must not overrule a stricter access, redaction, source, or safety policy.

## Relationship To Answer Gate

Professor review can recommend a posture, but the metamemory answer gate enforces final answer-time policy. If professor output raises risk, source weakness, contradiction, or uncertainty, the answer gate must become stricter unless a new trace proves the concern is resolved.
