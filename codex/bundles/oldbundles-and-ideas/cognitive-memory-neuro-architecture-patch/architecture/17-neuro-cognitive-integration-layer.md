# 17 Neuro-Cognitive Integration Layer

## Purpose

Add an explicit layer that connects source-grounded durable memory with cognitive control mechanisms inspired by human cognition.

This layer does not claim to simulate a biological brain. It translates useful cognitive mechanisms into enterprise software responsibilities:

| Cognitive concept | Software responsibility |
|---|---|
| Working memory | Active scoped workspace with focus slots and context budget. |
| Attention | Executive router deciding the next cognitive operation. |
| Salience | Durable multi-dimensional signal ledger. |
| Prediction error | Expected-vs-actual comparison that drives learning. |
| Belief revision | Claim/evidence ledger with support and attack links. |
| Context binding | Entity, scope, environment, and temporal disambiguation. |
| Replay | Scheduled rehearsal/reprocessing of important weak memories. |
| Procedural skill | Validated step graph with preconditions and failure modes. |
| Metamemory | Answer-time awareness of uncertainty and source sufficiency. |

## Why This Layer Is Needed

The existing architecture has good storage, recall, consolidation, probing, and learning proposal concepts. But it lacks a control layer that answers these questions:

- What is currently in focus?
- Which memory candidates should be inhibited because they are contextually wrong?
- Is this an answer problem, a source problem, a context problem, or a missing-knowledge problem?
- What did the system expect, and what actually happened?
- Which claims are supported, attacked, stale, or only valid in a specific context?
- Which weak memories should be replayed now?
- Should the system answer, ask, probe, abstain, or open review?

## Placement

```text
Source adapters
  -> Ingestion and canonicalization
  -> Schema/entity/context binding
  -> Claim/evidence/belief ledger
  -> Canonical memory items and relations
  -> Projection builders / Qdrant

Recall / Probing / Workflows
  <-> Cognitive workspace
  <-> Attention router
  <-> Metamemory answer gate
  <-> Prediction error engine
  <-> Salience signal ledger
  <-> Replay scheduler
```

## Main Components

### `ICognitiveWorkspaceService`

Creates and updates active working-memory frames for a user, agent, workflow, process run, or probing session.

Responsibilities:

- manage focus slots,
- track current goal stack,
- hold unresolved questions,
- preserve selected candidate memory and claims,
- record inhibited candidates and why,
- maintain context/token budget,
- expire or persist frames as episodic source input.

### `IAttentionRouter`

Decides what to do next.

Possible decisions:

- recall,
- answer from current workspace,
- ask clarification,
- run source audit,
- start probe,
- request learning proposal,
- enqueue review,
- run consolidation/replay,
- abstain.

### `IClaimEvidenceLedger`

Stores atomized claims, evidence anchors, support/attack relations, temporal validity, context frames, and belief state.

### `ICognitiveSignalLedger`

Stores event-sourced cognitive signals such as novelty, surprise, risk, usefulness, recurrence, validation success, and user interest.

### `IPredictionErrorEngine`

Compares predictions with outcomes from probing, workflows, tests, process runs, and human feedback.

### `IReplayScheduler`

Schedules replay/rehearsal jobs using cognitive signals and memory risk.

### `IMetamemoryAnswerGate`

Prevents unsafe fluent answers by enforcing source sufficiency, confidence calibration, redaction awareness, and abstention rules.

## Integration With Existing Architecture

### Source Ingestion

The ingestion pipeline should add schema/entity/context binding before canonical memory is promoted.

### Recall

Recall should read from the cognitive workspace and claim ledger, not only memory item projections. Recall traces should include:

- workspace frame id,
- attention routing decision,
- selected claims,
- inhibited candidates,
- source sufficiency,
- answer gate result.

### Probing

Probe feedback should publish:

- prediction error records,
- cognitive signal records,
- claim correction candidates,
- review items,
- regression tests.

Probe feedback must still not directly mutate authoritative memory.

### Epistemic Drive

Epistemic Drive should consume cognitive signals and prediction error records as evidence, while preserving the existing multi-dimensional vector model.

### Consolidation

Consolidation should become one consumer of the replay scheduler. Nightly consolidation remains, but replay can also be triggered by prediction error, high salience, or spaced rehearsal.

## Non-Goals

- Do not create a black-box autonomous consciousness layer.
- Do not let salience override source policy.
- Do not convert speculative associations into truth.
- Do not bypass human review for high-risk memory changes.
- Do not make the cognitive workspace a durable source of truth.

## Minimal V1 Patch

For V1, implement the architecture documentation and contract sketches for:

1. Cognitive workspace frames.
2. Attention router decisions.
3. Claim/evidence ledger.
4. Prediction error and salience signal records.
5. Metamemory answer gate.
6. Replay scheduler design.
7. Procedure skill memory model.

Implementation may come later in separate execution bundles.
