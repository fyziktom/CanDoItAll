# Codex Implementation Prompt: Interactive Memory Probing

You are a senior C#/.NET architect implementing the Interactive Memory Probing subbundle for CanDoItAll Cognitive Memory.

## Goal

Implement a probing workbench and service layer that lets users question Cognitive Memory like a student, inspect why it answered, correct it safely, and convert failures into review items, knowledge-gap evidence, and regression tests.

## Critical Rules

- Do not mutate active/canonical memory directly from a probe turn.
- Every probe answer must link to a recall trace.
- User corrections are evidence and review candidates, not automatic truth.
- High-risk procedural/security/deployment corrections require human review.
- Source refs and access/redaction policy must be preserved.
- Qdrant is optional for MVP; probing must work with lexical/graph fallback.
- Source code comments must be in English.

## Inputs To Read First

1. `architecture/15-interactive-memory-probing.md`
2. `architecture/16-probing-regression-and-calibration-loop.md`
3. `contracts/csharp/InteractiveMemoryProbingContracts.cs`
4. `subbundles/13-interactive-memory-probing-workbench/README.md`
5. `validation/probing-test-matrix.md`

## Expected Deliverables

- EF records/configurations for probe sessions, turns, feedback, findings, corrections, calibration events, and regression tests.
- `IMemoryProbeSessionService` implementation.
- `IMemoryProbeQuestionGenerator` implementation with deterministic and serendipitous inputs.
- `IMemoryProbeAssessmentService` implementation.
- `IMemoryRegressionTestService` implementation.
- Human review integration for correction candidates.
- Epistemic Drive evidence publisher for probe outcomes.
- Dialogue Workbench UI.
- Workflow executor registration for probe actions.
- Unit, integration, and browser tests.

## Required Golden Scenario

Use Docker context separation:

- production Docker deployment,
- test/simulation Docker deployment,
- local development Compose,
- CI Docker pipeline,
- unrelated UI testing.

The probe must catch an answer that uses a test-only Docker procedure as authoritative production guidance.

## Completion Evidence

Provide build/test output, UI screenshots, sample probe session report, sample review item, sample regression test, and a short architecture deviation report.
