# 28 Self-Model And Epistemic Identity

## Purpose

Define a durable, scoped, evidence-backed self-model for Cognitive Memory.

A self-model is not a prompt persona. It is structured control data describing operational role, competence, limits, known failure patterns, and policy.

## Scope

Self-model records are scoped by:

- project,
- user or agent role,
- model provider/profile,
- task type,
- domain or knowledge region,
- process/workflow role,
- risk category where needed.

The system may be well-calibrated for CanDoItAll architecture and poorly calibrated for current legal, medical, or external pricing facts unless fresh sources are retrieved.

## Core Records

### `CognitiveSelfModelRecord`

Stores stable operating identity for a role/model/project scope:

- id,
- project id,
- model profile key,
- role key,
- purpose,
- operating principles,
- allowed task categories,
- restricted task categories,
- strong domain profile ids,
- weak domain profile ids,
- known failure pattern ids,
- default self-regulation policy id,
- algorithm/profile version,
- created/updated timestamps.

### `DomainCompetenceProfileRecord`

Stores evidence-backed competence for a domain/task/profile:

- domain or knowledge region,
- task type,
- source coverage,
- probe success rate,
- regression success rate,
- user correction rate,
- human review approval rate,
- workflow/test success rate,
- historical calibration score,
- recent drift,
- scope limitations,
- minimum required evidence posture.

### `KnownFailurePatternRecord`

Stores recurring failure patterns with score shapes and mitigation:

- wrong-scope answer from semantically similar memory,
- generated summary treated as truth,
- stale procedure recalled after source change,
- high confidence with weak source coverage,
- contradiction hidden by fluent answer,
- production/test/local/CI context merge,
- redaction-limited proof rendered as complete proof.

Each pattern includes trigger conditions, score shape, observed examples, mitigation steps, required posture floor, related regression tests, related probe findings, review requirement, and last observed timestamp.

### `SelfRegulationPolicyProfileRecord`

Defines behavior by mode:

- normal assistance,
- strict safety,
- workflow automation,
- probing exam,
- exploratory research,
- creative ideation,
- professor review,
- high-risk procedure.

Policy fields include source sufficiency requirements, allowed answer postures, professor review triggers, human review triggers, probing triggers, abstention thresholds, clarification thresholds, calibration update policy, and confidence reinforcement policy.

## Evidence-Backed Updates

Self-model updates can come from:

- probe confirmations and corrections,
- regression test results,
- workflow/test outcomes,
- human review decisions,
- source audit outcomes,
- professor review outcomes after validation,
- repeated prediction errors,
- accepted project decisions.

Self-model updates must not come from tone, praise, single unverified chat turns, or unsupported generated summaries.

## Confidence Reinforcement

Confidence may become stronger only for the scoped feature pattern where evidence supports it. Reinforcement can reduce calibration risk or permit a stronger answer posture, but it cannot erase source requirements, contradiction dimensions, access policy, or review gates.

## Guardrails

- A self-model cannot mark an unsupported claim as true.
- A self-model cannot grant access to redacted content.
- A self-model cannot bypass source anchors.
- A self-model cannot suppress a known failure pattern without reviewable evidence.
- A self-model update must be versioned and auditable.
- Old traces must remain tied to the profile version that produced them.
