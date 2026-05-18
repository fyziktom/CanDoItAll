# Contract And Bundle Consistency Audit

## Contract Issues To Fix Or Clarify

### 1. `IMemoryStore` Should Not Be The Public Mutation Boundary

Current sketch exposes:

```csharp
Task UpsertMemoryItemAsync(MemoryItem item, CancellationToken cancellationToken = default);
Task UpsertRelationAsync(MemoryRelation relation, CancellationToken cancellationToken = default);
```

Architecture patch: introduce `IMemoryMutationAuthority`. Low-level stores can remain internal repositories, but all externally initiated changes must go through mutation commands with audit and review policy.

### 2. Validation State Query Should Not Use `MinimumValidationState`

`MemoryValidationState` is not a reliable ordinal. `Approved` is not simply greater than `HumanReviewed`; `Superseded`, `Rejected`, and `Retired` are not lower or higher quality in a linear way.

Architecture patch: replace query semantics with allowed sets or policy filters:

- `AllowedValidationStates`,
- `ExcludeValidationStates`,
- `RequireHumanApprovedForHighRisk`,
- `AllowDraftOnlyForReviewMode`.

### 3. Evidence References Need Anchors

`MemorySourceRef` should be supplemented with `MemoryEvidenceAnchor`:

- anchor kind,
- source manifest id,
- source item id,
- storage path/locator,
- structured path,
- text span,
- quote hash,
- trust level,
- redaction state.

### 4. Recall Candidate Reasons Need Structured Explanation

`SelectionReason` is not enough. Add structured score features and gate decisions:

- scores by channel,
- excluded-but-relevant reason,
- redaction effect,
- context boundary reason,
- confidence calibration reason,
- answer gate result.

### 5. Projection Payload Needs Typed Serialization

`IReadOnlyDictionary<string, object?> Payload` is flexible but risky. The architecture should require a projection payload serializer that validates Qdrant-compatible primitive payloads and stores schema/profile versions.

### 6. Knowledge Need Vectors Need Versioned Dimension Schema

`KnowledgeNeedVector` should store or reference:

- vector schema version,
- dimension scale and normalization,
- source feature contributors,
- missing dimension policy,
- calculation confidence.

### 7. Probe Turn Needs Strong Turn Ordering

`MemoryProbeTurnRequest` relies on session id, which is acceptable, but persistence should include turn number, parent turn id, and concurrency token to support branching corrections and replay.

### 8. Procedure Extraction Needs More Than `MemoryItem`

`IProcedureExtractor` returning `IReadOnlyList<MemoryItem>` is too generic. Add `ProcedureSkillRecord` and convert to `MemoryItem` projection only after validation.

## Bundle Consistency Issues

### 1. Duplicate Subbundle Sources

The original bundle has both:

- `subbundles/*`
- `plan/subbundles/*`

They partially overlap but are not identical. Codex should state one source of truth. Recommended: root `subbundles/` is authoritative; `plan/subbundles/` becomes a generated/summary index or is updated to mirror root.

### 2. Numbering Drift

The patch identified historical numbering drift between Epistemic Drive and Interactive Probing plan paths. This v2 integration resolves it by mirroring root `subbundles/` into `plan/subbundles/` and making `plan/01-phase-plan.md` the authoritative dependency order.

Patch instruction: keep old folder names for compatibility where needed, but add a clear execution-order table in README, MANIFEST, and `plan/01-phase-plan.md`.

### 3. `01-target-architecture.md` vs `01-target-solution.md`

Both files are useful, but Codex should ensure they do not diverge. Add a note identifying `01-target-solution.md` as the concise target and `01-target-architecture.md` as the expanded design.

### 4. Local Path References

Exact source references are useful, but they are local and may become stale. Codex should keep them but add a validation rule: source references must be rechecked against the current repo before implementation.

## Acceptance Of Existing Design

The patch does not reject the original architecture. It extends it with missing cognitive control and belief-management layers.
