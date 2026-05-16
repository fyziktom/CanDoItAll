# Risk-Ranked Findings

## Critical Findings

### C-01: No Explicit Cognitive Workspace

Risk: agents may receive too much context, wrong context, or inconsistent state across a multi-turn reasoning/probing session.

Required fix: add `CognitiveWorkspaceFrame`, `WorkingMemorySlot`, `GoalStack`, attention budget, inhibition rules, and session/process/workflow scoping.

### C-02: No Claim-Level Belief Ledger

Risk: canonical summaries may hide contradictions or merge only partially compatible facts.

Required fix: add `MemoryClaim`, `MemoryEvidenceAnchor`, `EvidenceDirection`, `BeliefState`, support/attack links, temporal validity, scope, and source anchors.

### C-03: Direct Mutation Semantics Are Unsafe

Risk: consolidation, learning output, probing feedback, or distributed job acceptance could mutate memory in inconsistent ways.

Required fix: add `IMemoryMutationAuthority` and command-based mutation results. Existing stores may remain internal repositories but should not be exposed as the public mutation API.

### C-04: Prediction Error Is Not Unified

Risk: probing failures, QA failures, stale sources, and workflow rework become disconnected signals.

Required fix: add `PredictionExpectation`, `PredictionErrorRecord`, `IPredictionErrorEngine`, and a common signal publication path.

## High Findings

### H-01: Attention Is Embedded In Recall Instead Of Being Executive Control

Risk: the system cannot decide whether to recall, probe, ask a clarifying question, inspect sources, or abstain.

Required fix: add `IAttentionRouter` and route decisions with explanations.

### H-02: Entity/Context Binding Is Too Weak

Risk: semantically close concepts in different operational contexts merge incorrectly.

Required fix: add entity registry, alias resolution, context frames, and context-boundary rules.

### H-03: Temporal Episode Model Is Too Shallow

Risk: the system cannot reliably explain sequences, causality, rework, or decision history.

Required fix: add temporal episode records with ordered steps, causal edges, predictions, outcomes, and related claims/procedures.

### H-04: Replay Is Not Prioritized By Cognitive Signals

Risk: idle consolidation spends compute on easy summaries instead of high-value weak memories.

Required fix: add replay/rehearsal scheduler using salience, prediction error, risk, usage, staleness, and probing/regression outcomes.

### H-05: Procedural Memory Is Not Skill-Like Enough

Risk: extracted procedures become passive text and cannot safely drive workflows or agent tools.

Required fix: model procedures as skill graphs with preconditions, postconditions, steps, failure modes, automation bindings, and validation evidence.

### H-06: Metamemory Does Not Gate Answers

Risk: calibrated confidence is stored but not used before answers leave the system.

Required fix: add an answer gate with abstention, warning, clarification, source-audit, and probe decisions.

## Medium Findings

### M-01: Evidence References Are Too Coarse

Add source spans, quote hashes, structured paths, storage locators, trust state, and redaction state.

### M-02: `MemoryItemQuery.MinimumValidationState` Is Semantically Ambiguous

Validation states are not a linear quality scale. Replace with allowed state sets or a policy object.

### M-03: `MemoryProjectionPoint.Payload` Uses `object?`

Qdrant payloads should use serializable primitive/array shapes and a typed payload builder.

### M-04: KnowledgeNeedVector Needs Dimension Metadata

Store dimension scale, normalization algorithm, evidence contributors, and version. Otherwise future scans may not be comparable.

### M-05: Bundle Ordering Has Drift

Root `subbundles/` and `plan/subbundles/` use different numbering for probing and Epistemic Drive. Codex should make root `subbundles/` authoritative and update the plan index accordingly.

### M-06: Conversation/Probe Sessions Should Become Episodic Sources

Probe turns and user corrections should be available as source-like episodic inputs with access policy and redaction.

## Architecture Decision Summary

Codex should add the missing cognitive architecture as an extension layer and update existing contracts and subbundles. It should not destabilize the already-good projection/source/probing/governance decisions.
