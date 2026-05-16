# 18 Procedural Skill Memory Simulation

## Status

- Ready after `17-temporal-replay-scheduler`.
- Required before workflow automation promotion or MAF procedure guidance is considered complete.

## Objective

Upgrade procedural memory from passive runbooks into validated skill records, and add a speculative simulation sandbox for procedure alternatives and cross-project analogies.

## Covered Inputs

- Neuro patch FR-049, FR-050 and NFR-030.
- Patch finding H-05.
- Existing v2 procedure extraction, workflows, plugins, MAF, review, and governance architecture.

## Prerequisites

- Mutation authority exists.
- Evidence anchors, claims, context frames, episodes, replay jobs, and signals exist.
- Consolidation can extract candidate procedural evidence.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\23-procedural-skill-memory-and-simulation.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\06-consolidation-engine.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\08-maf-workflow-agent-integration.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\10-security-governance-and-provenance.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\contracts\csharp\CognitiveMemory.NeuroPatchContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\validation\test-and-quality-plan.md

## Deliverables

- Procedure skill, step, failure mode, maturity, risk, and automation-binding model.
- Simulation sandbox output model and speculation policy.
- Procedure-to-workflow/template promotion rules.
- Procedure-specific validation evidence and review gates.
- Updates to procedure extraction contracts to return procedure skill candidates, not only generic memory items.

## Dependency Impact

- MAF/workflow integration can only use procedural memory safely after maturity and validation policy.
- Metamemory answer gate uses procedure maturity/risk to warn or abstain.
- Replay scheduler can validate/rehearse procedure skills.
- Epistemic Drive can propose learning for weak procedures and simulation-required source gaps.

## Validation Depth

- Unit/integration tests for procedure skill preconditions, steps, postconditions, failure modes, maturity, risk, and evidence.
- Negative tests proving draft/simulated procedure cannot become automatable.
- Simulation labeling tests proving outputs remain speculative.
- Policy tests for high-risk automation and cross-project analogy access.
- EF/performance tests for procedure skill lists/details and failure-mode queries.

## Implementation Steps

1. Add procedure skill, step, failure mode, maturity, and simulation records/configurations.
2. Update procedure extraction contract and consolidation output.
3. Add maturity evaluation and validation evidence services.
4. Add simulation sandbox service and speculation labels.
5. Add workflow/MAF promotion guardrails.
6. Add tests and Docker procedure fixtures.

## Scope Exceptions

- Do not build a full workflow-template generator in this subbundle.
- Do not execute external studies or autonomous procedure validation beyond approved local/test evidence.

## Do Not Do

- Do not promote generated runbooks directly to active procedure truth.
- Do not make simulation output authoritative.
- Do not allow high-risk procedures to become automatable without review/validation.
- Do not ignore context frames when reusing procedures cross-project.

## Acceptance Checklist

- Procedure skills are not generic memory items only.
- Skill maturity gates automation binding.
- Failure modes are first-class and linked to prediction errors/episodes.
- Simulation output is visibly speculative.
- Cross-project analogies are access-policy filtered.

## Proof Required

- Build/test output.
- EF model/index proof.
- Procedure maturity and simulation labeling tests.
- High-risk automation rejection proof.
- Implementation report with deviations.

## Browser Validation Logging

- Browser proof is required if this phase exposes procedure skill review/detail screens.
- Log route, viewport, actions, screenshot paths, and visual checks in `reviews/01-execution-report.md`.

## Progression Gate

- Do not proceed to MAF procedure guidance, answer-gate procedure decisions, Epistemic Drive procedure learning, or distributed procedure replay until skill maturity and simulation-safety tests pass.
- Reopen this subbundle if generated/simulated procedures can become active without validation.

## Suggested Agent Prompt

Implement procedural skill memory and simulation sandbox. Model procedures as validated skill graphs with failure modes and maturity, keep simulation speculative, and gate workflow/automation promotion through evidence and review policy.

