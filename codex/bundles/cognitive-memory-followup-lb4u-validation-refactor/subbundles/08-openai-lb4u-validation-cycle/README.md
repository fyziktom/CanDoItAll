# 08 OpenAI LB4U Validation Cycle

## Status

- Status: `Ready`

## Objective

Run the main staged LB4U cognitive-memory validation using OpenAI `gpt-5-mini`, including ingestion, consolidation, probing, review decisions, deeper study, and regression evidence.

## Covered Inputs

- LB4U staged manifest.
- OpenAI model profile.
- Consolidation, epistemic, and probing improvements.
- Workbook validation matrix.

## Prerequisites

- Subbundles 02, 03, 04, 05, and 06 must pass.
- API must be running or started in a controlled way.
- Provider profile for `gpt-5-mini` must be available.

## Exact Source References

- C:\Users\lucys\OneDrive - TechnicInsider\Brano\LB4U
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.cs
- C:\Users\lucys\.codex\skills\candoitall-api-cognitive-memory\SKILL.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-followup-lb4u-validation-refactor\inputs\04-memory-probing-script.md

## Deliverables

- Multi-cycle OpenAI validation evidence.
- Operation ids for ingestion, consolidation, review, probes, and snapshots.
- Accepted/rejected recommendation records.
- Before/after memory quality comparison.
- Workbook evidence log updates.

## Dependency Impact

- Unblocks Ollama validation.
- May reveal additional fixes needed in earlier subbundles.
- Final closure cannot proceed until this gate passes or is explicitly blocked with proof.

## Validation Depth

- At least three staged cycles with `gpt-5-mini`.
- Runtime API smoke and snapshots.
- Probe answer quality review.
- Secret absence checks.
- Test command rerun after fixes.

## Implementation Steps

1. Select OpenAI `gpt-5-mini` model profile.
2. Ingest LB4U stages incrementally.
3. Run consolidation after each stage.
4. Probe using the script.
5. Approve useful recommendations and reject weak ones.
6. Ask for deeper study when recall misses source-backed facts.
7. Capture snapshots and quality evidence.

## Do Not Do

- Do not bulk import all stages at once.
- Do not accept recommendations without source support.
- Do not manually seed generic planning rules.
- Do not skip secret absence checks.

## Acceptance Checklist

- `gpt-5-mini` is explicitly selected.
- Staged ingestion evidence exists.
- Memory answers are source-backed and useful.
- Deeper-study loop improves at least one missed area.
- Secret exclusion remains clean.

## Proof Required

- API operation ids.
- Probe transcript summaries.
- Review decision ids.
- Snapshot deltas.
- Workbook evidence.

## Browser Validation Logging

- Browser validation is required if UI is used for review/probe evidence.
- Log route, viewport, screenshots, and result.

## Progression Gate

- Proceed to subbundle 09 only after OpenAI validation passes or blockers are documented with fixes.

## Suggested Agent Prompt

Run the staged LB4U validation with OpenAI `gpt-5-mini`. Behave like a human project user: ingest in stages, probe, review, request deeper study, and record evidence.
