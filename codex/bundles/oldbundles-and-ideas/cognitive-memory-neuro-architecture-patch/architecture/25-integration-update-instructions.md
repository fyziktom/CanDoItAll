# 25 Integration Update Instructions For Codex

## Goal

Apply this patch to the existing `cognitive-memory-architecture/` bundle and produce a complete updated architecture ZIP.

## Files To Add

Add these architecture files to the original bundle:

- `architecture/17-neuro-cognitive-integration-layer.md`
- `architecture/18-cognitive-workspace-and-attention-router.md`
- `architecture/19-prediction-error-salience-signal-ledger.md`
- `architecture/20-claim-evidence-belief-ledger.md`
- `architecture/21-schema-entity-context-binding.md`
- `architecture/22-temporal-episodic-memory-and-replay.md`
- `architecture/23-procedural-skill-memory-and-simulation.md`
- `architecture/24-metamemory-confidence-and-abstention.md`

Add the contract sketch:

- `contracts/csharp/CognitiveMemory.NeuroPatchContracts.cs`

Add diagrams:

- `diagrams/14-neuro-cognitive-overview.mmd`
- `diagrams/15-attention-recall-probing-loop.mmd`
- `diagrams/16-claim-evidence-belief-ledger.mmd`
- `diagrams/17-replay-and-procedural-memory-flow.mmd`

Add new subbundles:

- `subbundles/14-neuro-foundation-claim-evidence-ledger/README.md`
- `subbundles/15-cognitive-workspace-attention-router/README.md`
- `subbundles/16-prediction-error-salience-signals/README.md`
- `subbundles/17-temporal-replay-scheduler/README.md`
- `subbundles/18-procedural-skill-memory-simulation/README.md`
- `subbundles/19-metamemory-abstention-calibration/README.md`
- `subbundles/20-architecture-integration-closure/README.md`

## Existing Files To Update

### `README.md`

Add:

- neuro-cognitive integration summary,
- updated reading order,
- updated execution order,
- explicit statement that working memory is not equivalent to recall context pack,
- explicit statement that claims/evidence are below memory items.

### `CZ_SUMMARY.md`

Add a short Czech summary of the neuro-cognitive patch.

### `MANIFEST.md`

Add the new files and subbundles.

### `neuroscience/01-human-memory-principles.md`

Add these principles:

- predictive coding / prediction error as learning signal,
- working memory as active control space,
- replay/rehearsal and retrieval practice,
- belief revision and source-grounded memory correction,
- procedural skill learning and action policies,
- metamemory and confidence/uncertainty awareness.

### `neuroscience/02-neuroscience-to-system-mapping.md`

Extend the mapping table with:

- cognitive workspace,
- attention router,
- prediction error engine,
- salience signal ledger,
- claim/evidence/belief ledger,
- replay scheduler,
- procedure skill memory,
- simulation sandbox,
- metamemory answer gate.

### `architecture/03-memory-taxonomy-and-data-model.md`

Add entities/records:

- `CognitiveWorkspaceFrameRecord`,
- `WorkingMemorySlotRecord`,
- `AttentionDecisionRecord`,
- `MemoryEvidenceAnchorRecord`,
- `MemoryClaimRecord`,
- `MemoryBeliefStateRecord`,
- `MemoryMutationAuditRecord`,
- `CognitiveSignalRecord`,
- `PredictionExpectationRecord`,
- `PredictionErrorRecord`,
- `ContextFrameRecord`,
- `EntityRegistryRecord`,
- `TemporalEpisodeRecord`,
- `EpisodeStepRecord`,
- `MemoryReplayJobRecord`,
- `ProcedureSkillRecord`,
- `ProcedureFailureModeRecord`,
- `MetamemoryGateDecisionRecord`.

### `architecture/05-recall-orchestrator.md`

Add:

- workspace-aware recall,
- attention-router pre-stage,
- claim-level candidate support,
- inhibited candidate reasons,
- answer gate stage,
- structured recall trace extensions.

### `architecture/06-consolidation-engine.md`

Add:

- prediction-error consumption,
- salience signal consumption,
- replay scheduler integration,
- claim mutation candidate creation,
- procedure skill reinforcement.

### `architecture/10-security-governance-and-provenance.md`

Add:

- evidence anchors,
- mutation authority,
- speculative/simulation output policy,
- high-risk procedure maturity gates,
- answer abstention policy.

### `architecture/14-epistemic-drive-and-learning-orchestration.md`

Add:

- signal ledger as evidence input,
- prediction error as evidence input,
- answer-gate abstention as gap evidence,
- replay outcomes as evidence,
- vector dimension metadata.

### `architecture/15-interactive-memory-probing.md`

Add:

- workspace frame per session,
- prediction expectation/error per important probe turn,
- claim-level correction candidates,
- signal publication from feedback,
- answer gate decision display.

### `architecture/16-probing-regression-and-calibration-loop.md`

Add:

- calibration records feeding answer gate,
- regression replay as replay scheduler job,
- claim-level expected constraints,
- context-frame expected constraints.

### `contracts/csharp/*.cs`

Do not blindly merge all contracts into one giant file if the architecture bundle prefers multiple files. The supplied patch contract file can be split into focused contract files.

### `requirements/01-normalized-requirements.md`

Add requirements FR-039 through FR-052 and NFR-025 through NFR-033 from `requirements/04-neuro-patch-requirements.md`.

### `requirements/02-acceptance-criteria.md`

Add acceptance sections for:

- cognitive workspace,
- attention router,
- claim/evidence ledger,
- prediction error/salience,
- temporal replay,
- procedural skill memory,
- metamemory answer gate,
- mutation authority.

### `traceability/01-requirement-traceability.md`

Add rows mapping new requirements to new subbundles.

### `validation/test-and-quality-plan.md`

Add tests from `validation/neuro-patch-test-plan.md`.

### `plan/01-phase-plan.md`

Update execution order. Recommended order after original prerequisite/foundation work:

1. `14-neuro-foundation-claim-evidence-ledger`
2. `15-cognitive-workspace-attention-router`
3. `16-prediction-error-salience-signals`
4. `17-temporal-replay-scheduler`
5. `18-procedural-skill-memory-simulation`
6. `19-metamemory-abstention-calibration`
7. Continue or align with existing Epistemic Drive/probing subbundles.
8. `20-architecture-integration-closure`

## Bundle Drift Cleanup

Codex must resolve or clearly document drift between:

- root `subbundles/`,
- `plan/subbundles/`,
- README execution order.

Recommended rule:

- root `subbundles/` is authoritative,
- `plan/subbundles/` is a mirror or summary index,
- README and MANIFEST must use the same order.

## Self-Review Required

Codex must write a self-review explaining:

- what was added,
- what existing design was preserved,
- why the additions are necessary,
- which risks remain,
- which implementation choices are still deferred.
