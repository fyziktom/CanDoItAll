# SB07: A2A v1 And Handoff Regression

## Status

- Completed

## Objective

Prove or explicitly guard A2A v1 and handoff behavior after the MAF 1.6 package upgrade.

## Covered Inputs

- RQ03: prove A2A v1 and handoff behavior.

## Prerequisites

- SB02 adoption matrix must classify A2A v1.

## Exact Source References

- repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/A2ARemoteAgentToolFactory.cs
- repo://src/CanDoItAll.AgentFramework.Hosting/AgentA2AHostCardFactory.cs
- repo://tests/CanDoItAll.Tests.Integration/MafAgentRuntimeHandoffTests.cs

## Deliverables

- Compile-level and smoke-level A2A v1 proof or an explicit readiness guard.
- Handoff regression proof that role mutation fixes do not break existing handoff workflows.

## Dependency Impact

- SB09 adapter boundary and SB18 final red-team depend on A2A/handoff classification.

## Validation Depth

- Critical semantic proof must include a local handoff smoke and A2A source/compile proof.

## Implementation Steps

- Audit A2A v1 package usage and host card mapping.
- Add smoke tests for local handoff and configured A2A bridge behavior.
- Guard runtime surfaces that cannot be locally tested.
- Update `proof/SB07`.

## Do Not Do

- Do not assume remote A2A availability without configuration proof.
- Do not keep obsolete handoff workarounds if MAF 1.6 changes make them harmful.

## Acceptance Checklist

- A2A v1 compile proof exists.
- Local handoff smoke proof exists.
- Unsupported remote behavior is guarded with diagnostics.

## Proof Required

- Failing-first/adversarial transcript.
- Passing handoff/A2A test transcript.
- Source assertions, anti-stub audit, and hashes.

## Browser Validation Logging

- N/A - no browser-visible behavior in this subbundle.

## Progression Gate

- SB09 may close only after A2A/handoff proof or guard is recorded.

## Suggested Agent Prompt

Prove MAF 1.6 A2A v1 and handoff behavior with source, compile, smoke, and guard evidence.

## Closure Proof

- bundle://proof/SB07/manifest.md
- bundle://proof/SB07/semantic-invariants.md

