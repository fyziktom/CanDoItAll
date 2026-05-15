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

## Threat Model

| Threat | Example | Mitigation |
|---|---|---|
| Poisoned source | Malicious email instructs agent to ignore policy | Source type trust score, prompt-injection scanner, restricted context pack. |
| Incorrect merge | Test Docker merged into production deployment | Relation type `semantically_related_but_contextually_separated`, human review for context conflicts. |
| Secret leakage | OAuth secret embedded into vector DB | redaction before embedding; skip `ContainsSecret`. |
| Stale knowledge | Old deployment procedure retrieved as current | supersession, staleness penalty, validity intervals. |
| Circular hallucination | Agent summary becomes source for future summaries | strict provenance levels; generated summaries cannot become raw truth. |
| Worker tampering | Distributed device returns bad cluster output | job input hash, output hash, worker identity, deterministic validation, coordinator acceptance. |

## Recommended EF Tables

- `MemorySourceManifestRecords`
- `MemorySourceItemRecords`
- `CanonicalMemoryRecords`
- `MemoryItemRecords`
- `MemoryRelationRecords`
- `MemoryProjectionRecords`
- `MemoryRecallTraceRecords`
- `MemoryConsolidationRunRecords`
- `MemoryHumanReviewItems`
- `MemoryAccessAuditRecords`

## Data Retention

Raw sources should follow existing project/storage retention policy. Derived projections can be compacted more aggressively:

- recall traces: retain summaries indefinitely, detailed context packs by policy,
- Qdrant projections: rebuild anytime,
- rejected relations: retain for learning and audit,
- distributed job outputs: retain hashes + summary, not necessarily full payload.
