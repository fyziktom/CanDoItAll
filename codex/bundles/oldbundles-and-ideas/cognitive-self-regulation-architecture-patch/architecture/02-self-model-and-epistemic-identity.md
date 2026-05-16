# Self-Model And Epistemic Identity

## Purpose

Define a durable, scoped, evidence-backed self-model for Cognitive Memory.

A self-model is not a prompt persona. It is a structured record of operational identity, domain competence, known limits, principles, and failure patterns.

## Scope

Self-model records should be scoped by project, user, agent type, model provider/model profile, task type, domain or knowledge region, and process/workflow role.

The system may have different competence and calibration profiles for different tasks. For example, it may be strong in CanDoItAll architecture but weak in current legal details unless fresh sources are retrieved.

## Core Records

### `CognitiveSelfModelRecord`

Stores the stable operating model for an agent/system in a project scope.

Required fields:

- id,
- project id,
- model profile id,
- role key,
- purpose,
- operating principles,
- allowed task categories,
- restricted task categories,
- strong domain ids,
- weak domain ids,
- known failure pattern ids,
- default self-regulation policy id,
- created/updated timestamps,
- algorithm/profile version.

### `DomainCompetenceProfileRecord`

Stores evidence-backed competence per domain/task type.

Dimensions:

- domain/knowledge region,
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

Stores recurring failure patterns.

Examples:

- wrong-scope answer from semantically similar memory,
- generated summary treated as truth,
- stale procedure recalled after source change,
- high confidence with weak source coverage,
- contradiction hidden by fluent answer,
- context boundary merge between production/test/local/CI.

Each pattern should include trigger conditions, score shape or matching rule, observed examples, mitigation steps, required answer posture change, related regression tests, related probe findings, and last observed timestamp.

### `SelfRegulationPolicyProfile`

Defines how self-regulation behaves for a mode:

- normal assistance,
- strict safety,
- workflow automation,
- probing exam,
- exploratory research,
- creative ideation,
- professor review,
- high-risk procedure.

Policy fields include source sufficiency requirements, allowed answer postures, professor review triggers, human review triggers, probing triggers, abstention thresholds, clarification thresholds, calibration update policy, and confidence reinforcement policy.

## Self-Model Is Evidence-Backed

Self-model updates must come from evidence:

- probe confirmations/corrections,
- regression test results,
- workflow/test outcomes,
- human review decisions,
- source audit outcomes,
- professor review outcomes,
- repeated prediction errors,
- accepted project decisions.

Do not update self-model from tone, praise, or single unverified chat turns.

## Required Guardrails

- A self-model cannot mark an unsupported claim as true.
- A self-model cannot grant access to redacted content.
- A self-model cannot bypass source anchors.
- A self-model cannot silently suppress known failure patterns.
- A self-model update must be versioned and auditable.
