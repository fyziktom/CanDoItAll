# SB08 - Event-backed Assimilation, Mastery, And Fading

## Status

- Status: `Completed`
- Criticality: `Critical`
- Execution order: `SB08`

## Objective

Replace keyword mastery with durable mastery/use/integration events and close professor anchor state transitions.

## Covered Inputs

- R-11
- R-12
- R-16

## Prerequisites

- Read the root README, current-state analysis, assumptions/risks, target architecture, and phase plan.
- Reopen all exact source references before changing code.
- For critical subbundles, create and maintain `proof/SB08/semantic-invariants.*` before closure.

## Exact Source References

- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorService.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAssimilationEvaluator.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorTransitionAudit.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedContracts.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedEntities.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedEntityConfigurations.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Signals/CognitiveMemorySignalContracts.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityEntities.cs
- repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs

## Deliverables

- Introduce event-backed professor mastery evidence records or reuse existing calibration/recall feedback events with explicit semantics.
- Count repeated use only when the statement was used successfully, accepted, or reinforced, not merely persisted in a synthesis source map.
- Require aggregate-ready dream/cluster integration, not any cluster membership.
- Add auditable anchor transitions for Active, Comparing, Assimilated, Faded, Rejected, and returned-to-Active states.
- Make manual assimilation bypass explicit, reviewed, and auditable if it remains allowed.

## Dependency Impact

- Upstream invariants from earlier subbundles must remain green.
- Downstream cognitive-memory services that consume changed contracts, entities, options, or generated records must be retested.
- Persistence changes require SQLite and PostgreSQL migration/model-snapshot proof where applicable.

## Validation Depth

- Add or use failing-first semantic tests for the owned invariants.
- Add targeted passing tests and at least one adversarial negative test.
- Run anti-stub audit against changed production files.
- For backend-only changes, browser validation can be N/A with an explicit reason; UI changes require Playwright evidence.

## Implementation Steps

- Design and migrate professor anchor mastery/use event records or typed reuse of existing feedback entities.
- Update ProfessorAssimilationEvaluator to stop using words like mastered/internalized as proof.
- Update repeated-use counting to require accepted outcome events.
- Update integration check to require an approved/applied aggregate or aggregate-ready cluster with sufficient coverage.
- Add transition audit rows and repair Comparing anchors when candidate validation fails or stays unresolved beyond policy.

## Do Not Do

- Do not fade a professor quote because a memory says it is mastered.
- Do not count a source link and evidence link from the same underlying source as two independent supports.
- Do not leave Comparing anchors permanently stuck.

## Acceptance Checklist

- All owned requirements are implemented without downgrading semantics.
- Semantic invariant contract exists and is cited by the proof manifest.
- Failing-first and passing transcripts exist for targeted tests.
- Changed source files are hashed and mapped to invariant IDs.
- No economic-governance scope creep is introduced.

## Proof Required

- Tests showing keyword-only mastery fails.
- Tests showing accepted-use events plus independent non-descendant support plus aggregate-ready integration pass.
- Tests showing Comparing anchor returns to Active or review when dream candidate is rejected.
- Migration/model snapshot proof if schema changes.

## Browser Validation Logging

- Backend-only unless this subbundle changes UI routes/components; if UI changes, add Playwright MCP evidence and screenshots.

## Progression Gate

- Passed. `bundle://proof/SB08/manifest.md` and `bundle://proof/SB08/semantic-invariants.md` cite current failing-first proof, targeted passing tests, professor lifecycle regressions, event-backed source assertions, anti-stub audit, no-migration proof, changed-file hashes, and prepared-stage validation.

## Suggested Agent Prompt

Implement SB08 exactly as written. First create or update the semantic invariant contract. Then implement the smallest production changes that satisfy the invariant generally, not only the fixture. Prove with failing-first and passing transcripts, changed-file hashes, anti-stub audit, downstream checks, and red-team notes. If any invariant cannot be satisfied, mark the subbundle blocked with a precise blocker instead of weakening the requirement.
