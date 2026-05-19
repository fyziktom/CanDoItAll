# 04-policy-preserving-operations

## Status

- `Ready`

## Objective

Preserve explicit operator policy through all Cognitive Memory validation operations.

## Required Edits

- Store access level, risk level, and allow-restricted flag on probe sessions.
- Reuse stored policy when asking probe turns.
- Add policy fields or policy snapshots to relevant operation audit records.
- Add warnings when restricted source truth is excluded.

## Closure Proof

- Restricted probe session can recall restricted source truth when explicitly allowed.
- Project-only probe session cannot recall restricted source truth.

## Covered Inputs

- Restricted consolidation and probe runs must keep explicit operator access and risk policy instead of reconstructing weaker defaults.

## Prerequisites

- Probe session contracts and operation audit rows can be extended additively with policy fields.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedServices.cs`

## Deliverables

- Persisted policy snapshots for probe sessions and operation paths that reuse the stored policy for later turns.

## Dependency Impact

- Dreaming, recall, feedback, and review decisions depend on policy-correct source visibility.

## Validation Depth

- Unit tests must prove restricted and project-only sessions produce different source-truth behavior.

## Implementation Steps

- Add typed policy fields, persist them in session records, propagate them through ask/recall paths, and expose warnings when restricted truth is excluded.

## Do Not Do

- Do not infer policy from string labels or fall back to project-only visibility when the stored session policy is missing.

## Acceptance Checklist

- Probe asks reuse stored access level, risk level, and allow-restricted settings.
- Restricted exclusion is reported explicitly.

## Proof Required

- Focused unit tests covering policy preservation and restricted-source recall behavior.

## Browser Validation Logging

- Record large-screen probe/session UI proof when policy controls or warnings are surfaced.

## Progression Gate

- Proceed only when the policy context survives session creation, probe ask, recall, and review orchestration.

## Suggested Agent Prompt

- Harden Cognitive Memory policy propagation so probe sessions and downstream operations preserve explicit access policy without stringly-typed fallbacks.
