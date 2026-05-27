# SB09: Refactor Checkpoint A - MAF 1.6 Adapter Boundaries

## Status

- Completed

## Objective

Refactor or verify MAF adapter seams after feature adoption decisions.

## Covered Inputs

- RQ04: keep adapter boundaries explicit and keep Processes independent from MAF internals.

## Prerequisites

- SB03 through SB08 must be complete or explicitly blocked with no dependency on the blocked behavior.

## Exact Source References

- repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs
- repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs
- repo://src/CanDoItAll.AgentFramework.Maf/README.md

## Deliverables

- Boundary audit for MAF 1.6 compatibility wrappers and adapter-owned types.
- All MAF-focused tests before process-runtime work continues.

## Dependency Impact

- SB10 through SB18 must trust this adapter boundary before validating process runtime behavior.

## Validation Depth

- Critical semantic proof must reject direct MAF type leakage into process domain/runtime models.

## Implementation Steps

- Audit adapter boundaries and MAF type usage.
- Extract or tighten compatibility wrappers only where they remove real duplication or leakage.
- Run MAF-focused tests.
- Update `proof/SB09`.

## Do Not Do

- Do not introduce abstractions with no boundary or test value.
- Do not move process governance into MAF-specific code.

## Acceptance Checklist

- MAF feature decisions are documented as adopted/deferred.
- Processes remain independent from MAF internals.
- MAF-focused tests pass.

## Proof Required

- Source assertion transcript.
- Passing MAF test transcript.
- Anti-stub audit and hashes.

## Browser Validation Logging

- N/A - no browser-visible behavior in this subbundle.

## Progression Gate

- Process-runtime subbundles may start only after the adapter boundary closure gate passes.

## Suggested Agent Prompt

Validate and tighten the MAF adapter boundary after feature adoption decisions, keeping Processes independent from MAF internals.

## Closure Proof

- bundle://proof/SB09/manifest.md
- bundle://proof/SB09/semantic-invariants.md

