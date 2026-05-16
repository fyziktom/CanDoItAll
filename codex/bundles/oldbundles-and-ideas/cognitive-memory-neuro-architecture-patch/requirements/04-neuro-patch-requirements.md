# 04 Neuro-Cognitive Patch Requirements

## Functional Requirements

### FR-039: Cognitive Workspace Frames

The system must represent temporary active working memory as scoped workspace frames with focus slots, goal stack, inhibited candidates, open questions, context budget, cognitive load, and expiry.

### FR-040: Attention Router

The system must have an explicit attention router that chooses the next cognitive operation: recall, answer from workspace, ask clarification, source audit, probe, review, learning proposal, replay, or abstention.

### FR-041: Claim/Evidence/Belief Ledger

The system must support atomic memory claims with source evidence anchors, support/attack evidence, scope/context frames, confidence, validation state, temporal validity, and belief state.

### FR-042: Evidence Anchors

The system must support fine-grained evidence anchors including source ids, storage locators, structured paths, text spans, quote hashes, trust level, redaction state, and source version/hash.

### FR-043: Memory Mutation Authority

Authoritative memory changes must pass through a mutation authority with idempotency, optimistic concurrency, actor identity, evidence checks, review policy, audit events, and projection invalidation.

### FR-044: Schema, Entity, And Context Binding

The system must resolve entities, aliases, schemas, and context frames before semantic merging or claim promotion. Context boundaries must prevent substituting semantically similar but operationally incompatible memories.

### FR-045: Prediction Expectations And Prediction Errors

The system must record expected outcomes and observed mismatches for important probe turns, workflow runs, procedure executions, QA events, and high-risk answers.

### FR-046: Salience Signal Ledger

The system must persist multi-dimensional cognitive signals such as novelty, surprise, risk, usefulness, reward, rework cost, contradiction pressure, user interest, staleness pressure, source weakness, and calibration risk.

### FR-047: Temporal Episodic Memory

The system must represent episodes as ordered sequences with actors, steps, decisions, artifacts, expected outcomes, actual outcomes, prediction errors, related claims, and related procedures.

### FR-048: Replay/Rehearsal Scheduler

The system must schedule replay jobs using cognitive signals, prediction errors, risk, staleness, usefulness, user interest, contradiction pressure, and procedure maturity.

### FR-049: Procedural Skill Memory

The system must represent procedures as skill records with preconditions, steps, postconditions, failure modes, validation evidence, maturity, risk, automation binding, and source anchors.

### FR-050: Simulation Sandbox

The system must support speculative simulation/planning outputs for procedure alternatives and cross-project analogies. Simulation outputs must be clearly marked as hypotheses and cannot become authoritative without review.

### FR-051: Metamemory Answer Gate

The system must evaluate answer readiness before rendering answers using source sufficiency, context fit, belief state, confidence calibration, contradiction risk, staleness, redaction, risk level, and access policy.

### FR-052: Workspace-Aware Probing

Probe sessions must attach to or create workspace frames, publish prediction errors and salience signals, and create claim-level correction candidates without directly mutating authoritative truth.

## Non-Functional Requirements

### NFR-025: No Direct Public Upsert For Authoritative Memory

Public write operations must use mutation authority. Repository-style upsert methods may exist internally only.

### NFR-026: No Silent Claim Merge

Claims with different context frames, validity windows, or evidence state must not be silently merged into one canonical truth.

### NFR-027: No Scalar-Only Salience

Salience must preserve signal dimensions. A display priority score may exist only as derived UI data.

### NFR-028: Explainable Attention

Attention decisions must include structured reasons and be persisted in recall/probe traces where relevant.

### NFR-029: Replay Safety

Replay jobs may produce draft changes, review items, regression results, or projection invalidations, but must not directly promote authoritative truth.

### NFR-030: Speculation Labeling

Simulation, analogy, and associative exploration outputs must be labeled speculative until source-backed and reviewed.

### NFR-031: Answer Abstention Safety

The answer gate must support abstention and clarification. The system must not hide uncertainty behind fluent wording.

### NFR-032: Context Boundary Safety

Context boundaries must be evaluated before answer rendering and procedure execution.

### NFR-033: Auditability Of Cognitive Signals

Signals, prediction errors, attention decisions, and answer gate decisions must be traceable to evidence, actor, time, and algorithm/profile version.

## Acceptance Criteria

- A recall/probe trace can show the workspace frame id, attention decision, selected claims, inhibited candidates, answer gate decision, and source anchors.
- A user correction creates claim mutation candidates and signal records, not direct approved memory changes.
- A production/test Docker confusion creates wrong-scope prediction error and context-boundary replay job.
- A high-risk procedure cannot become automatable until maturity and validation policy allow it.
- A source-poor high-confidence answer is blocked or warning-rendered by the answer gate.
- Replay priority changes after repeated probe failures or workflow rework.
- Cross-project analogy output remains speculative and access-policy filtered.
