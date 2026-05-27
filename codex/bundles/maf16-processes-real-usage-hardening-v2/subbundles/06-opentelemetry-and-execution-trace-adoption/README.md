# SB06: OpenTelemetry And Execution Trace Adoption

## Status

- Completed

## Objective

Handle MAF 1.6 OpenTelemetry wrapper changes deliberately and preserve CanDoItAll trace correlation.

## Covered Inputs

- RQ03: adopt or guard OpenTelemetry compatibility.

## Prerequisites

- SB02 adoption matrix must classify OpenTelemetry wrapper behavior.

## Exact Source References

- repo://src/CanDoItAll.AgentFramework.Core/Telemetry/AgentFrameworkTelemetry.cs
- repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs
- repo://tests/CanDoItAll.Tests.Integration/MafAgentRuntimeTests.cs

## Deliverables

- Source-backed decision on MAF telemetry wrapping.
- Tests or source assertions proving no double wrapping and no missing correlation across tool receipts, execution logs, context traces, finalizer invocations, and process journal events.

## Dependency Impact

- SB17 observability and runbook proof depend on trace correlation.

## Validation Depth

- Critical semantic proof must reject a wrapper-only change that loses CanDoItAll execution correlation.

## Implementation Steps

- Audit telemetry wrapper usage.
- Add guards/tests/source assertions for double wrapping and missing spans.
- Update trace correlation proof.
- Update `proof/SB06`.

## Do Not Do

- Do not wrap chat clients twice.
- Do not remove CanDoItAll telemetry tags to satisfy MAF defaults.

## Acceptance Checklist

- Telemetry adoption/defer decision is explicit.
- Correlation proof covers execution and process paths.
- Proof artifacts are updated.

## Proof Required

- Failing-first/adversarial transcript.
- Passing transcript or source assertion where runtime telemetry test is impractical.
- Anti-stub audit and hashes.

## Browser Validation Logging

- N/A - no browser-visible behavior in this subbundle.

## Progression Gate

- SB17 may use telemetry proof only after SB06 closes.

## Suggested Agent Prompt

Audit MAF 1.6 telemetry wrapper impact and prove CanDoItAll execution/process trace correlation is preserved without double wrapping.

## Closure Proof

- bundle://proof/SB06/manifest.md
- bundle://proof/SB06/semantic-invariants.md

