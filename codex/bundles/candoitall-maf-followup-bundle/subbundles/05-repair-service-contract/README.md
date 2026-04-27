# Subbundle 05 — Repair service contract and semantics

## Problem

The current `DefaultAgentOutputRepairService` is a conservative JSON extraction repair. It can recover a single balanced JSON object from wrapped prose, but it does not semantically repair missing fields, invalid enum values, inconsistent branch outcomes, or business-rule errors.

This is not necessarily bad. It is safer than a loose LLM repair loop. But the name, docs, tests, and architecture should make the behavior explicit.

## Required change

Choose one of two paths.

### Path A — Keep conservative extraction repair

Rename or document it as JSON extraction repair, for example:

- `JsonObjectExtractionAgentOutputRepairService`
- registered as the default `IAgentOutputRepairService`

Ensure docs say:

- it only extracts a candidate JSON object from wrapped text;
- it never invents missing content;
- it always revalidates;
- semantic repair is intentionally not enabled by default.

### Path B — Add semantic repair as opt-in

Create a separate service, for example:

- `SemanticAgentOutputRepairService`

It must:

- be opt-in via configuration/policy;
- use the same structured output contract;
- include validation errors and schema description;
- perform bounded attempts only;
- never bypass finalizer-required failures;
- revalidate before accepting output;
- emit telemetry/logs without leaking raw secrets.

## Tests

Add repair tests:

- wrapped prose containing one valid JSON object is extracted and accepted;
- two JSON objects fail or choose deterministic documented behavior;
- no JSON object fails;
- malformed JSON fails;
- JSON with missing required business fields fails validation after extraction;
- repair attempt count is clamped to `ExecutionInvocationMetadata.MaxRepairAttempts`;
- required finalizer missing is not repaired via assistant text.

## Status

Completed. Proof is recorded in `../../reviews/01-execution-report.md`.

## Requirements Owned

R08.

## Prerequisites

Subbundle 04 should be completed or the relevant output-contract tests must be updated in this pass.

## Dependency Impact

Supports truthful verification documentation and prevents overstating repair semantics.

## Validation Depth

Unit tests around the default repair service and execution repair attempt policy, plus documentation updates that describe conservative extraction accurately.

## Progression Gate

Downstream work may continue only after the default repair behavior is either renamed or documented as conservative JSON extraction and tests prove it does not perform semantic repair.
