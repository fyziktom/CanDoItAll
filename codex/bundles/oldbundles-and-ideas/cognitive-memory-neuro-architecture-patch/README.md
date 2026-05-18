# CanDoItAll Cognitive Memory Neuro-Architecture Patch Bundle

## Purpose

This bundle is a separate Codex-ready architecture patch for the existing `cognitive-memory-architecture-with-interactive-probing` bundle.

The supplied architecture is already strong in these areas:

- Qdrant is correctly treated as a rebuildable projection, not as memory itself.
- Source provenance, human review, consolidation, recall traces, Epistemic Drive, and Interactive Memory Probing are already first-class concepts.
- The design correctly separates generated summaries from source truth and protects authoritative memory from direct mutation by probing feedback.

The remaining gaps are not basic RAG gaps. They are cognitive-architecture gaps between source ingestion, attention, working memory, prediction error, evidence-based belief revision, temporal replay, and procedural skill formation.

This patch instructs Codex to extend the architecture so the system behaves more like a disciplined cognitive system inspired by human memory, while remaining an auditable enterprise software system.

## Input Bundle Reference

Original uploaded bundle:

- File name: `cognitive-memory-architecture-with-interactive-probing.zip`
- SHA-256: `336bb656fc94f7c55169dce86494b55391fb35868b40cc4627bb6a6615836ea5`
- Root folder inside ZIP: `cognitive-memory-architecture/`

## Hard Rule

This patch is architecture-only. Codex must not implement runtime code from this patch unless a later implementation bundle explicitly authorizes it.

Codex should update the existing architecture bundle, add the missing architecture documents/contracts/subbundles, update traceability and validation, and produce a new complete architecture ZIP.

## Main Additions

1. **Cognitive Workspace and Attention Router**
   - Working memory is not just a context pack.
   - Add a durable/ephemeral working frame with focus slots, goal stack, cognitive load, inhibition, and context budget.
   - Add an attention router that decides whether to recall, probe, ask clarification, run consolidation, create review, or abstain.

2. **Prediction Error and Salience Signal Ledger**
   - Add typed surprise/error/novelty/risk/reward/usefulness signals.
   - Persist signals from probing, workflow failures, QA, successful runs, user corrections, and stale-source conflicts.
   - Feed signals into activation, replay scheduling, Epistemic Drive, and confidence calibration without collapsing them into one scalar.

3. **Claim-Evidence-Belief Ledger**
   - Canonical memory items are too coarse to safely manage contradictions.
   - Add atomic claims with source anchors, support/attack evidence, scope, validity windows, confidence, validation state, and revision lineage.
   - Summaries may compose claims, but cannot hide unresolved contradictions.

4. **Schema, Entity, and Context Binding**
   - Add first-class entity registry, aliases, context frames, and scope boundaries.
   - Prevent semantically similar but context-separated topics from merging accidentally.
   - Make production/test/local/CI/environment context an explicit typed frame, not only tags.

5. **Temporal Episodic Memory and Replay Scheduler**
   - Episode records need sequence, causality, outcomes, prediction errors, and validity windows.
   - Replay should prioritize weak, useful, risky, surprising, stale, or often-used memories.
   - Replay can create review/projection/regression jobs, but must not directly rewrite truth.

6. **Procedural Skill Memory and Simulation Sandbox**
   - Procedural memory needs preconditions, postconditions, steps, failure modes, validation evidence, automation bindings, and skill maturity.
   - Add a simulation/planning sandbox for hypothetical procedures and analogies. Hypotheses remain speculative until validated.

7. **Metamemory Answer Gate**
   - Add an answer-time gate that decides when to answer, answer with warnings, ask for clarification, request source inspection, create probe, or abstain.
   - Confidence calibration from probing must influence answer rendering and not only dashboards.

8. **Memory Mutation Authority**
   - Replace direct upsert-style mutation semantics with command-based mutation authority.
   - Enforce idempotency, optimistic concurrency, audit events, source/evidence checks, and review policy.

## Recommended Codex Execution

Use `prompts/codex-master-prompt.md` first. Codex should apply the patch in this order:

1. Read and summarize the original architecture bundle.
2. Add the new neuro-cognitive architecture documents.
3. Extend existing requirements, acceptance criteria, contracts, traceability, validation, diagrams, and subbundle ordering.
4. Normalize internal bundle drift: root `subbundles/` should be the source of truth; `plan/subbundles/` must either mirror it or be clearly marked as an index.
5. Produce a complete updated ZIP.

## Output Expected From Codex

A new complete architecture bundle ZIP containing the original architecture plus the neuro-cognitive patch, with:

- updated README and manifest,
- updated architecture docs,
- updated C# contract sketches,
- updated requirements and acceptance criteria,
- new subbundles 14-20,
- updated diagrams,
- updated validation plan,
- updated traceability,
- self-review notes explaining what changed and why.

