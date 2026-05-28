# SB11: 11-api-openapi-and-process-tools-parity

## Goal

Keep HTTP API, process tools, OpenAPI, and runtime models aligned.

## Required work

- Audit API DTOs/read models against current runtime: artifact statuses, validation status, failure ownership, output roots, manager chat, live-run profiles.
- Update OpenAPI examples and API skill examples.
- Add API integration tests for new/changed fields.
- Ensure enum serialization guidance is current.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: runtime / API / UI / docs / template / test-harness / MAF.
- Note whether this closes previous proof debt.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB11` are updated and the next dependent workstream can rely on it.
