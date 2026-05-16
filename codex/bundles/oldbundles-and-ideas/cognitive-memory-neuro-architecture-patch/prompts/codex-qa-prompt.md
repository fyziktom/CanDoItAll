# Codex QA Prompt: Neuro-Cognitive Patch Review

You are a senior architecture reviewer. Review the updated `cognitive-memory-architecture` bundle after the neuro-cognitive patch has been applied.

## Review Objectives

Verify that the updated bundle:

- preserves source truth and Qdrant projection boundaries,
- adds cognitive workspace and attention router clearly,
- adds claim/evidence/belief ledger without replacing source manifests,
- adds prediction error and salience signals as durable evidence,
- adds temporal episodic sequence and replay scheduling,
- upgrades procedural memory into validated skill memory,
- adds metamemory answer gating and abstention,
- uses mutation authority for authoritative writes,
- keeps probing feedback as evidence only,
- updates requirements, traceability, validation, diagrams, and subbundles consistently.

## Failure Conditions

Fail the review if:

- Qdrant is treated as source truth,
- generated summaries can become authoritative without source anchors,
- probe feedback can directly overwrite approved memory,
- salience signals can bypass policy,
- simulated/hypothetical output can become active procedure without review,
- `MemoryItem` remains the only belief unit without claim/evidence extension,
- working memory is described only as `RecallContextPack`,
- answer confidence calibration is recorded but not used by an answer gate,
- root subbundle order and plan order disagree without explanation,
- C# contract comments are not English.

## Required Evidence

Produce a QA report with:

- pass/fail per area,
- file references,
- missing updates,
- contradictions introduced by the patch,
- corrected execution order,
- recommended next implementation gate.
