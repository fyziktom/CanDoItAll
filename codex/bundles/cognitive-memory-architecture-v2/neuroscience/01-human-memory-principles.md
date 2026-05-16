# Human Memory Principles Used As Design Inspiration

This document maps neuroscience principles into software architecture. It is intentionally conservative: it uses neuroscience as inspiration, not as a claim that the system simulates the brain.

## 1. Memory Is Layered, Not Flat

Human memory is not a single store. It includes working memory, episodic memory, semantic memory, procedural memory, emotional salience, spatial/contextual memory, and metacognitive awareness.

Software implication:

- Do not build one table or one vector collection called `Memory`.
- Build multiple memory item kinds with explicit relations and retrieval policies.

## 2. The Hippocampus Links Episodes, Space, and Context

A useful engineering analogy is to treat the mindmap/project graph as a hippocampal index:

- it links items,
- encodes context,
- preserves rough spatial placement,
- helps later recall related areas without storing every detail directly.

Software implication:

- Mindmap position and graph edges are first-class memory features.
- Source coordinates should be part of recall scoring and clustering.

## 3. Systems Consolidation Moves From Episode To Stable Knowledge

In systems consolidation, memories initially dependent on hippocampal indexing can be reorganized into broader long-term knowledge structures. In engineering terms, detailed runs, artifacts, and mindmap nodes gradually produce stable project topics, decisions, and procedures.

Software implication:

```text
raw event/source -> episode -> canonical item -> semantic/procedural summary -> cross-project topic
```

## 4. Sleep/Rest Supports Replay And Reorganization

Sleep and quiet rest are associated with memory replay/reactivation and consolidation. For CanDoItAll, this maps naturally to idle/night jobs that replay recent process runs, artifacts, source changes, and user edits to update memory projections.

Software implication:

- Consolidation should run asynchronously during idle periods.
- It should replay recent changes and update summaries, clusters, activation, and contradictions.

## 5. Recall Is Progressive

People often recall a vague region first, then focus attention and retrieve detail later. Retrieval is not a single exact lookup.

Software implication:

Recall should be multi-stage:

1. coarse activation of candidate memory areas,
2. association expansion,
3. focus selection based on current goal,
4. detail loading from canonical/source records,
5. trace logging for later learning.

## 6. Attention Controls What Enters Working Memory

Human cognition is capacity-limited. A system should also avoid flooding agents with every similar chunk.

Software implication:

- Use a `RecallOrchestrator` that returns a bounded `RecallContextPack`.
- Include selected context, excluded-but-related context, uncertainty, and citations.
- Keep source detail available through tools rather than stuffing all detail into the model context.

## 7. Salience Matters

Humans remember surprising, risky, costly, emotional, or repeatedly useful information more strongly.

Software implication:

Memory records need activation signals:

- importance,
- recency,
- frequency of use,
- risk level,
- human validation,
- process failure/rework impact,
- contradiction state.

## 8. Forgetting Is Useful

Forgetting is not only a defect. It reduces noise and prevents obsolete details from dominating.

Software implication:

- Do not delete raw source by default.
- Use soft forgetting: activation decay, dormant state, superseded-by links, staleness penalties, and projection retirement.

## 9. Prediction Error Drives Learning

Humans learn strongly from mismatches between expected and observed outcomes. The system equivalent is a durable prediction-error ledger that records expectation, observation, cause hypothesis, magnitude, and suggested adaptation.

Software implication:

- Probing failures, workflow rework, QA failures, stale source conflicts, and wrong-scope answers should publish typed prediction errors.
- Prediction errors should feed replay scheduling, Epistemic Drive, confidence calibration, and procedure improvement.

## 10. Belief Revision Is Evidence-Based

Human memory updates are not just appending summaries. A disciplined software memory needs atomic claims with support, attack, scope, temporal validity, and revision lineage.

Software implication:

- Store claims below memory items.
- Use evidence anchors with spans/paths/hashes/trust/redaction state.
- Preserve contested or scope-limited claims instead of hiding them inside fluent summaries.

## 11. Procedural Skill Needs Practice And Validation

Procedural memory becomes reliable through repeated execution, feedback, and failure recovery.

Software implication:

- Model procedures as skill graphs with preconditions, steps, postconditions, failure modes, validation evidence, maturity, and automation policy.
- Simulation can explore hypotheses, but validated execution evidence is required before automation.

## 12. Metamemory Controls Confidence

Humans can sometimes know that they do not know. A memory system needs the same answer-time gate.

Software implication:

- The system must be able to answer with warnings, ask clarification, request source audit, probe, create review, request learning, or abstain.
- Confidence calibration must affect answer rendering, not only dashboards.

## References

- Squire et al. describe memory consolidation as a process involving hippocampal dependence and later reorganization into longer-term memory systems.
- Sleep/replay literature describes hippocampal and cortical replay/reactivation as a consolidation mechanism.
- These ideas are used here as design inspiration for source replay, consolidation jobs, and staged recall.
