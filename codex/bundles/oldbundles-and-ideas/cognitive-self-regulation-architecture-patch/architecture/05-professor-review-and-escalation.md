# Professor Review And Escalation Architecture

## Purpose

Formalize the use of larger LLMs as professor-like reviewers, challengers, and auditors.

A large LLM can broaden perspective and catch weak reasoning, but it is not source truth and cannot bypass memory governance.

## Professor Review Modes

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

Professor Review should be considered when domain competence is weak, topic is novel and high-impact, contradiction pressure is high, calibration health is poor for the feature pattern, answer requires complex architecture synthesis, local model and evidence disagree, repeated probing failures occur, high-risk workflow/tool action is proposed, user explicitly asks for a professor-like review, or a major architecture decision is about to be promoted.

## Professor Review Output

A review output should include summary, critique, missing evidence, risks, alternative hypotheses, confidence assessment, suggested probes, suggested regression tests, suggested source audits, recommended answer posture, evidence refs, and trace refs.

## Governance Rules

- Professor review output is evidence/challenge/review input, not canonical truth.
- It can propose claim operations but must use mutation authority and review policy.
- It can generate probing questions and regression candidates.
- It can update calibration evidence if outcome is later validated.
- It must identify whether it used only supplied context or external knowledge.
- It must not inspect redacted content unless access policy allows it.

## Required Trace

Every professor review must record model profile, input context ids, source access level, prompt/profile version, review mode, output hash, review result id, resulting actions, and whether human review is required.
