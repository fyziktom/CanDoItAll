# Neuro Architecture Patch Reference

## Source

- Patch bundle path: `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-neuro-architecture-patch`
- Apply date: 2026-05-16
- Target bundle: `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2`

## Imported Inputs

The patch adds neuro-cognitive architecture mechanisms that the v2 bundle did not model strongly enough:

- cognitive workspace frames and attention routing,
- prediction error and salience signal ledgers,
- claim/evidence/belief ledger and mutation authority,
- schema/entity/context binding,
- temporal episodic memory and replay scheduling,
- procedural skill memory and simulation sandbox,
- metamemory answer gate and abstention.

## Integration Decision

These are not late optional features. The foundation pieces affect the shape of ingestion, canonicalization, projections, recall, probing, Epistemic Drive, cross-project promotion, and distributed replay. The v2 execution plan now schedules the patch foundation before source ingestion and recall-dependent phases.

