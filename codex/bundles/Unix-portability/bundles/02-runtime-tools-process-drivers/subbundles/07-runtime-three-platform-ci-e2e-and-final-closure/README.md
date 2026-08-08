# B07 — Runtime three-platform CI, E2E, and final closure

## Mission

Prove runtime nodes, Manager, MCP, tools, plugins, and Processes on actual Windows/Linux/macOS hosts and close the full Unix portability program.

## Why now

Neutral build success cannot prove process trees, terminal/desktop availability, executable permissions, native discovery, MCP setup, Docker, or process-domain behavior.

## Scope

- Execute only the tasks and requirements owned by this subbundle.
- Update affected source references, findings, requirements, ADRs, validation, and evidence.
- Preserve established architecture and migration compatibility.

## Out of scope

- Downstream subbundle implementation.
- Opportunistic unrelated cleanup.
- Changes to external repositories/packages unless this subbundle explicitly invokes a split/quarantine path.
- Commit, push, or PR publication without explicit operator instruction.

## Source hotspots

- `{{REPO_ROOT}}/.github/workflows-disabled/ci.yml`
- `{{REPO_ROOT}}/tests/Unit/CanDoItAll.Tests.Unit`
- `{{REPO_ROOT}}/tests/Integration/CanDoItAll.Tests.Integration`
- `{{REPO_ROOT}}/tests/Playwright/CanDoItAll.Tests.Playwright`
- `{{REPO_ROOT}}/tests/Playwright/CanDoItAll.Tests.Playwright/PlaywrightAppFixture.cs`

## Requirements

`RCI-001`, `RCI-002`, `RCI-003`, `RCI-004`, `RCI-005`, `RCI-006`, `RCI-007`

## Prerequisites

- `B06`
- `Gate R3`

## Deliverables

- Production and test changes limited to this scope.
- Failing-first or named characterization proof.
- Updated evidence and gate report.
- Updated source/finding/requirement traceability.
- Redaction scan result.
- Session handoff.

## Architecture constraints

- No broad platform service, duplicate process/path/secret stack, insecure fallback, automatic Unix elevation, or name-only process kill.
- Use logical versus physical path contracts correctly.
- Keep MAF generic and process semantics in `Processes`.
- Use typed process arguments; shell only for explicitly modeled scripts.
- Keep source-code comments in English.

## Entry gate

- Status before execution: `Blocked by Gate R3`
- Verify exact HEAD, dirty state, prerequisites, and prior evidence.
- Reproduce the relevant baseline before edits.

## Exit gate

- Final Gate R4 is GO with actual-host Windows/Ubuntu/macOS evidence.
- All runtime P0 requirements are Solved and no critical finding remains open.
- Core Gate C4 remains valid and Windows regression is green.
- Support/limitation, rollback, external dependency, and evidence manifests are complete.

## Status

- `Blocked by Gate R3`

## Handoff

Record changed files, commands/results, evidence paths, design decisions, residual risks, and the next eligible subbundle. Stop on NO-GO.
