# Codex Master Prompt: Apply Neuro-Cognitive Architecture Patch

You are a senior C#/.NET architect and cognitive-memory systems architect. You are updating an existing architecture bundle, not implementing runtime code.

## Input

You will receive an existing architecture bundle rooted at:

```text
cognitive-memory-architecture/
```

You will also receive this patch bundle rooted at:

```text
cognitive-memory-neuro-architecture-patch/
```

## Mission

Update the existing `cognitive-memory-architecture` bundle so it includes the missing neuro-cognitive mechanisms identified in this patch:

- Cognitive Workspace and Attention Router.
- Prediction Error Engine and Salience Signal Ledger.
- Claim/Evidence/Belief Ledger.
- Schema, Entity, and Context Binding.
- Temporal Episodic Memory and Replay Scheduler.
- Procedural Skill Memory and Simulation Sandbox.
- Metamemory Answer Gate.
- Memory Mutation Authority.

The existing architecture is good and must not be discarded. Preserve these decisions:

- Qdrant is a rebuildable projection, not durable memory.
- Raw source provenance is mandatory.
- Generated summaries are not source truth.
- Probing feedback is evidence, not direct mutation.
- Epistemic Drive preserves vector dimensions and evidence.
- Learning proposals are reviewable and approval-gated.
- Distributed workers cannot mutate authoritative memory.

## Hard Constraints

1. Do not implement runtime code.
2. Do not modify the actual CanDoItAll repository unless explicitly requested by a later task.
3. Do not add direct MAF private-provider logic.
4. Do not move durable memory semantics into Qdrant/RAG.
5. Do not make speculative/simulation output authoritative.
6. Do not allow user corrections or probe feedback to overwrite approved memory directly.
7. All source code comments in contract sketches must be in English.
8. Use root `subbundles/` as the source of truth for execution subbundles.

## Required Work

### Step 1: Inspect Existing Bundle

Read at least:

- `README.md`
- `MANIFEST.md`
- `CZ_SUMMARY.md`
- `architecture/03-memory-taxonomy-and-data-model.md`
- `architecture/05-recall-orchestrator.md`
- `architecture/06-consolidation-engine.md`
- `architecture/10-security-governance-and-provenance.md`
- `architecture/14-epistemic-drive-and-learning-orchestration.md`
- `architecture/15-interactive-memory-probing.md`
- `architecture/16-probing-regression-and-calibration-loop.md`
- `contracts/csharp/*.cs`
- `requirements/01-normalized-requirements.md`
- `requirements/02-acceptance-criteria.md`
- `traceability/01-requirement-traceability.md`
- `validation/test-and-quality-plan.md`
- `subbundles/*/README.md`

### Step 2: Add New Architecture Files

Copy/adapt patch files:

- `architecture/17-neuro-cognitive-integration-layer.md`
- `architecture/18-cognitive-workspace-and-attention-router.md`
- `architecture/19-prediction-error-salience-signal-ledger.md`
- `architecture/20-claim-evidence-belief-ledger.md`
- `architecture/21-schema-entity-context-binding.md`
- `architecture/22-temporal-episodic-memory-and-replay.md`
- `architecture/23-procedural-skill-memory-and-simulation.md`
- `architecture/24-metamemory-confidence-and-abstention.md`

### Step 3: Update Existing Architecture Docs

Apply the instructions in:

```text
architecture/25-integration-update-instructions.md
```

Do not simply append text blindly. Integrate it coherently.

### Step 4: Update Contracts

Add or split:

```text
contracts/csharp/CognitiveMemory.NeuroPatchContracts.cs
```

Also update existing contract sketches where necessary to show compatibility:

- recall trace includes workspace frame id and answer gate decision id,
- recall candidate can include selected claim ids,
- probe feedback can publish prediction errors/signals,
- procedure extraction should produce procedure skill records or skill candidates,
- memory mutation should go through mutation authority.

### Step 5: Update Requirements, Traceability, Validation

Add the new requirements from `requirements/04-neuro-patch-requirements.md`.

Update:

- `requirements/01-normalized-requirements.md`
- `requirements/02-acceptance-criteria.md`
- `traceability/01-requirement-traceability.md`
- `validation/test-and-quality-plan.md`

### Step 6: Add Subbundles

Add root subbundles 14-20 from this patch. Update README and plan ordering so Codex cannot confuse old plan numbering.

### Step 7: Add Diagrams

Add Mermaid diagrams 14-17 and update `diagrams/README.md`.

### Step 8: Self-Review

Write/update:

- `reviews/02-neuro-patch-self-review.md`
- `UPDATE_SUMMARY.md`

The self-review must answer:

1. What was added?
2. Which original decisions were preserved?
3. Which architecture risks are reduced?
4. Which risks remain?
5. What implementation work is still deferred?
6. Did you normalize subbundle ordering drift?

## Output

Produce a complete updated architecture ZIP.

The output should be self-contained. A later implementation agent should not need this patch bundle separately.
