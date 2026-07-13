# SBxx Title

## Status

- `Ready`

## Objective

State the implementation outcome and the defect or architecture risk it closes.

## Covered Inputs

- Cite the user request, GPTPro note, requirement id, or prior subbundle dependency.

## Prerequisites

- List the gates that must pass before implementation starts.

## Exact Source References

- `bundle://path/to/source.md`
- `repo://path/to/source.cs`

## Deliverables

- List source changes, tests, artifacts, and proof outputs.

## Dependency Impact

- Explain which later subbundles or runtime surfaces depend on this change.

## Validation Depth

- State whether this is characterization, critical behavior, architecture boundary, runtime reliability, UI diagnostics, or closure.

## Implementation Steps

1. Start with failing-first proof.
2. Make the smallest production change that satisfies the contract.
3. Add positive and negative tests.
4. Capture proof artifacts.

## C# Architecture Impact

Describe the boundary and ownership consequences.

## Boundary Ownership

- Name the owning layer or module.

## Dependency Direction

- State allowed and forbidden dependencies.

## Pattern Decision

- Name the chosen pattern and rejected shortcut.

## Testability Contract

- State how the behavior can be tested without full runtime where possible.

## Partial Class Policy

- State whether partial classes are forbidden, unchanged, or require closure justification.

## Architecture Proof Required

- List architecture proof required after execution.

## Do Not Do

- List shortcuts that would fake completion.

## Acceptance Checklist

- List observable behavior and test outcomes required to close the phase.

## Proof Required

- `bundle://proof/SBxx/manifest.md`
- `bundle://proof/SBxx/semantic-invariants.md` when behavior changes are critical.

## Browser Validation Logging

- State whether browser validation is required and how to log it.

## Progression Gate

- State what later work remains blocked until this closes.

## Suggested Agent Prompt

Write the exact implementation prompt for the execution agent.
