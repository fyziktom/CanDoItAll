# Security, Governance, and Provenance Architecture

## Design Goal

The Cognitive Memory module must make agents more capable without making the system less trustworthy. The module will store derived summaries, relations, and vector projections, but it must always preserve a clear chain back to raw sources.

The core security principle is:

```text
No derived memory item is authoritative unless it can be traced to source evidence.
```

## Source of Truth Hierarchy

| Layer | Authority | Mutable | Purpose |
|---|---:|---:|---|
| Raw source snapshot/reference | Highest | No, except deletion policy | Preserve what existed. |
| Source manifest item | High | Versioned | Track source identity, hash, connector, scope. |
| Canonical source item | Medium-high | Versioned | Normalize source content into typed facts. |
| Memory item | Medium | Versioned/superseded | Store semantic, episodic, procedural, decision, or reflection meaning. |
| Memory graph relation | Medium | Versioned | Store explicit associations and reasoning evidence. |
| Epistemic Drive records | Medium | Versioned | Store coverage, gaps, proposals, and learning outcomes with evidence refs. |
| Qdrant projection | Low | Rebuildable | Retrieval acceleration only. |
| Recall context pack | Low | Ephemeral/versioned | Working context for a task/agent. |

## Provenance Requirements

Every memory item MUST contain:

- `MemoryItemId`
- `ProjectId` or `WorkspaceId`
- `MemoryType`
- `Title`
- `CanonicalText`
- `SourceRefs[]`
- `CreatedBy`
- `CreatedAtUtc`
- `AlgorithmVersion`
- `Confidence`
- `ValidationState`
- `ContentHash`

Every source reference MUST contain:

- source system name,
- source item id,
- source content hash,
- source timestamp or version,
- locator for raw retrieval,
- connector/plugin identity,
- access classification.

## Trust States

```text
Draft -> MachineGenerated -> HumanReviewed -> Approved -> Superseded -> Retired
                         \-> Rejected
```

Recommended enum:

```csharp
public enum MemoryValidationState
{
    Draft = 0,
    MachineGenerated = 1,
    NeedsHumanReview = 2,
    HumanReviewed = 3,
    Approved = 4,
    Superseded = 5,
    Retired = 6,
    Rejected = 7
}
```

## Source Trust Classification

Learning workflows must classify every proposed source before use.

Recommended trust levels:

- `LocalProjectSource`: project docs, repository files, uploaded files, source snapshots.
- `InternalApprovedSource`: internal knowledge bases or approved team material.
- `OfficialVendorDocumentation`: official product/vendor docs.
- `CommunitySource`: community posts, examples, issues, blogs, or forums.
- `UntrustedSource`: unknown, low quality, prompt-injection-risk, or policy-blocked sources.

Community and untrusted sources can help form questions, but they should not become canonical truth without stronger corroboration and review.

## High-Risk Memory Categories

These memory categories should require stronger provenance and optional human validation:

- security decisions,
- production deployment procedures,
- secrets and credential handling,
- financial decisions,
- customer commitments,
- legal/compliance notes,
- destructive automation procedures,
- migration plans,
- code-generation instructions that affect production systems.
- learning-derived updates to any of the above categories.

## Learning Approval Policy

Epistemic Drive may create draft learning proposals during consolidation. It must not execute external source study, create high-impact active procedures, or promote learning-derived canonical records without required approval.

Approval policy should be explicit per scope:

- local-only source review may be allowed in observe/draft mode,
- external internet reading requires approval when policy requires it,
- high-risk procedures always require human validation,
- cross-project promotion requires project/source sharing approval,
- generated learning outcomes remain draft until QA and review complete.

Learning proposal decisions must be audited with user/agent id, scope, source list, approved depth, timestamp, and reason.

## Secret Handling

The memory system must never embed secrets into Qdrant or derived summaries.

Required controls:

1. Source ingestion must classify secret-like content before canonicalization.
2. Secret values are replaced by stable references, not text values.
3. Secret references can point to the existing vault/secrets layer.
4. Vector projection skips or redacts records marked `ContainsSecret`.
5. Context packs must be built with policy-aware redaction.

Recommended metadata:

```json
{
  "containsSecret": false,
  "redactionProfile": "standard",
  "allowedAgentRoles": ["architect", "qa"],
  "blockedMemoryTools": ["external_llm_context"]
}
```

## Access Control

Memory access should be checked at three levels:

1. **Source access**: can the current user/agent access the raw source?
2. **Memory item access**: can the current user/agent access the derived memory?
3. **Context-pack access**: can this item be passed into this particular model/tool?

The recall orchestrator must accept a `MemoryAccessContext` containing:

- user id,
- agent id,
- roles,
- project id,
- process/workflow run id,
- requested operation,
- model/provider policy,
- data export policy.

## Audit Events

The following events should be recorded:

- source ingested,
- source skipped,
- source redacted,
- canonical item created,
- memory item created,
- memory item superseded,
- relation created,
- relation rejected,
- Qdrant projection upserted,
- recall requested,
- recall result selected,
- recall result ignored,
- memory injected into agent context,
- human review decision,
- distributed worker output accepted/rejected.
- knowledge gap detected,
- knowledge coverage map refreshed,
- epistemic tension evaluated,
- learning proposal created/updated,
- learning proposal approved/rejected/snoozed/scoped,
- probing requested from learning proposal,
- learning task planned/started/completed/failed,
- learning outcome accepted/rejected,
- projection refreshed after learning outcome.

## Governance Rules

### Rule 1: Qdrant Is Rebuildable

Do not store facts only in Qdrant. Every point must be reconstructable from DB/storage state.

### Rule 2: Summaries Must Not Hide Contradictions

If two source records conflict, the canonical layer should preserve the conflict rather than silently choosing one.

### Rule 3: Generated Procedures Need Evidence

A `ProcedureMemoryItem` must include evidence from either:

- successful workflow/process episodes,
- explicit user-authored procedure,
- approved architecture bundle,
- validated test evidence.

### Rule 4: Human Review Is a First-Class Workflow

When the system finds ambiguous merges, contradictions, or high-risk new procedures, it should create review tasks instead of forcing automatic truth.

### Rule 5: Model Versioning Is Mandatory

All embedding, summarization, classification, clustering, and relation detection outputs must store model/provider/algorithm versions.

### Rule 6: Epistemic Drive Is Evidence-Driven

Knowledge need decisions must preserve vector components, evidence refs, project direction intersections, ROI assumptions, and explanation text. A scalar display score cannot be the authoritative decision model.

### Rule 7: Learning Output Is Draft Until Validated

Generated learning output can create draft canonical records, procedures, runbooks, and probing questions. It cannot silently replace human-validated records or become active high-risk guidance without review.

### Rule 8: External Study Is Policy-Gated

Any learning task that reads external sources must use approved source scope and source trust classification. If policy denies external access, Epistemic Drive can propose local sources, ask the user for sources, or request probing instead.

### Rule 9: Anti-Hallucination Requirements

Learning-derived canonical records must include source refs. Summaries must state uncertainty and unresolved contradictions. Contradictory or stale source evidence must be preserved, not hidden by generated synthesis.

## Threat Model

| Threat | Example | Mitigation |
|---|---|---|
| Poisoned source | Malicious email instructs agent to ignore policy | Source type trust score, prompt-injection scanner, restricted context pack. |
| Incorrect merge | Test Docker merged into production deployment | Relation type `semantically_related_but_contextually_separated`, human review for context conflicts. |
| Secret leakage | OAuth secret embedded into vector DB | redaction before embedding; skip `ContainsSecret`. |
| Stale knowledge | Old deployment procedure retrieved as current | supersession, staleness penalty, validity intervals. |
| Circular hallucination | Agent summary becomes source for future summaries | strict provenance levels; generated summaries cannot become raw truth. |
| Worker tampering | Distributed device returns bad cluster output | job input hash, output hash, worker identity, deterministic validation, coordinator acceptance. |
| Unapproved autonomous learning | Agent studies external source and updates memory without approval | learning approval policy, source trust classification, proposal lifecycle, audit gates. |
| Scalar-only knowledge desire | Important dimensions hidden behind one score | durable vector fields, evidence refs, category/Pareto/ROI metadata, validation test. |

## Recommended EF Tables

- `MemorySourceManifestRecords`
- `MemorySourceItemRecords`
- `CanonicalMemoryRecords`
- `MemoryItemRecords`
- `MemoryRelationRecords`
- `MemoryProjectionRecords`
- `MemoryRecallTraceRecords`
- `MemoryConsolidationRunRecords`
- `KnowledgeRegionRecords`
- `ProjectDirectionVectorRecords`
- `KnowledgeCoverageMapRecords`
- `KnowledgeGapRecords`
- `KnowledgeNeedVectorRecords`
- `EpistemicTensionRecords`
- `LearningProposalRecords`
- `LearningTaskRecords`
- `LearningOutcomeRecords`
- `OpenQuestionSetRecords`
- `ProbingQuestionSetRecords`
- `CognitiveSelfModelRecords`
- `SelfRegulationAssessmentRecords`
- `AnswerPostureDecisionRecords`
- `CalibrationAggregateRecords`
- `ProfessorReviewRecords`
- `MemoryHumanReviewItems`
- `MemoryAccessAuditRecords`

## Data Retention

Raw sources should follow existing project/storage retention policy. Derived projections can be compacted more aggressively:

- recall traces: retain summaries indefinitely, detailed context packs by policy,
- Qdrant projections: rebuild anytime,
- rejected relations: retain for learning and audit,
- distributed job outputs: retain hashes + summary, not necessarily full payload.

## Probing Governance Rules

Interactive probing introduces new sensitive surfaces because a user may paste secrets, internal plans, or corrections that imply confidential source material.

Rules:

1. Probe transcripts are project-scoped sensitive artifacts by default.
2. Probe answers must obey the same access policy as recall context packs.
3. Secret-like content must be redacted before embedding, external model calls, or cross-project evidence summaries.
4. User corrections are conversational evidence, not authoritative raw source truth.
5. High-risk corrections require human review before active memory changes.
6. Regression tests created from restricted probes must not leak restricted text in globally shared fixtures.
7. Cross-project probing may share only approved summaries and reusable test constraints.

## Neuro-Cognitive Governance Rules

### Rule 10: Evidence Anchors Are Required For Belief Changes

Atomic claims, belief state changes, procedure maturity changes, and projection invalidations must cite evidence anchors or an explicit generated/draft reason. Source refs that only identify a broad source item are not enough for high-risk or contested claims; use structured paths, spans, quote hashes, source hashes, trust level, and redaction state where available.

### Rule 11: Mutation Authority Is The Public Write Boundary

Authoritative memory writes must use mutation commands with:

- idempotency key,
- actor identity,
- evidence anchor refs,
- expected version/concurrency token,
- policy decision,
- review requirement,
- audit event,
- projection invalidation.

Low-level repository upserts may exist as internal persistence mechanics only.

### Rule 12: Workspace Is Not Truth

Cognitive workspace frames are active control state. They can reference claims, sources, procedures, and traces, but they do not become raw source truth. Important frames may be persisted as episodic input only through source/evidence policy.

### Rule 13: Salience Does Not Override Policy

Novelty, risk, reward, usefulness, and user interest can affect priority, activation, replay, and attention. They cannot create truth, bypass access control, promote generated output, or leak project-private content into global memory.

### Rule 14: Simulation Is Speculative

Simulation and cross-project analogy outputs must remain hypotheses until source-backed, reviewed, and validated. A simulated procedure cannot become executable automation or active procedure guidance without maturity and review policy.

### Rule 15: Metamemory Must Block Unsafe Fluent Answers

The answer gate must support warnings, clarification, source audit, probing, review, learning proposal, and abstention. It is a security and correctness boundary, not only a UI annotation.

### Rule 16: Self-Regulation Is Not Authority

Self-model, calibration health, humility triggers, answer posture, professor review, prediction errors, salience signals, and probing outcomes are control/evidence surfaces. They can require review, source audit, probing, learning, replay, or abstention. They cannot directly create canonical truth, grant access, bypass redaction, or mutate claims/procedures outside mutation authority.

Professor review records must preserve model profile, prompt/profile version, source access level, input ids, output hash, and resulting governed actions. External professor review must not receive restricted context unless policy explicitly allows it.
