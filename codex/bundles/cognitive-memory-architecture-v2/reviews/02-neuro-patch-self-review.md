# Neuro Patch Self Review

## What Was Added

- Neuro-cognitive architecture docs for workspace/attention, prediction error/salience, claim/evidence/belief, context binding, temporal replay, procedural skills/simulation, and metamemory answer gating.
- Contract sketch `CognitiveMemory.NeuroPatchContracts.cs` with strongly typed states, decisions, command kinds, context kinds, signal kinds, replay kinds, and answer-gate decisions.
- Requirements FR-039 through FR-052 and NFR-025 through NFR-033.
- Subbundles `14` through `20`, mirrored into `plan/subbundles`.
- Diagrams `14` through `17`.
- Validation plan additions and patch-specific test plan.

## Preserved Decisions

- Qdrant remains a rebuildable projection.
- Raw sources and evidence anchors remain below summaries and projections.
- Generated summaries, probing feedback, simulation output, replay output, and distributed worker output do not directly become truth.
- Epistemic Drive remains vector/evidence based and approval-gated.
- High-risk memory/procedure changes remain review-gated.

## Risks Reduced

- Canonical summaries can no longer hide claim-level contradictions as the intended design.
- Direct public upsert-style mutation is no longer the planned authoritative write boundary.
- Working memory is no longer confused with rendered context packs.
- Confidence calibration is now used at answer time through the metamemory gate.
- Production/test/local/CI context separation is represented before merge, recall, answer rendering, and procedure execution.

## Remaining Risks

- Entity/context binding must start deterministic and testable; over-ambitious extraction could delay the first slice.
- Claim granularity can explode if implementation tries to atomize everything at once. First slice should focus on high-risk/source-backed claims and Docker context-separation fixtures.
- Answer gate thresholds will need calibration from real probing data.
- Procedure skill maturity must not become a checkbox; validation evidence and failure modes need real proof.

## Deferred Implementation

- No product code was implemented.
- Full entity extraction/classification, replay prioritization tuning, simulation quality, and answer-gate thresholds are implementation-phase decisions.
- Browser proof remains planned for UI-affecting phases only.

## Ordering Review

- Root `subbundles/` is authoritative.
- `plan/subbundles/` is mirrored from root subbundles.
- `plan/01-phase-plan.md` is the authoritative dependency order and intentionally schedules subbundle `14` before source ingestion despite its patch-origin folder number.
