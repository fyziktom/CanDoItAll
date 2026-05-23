# Structured Input

## Problem

The runtime recognizes missing required process artifacts after a completed implementation attempt, but the recovery action is executor-local. For the Tetris run, step two has multiple attempts and still lacks the required handoff artifacts, leaving the run stuck with `MissingArtifact` health.

## Desired Behavior

When the implementation executor completes but required artifacts are missing, the process runtime must ask the process manager to recover those artifacts using prior step history and attempt history. The manager recovery must be auditable and bounded.

## Non-Goals

- Do not weaken artifact validation.
- Do not mark the step complete without actual artifact records.
- Do not create a broad refactor of the process runtime.
- Do not rely on a repeated self-rerun of the same step executor.
