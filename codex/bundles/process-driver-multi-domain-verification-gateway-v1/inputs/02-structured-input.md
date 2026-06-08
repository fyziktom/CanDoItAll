# Structured Input

## Source Request
- Preserve and verify the prior Codex work on branch `maf-processes-refactor` using real repository code, not only the previous bundle report.
- Identify required fixes and quality improvements after a Codex crash during implementation.
- Prepare broader next phases toward a stable Process Core with domain drivers.
- Keep the bundle larger and more complete than the previous one, but do not trade quality for speed.
- Follow the repository bundle shape under `repo://codex/skills/bundles`.
- Produce a zip-ready bundle.

## Scope Classification
- Profile: initiative bundle.
- Runtime/UI proof: N/A unless production UI or media files unexpectedly change.
- Primary source areas: Process Core, driver abstractions, transcript verification, runtime evidence verification, process-module read-only adapters, and related unit/integration tests.

## Requirement Groups
- REQ-001: Crash/source reconciliation.
- REQ-002: Unit debt cleanup.
- REQ-003..REQ-004: Process Core and driver abstraction stability.
- REQ-005..REQ-006: Transcript and runtime evidence verifier hardening.
- REQ-007..REQ-012: Controlled read-only verification gateway, evidence boundary, audit/redaction/no-mutation, and additional domain lanes.
- REQ-013..REQ-015: Release gates, no UI drift, and roadmap closure.

## Owned Artifacts
- `bundle://inputs/00-original-request.md`
- `bundle://inputs/01-source-artifacts.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://traceability/01-requirement-traceability.md`
