# SB07: 07-a2a-v1-remote-and-local-handoff-proof

## Goal

Strengthen A2A v1/handoff proof.

## Required work

- Keep deterministic local handoff smoke.
- Add remote/hosted A2A capability proof if feasible; otherwise guard the path with explicit readiness diagnostics.
- Verify handoff roles/messages are not mutated unexpectedly.
- Verify human-in-the-loop/A2A input-request content behavior if used.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB07` are updated and downstream subbundles can rely on it.

## Status

- Completed

## Objective

Confirm the A2A proof boundary remains in the host and tool factory code.

## Covered Inputs

- RQ06 A2A and handoff behavior.

## Prerequisites

- MAF/A2A package symbols are reflected in SB02.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Hosting/AgentA2AHostCardFactory.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/A2ARemoteAgentToolFactory.cs`

## Deliverables

- A2A proof boundary documented without runtime changes.

## Dependency Impact

- SB18 uses this as a release-readiness proof boundary.

## Validation Depth

- Reflection proof and source inspection.

## Implementation Steps

- Inspect A2A host and remote tool code.
- Avoid unrelated handoff rewrites.

## Do Not Do

- Do not broaden A2A behavior in a process artifact bundle.

## Acceptance Checklist

- A2A source and package symbols are cited.

## Proof Required

- SB02 reflection proof and final report row.

## Browser Validation Logging

- No browser route is affected.

## Progression Gate

- A2A remains outside the artifact validation change surface.

## Suggested Agent Prompt

Verify A2A boundaries from real source and package symbols before closing release readiness.
