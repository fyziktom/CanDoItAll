# Task 08: Add project-structure acceptance criteria matrix

## Goal

A multi-team development process must validate arbitrary .NET applications, not only simple scaffold absence.

## New artifact

Add a produced artifact slot such as:

- `acceptance-criteria-matrix`

Suggested JSON/Markdown shape:

```json
{
  "criteria": [
    {
      "id": "AC-001",
      "sourceNodeId": "custom:...",
      "summary": "Automatic falling-piece loop is implemented outside Razor event handlers.",
      "verificationMethods": ["unit-test", "source-inspection", "browser-proof"],
      "requiredForAcceptance": true
    }
  ]
}
```

This is generic. The example above is Tetris data from project structure, not a hardcoded template rule.

## Process integration

- `feature-intake` or `architecture-review` creates/updates the matrix.
- `implementation` maps code changes and tests to criteria ids.
- `peer-review` verifies mapping coverage.
- `qa-validation` accepts only if required criteria have adequate proof.
- `quality-repair` fixes criteria failures.
- `qa-recheck` revalidates the matrix.

## Acceptance

- Calculator criteria stay simple.
- Tetris-like criteria catch missing game loop, keyboard input, next piece UI, and local score persistence.
- QA cannot accept a shell UI just because browser screenshot exists.
