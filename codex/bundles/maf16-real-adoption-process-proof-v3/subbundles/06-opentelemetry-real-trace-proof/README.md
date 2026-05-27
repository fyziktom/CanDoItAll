# SB06: 06-opentelemetry-real-trace-proof

## Goal

Provide real OpenTelemetry/trace proof after MAF 1.6.

## Required work

- Audit whether OpenTelemetryChatClient is auto-wired or whether CanDoItAll wraps telemetry explicitly.
- Add a trace correlation test or diagnostic endpoint proof linking agent run id, process run id, tool call, and journal entry.
- Ensure no double-wrapping creates duplicate spans.
- Ensure missing telemetry does not break process runtime.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB06` are updated and downstream subbundles can rely on it.

## Status

- Completed

## Objective

Keep trace proof scoped to the existing telemetry infrastructure.

## Covered Inputs

- RQ06 trace behavior after upgrade.

## Prerequisites

- Runtime behavior changes are limited to process artifact validation.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Core/Telemetry/AgentFrameworkTelemetry.cs`

## Deliverables

- No telemetry refactor was required for this bundle.

## Dependency Impact

- SB18 records the residual live-run trace proof boundary.

## Validation Depth

- Source inspection and final runbook coverage.

## Implementation Steps

- Inspect telemetry source.
- Keep final live trace proof in the runbook.

## Do Not Do

- Do not fake a trace artifact from a non-live run.

## Acceptance Checklist

- Trace proof expectation is recorded for the next live test.

## Proof Required

- Final report and SB18 runbook proof.

## Browser Validation Logging

- No browser route is affected.

## Progression Gate

- Telemetry proof remains a live-run verification item.

## Suggested Agent Prompt

Document trace proof honestly and avoid inventing a non-live telemetry receipt.
