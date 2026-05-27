# SB03: 03-maf16-feature-adoption-from-matrix-to-runtime

## Goal

Ensure each Adopted MAF feature has production runtime use.

## Required work

- For each feature marked Adopted, point to production code and a test.
- For each feature marked Deferred, ensure a reason and safe fallback are documented.
- Do not mark context-provider fallback as IChatMessageInjector adoption unless the injector symbol is actually used.
- Add tests where claims are currently source-only.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB03` are updated and downstream subbundles can rely on it.

## Status

- Completed

## Objective

Tie MAF adoption claims to runtime code paths or explicit safe deferrals.

## Covered Inputs

- RQ03 production/runtime proof.

## Prerequisites

- SB02 reflection proof is available.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Context.cs`

## Deliverables

- Adoption boundary captured in the execution report.

## Dependency Impact

- SB04 and SB09 depend on the feature boundary.

## Validation Depth

- Source inspection plus reflection proof reuse.

## Implementation Steps

- Review adopted MAF feature paths.
- Avoid adding wrappers without a runtime need.

## Do Not Do

- Do not add a facade just to make the matrix look fuller.

## Acceptance Checklist

- Each adoption claim is backed by source or marked as deferred.

## Proof Required

- Final report rows and SB02 reflection proof.

## Browser Validation Logging

- No browser route is affected.

## Progression Gate

- Runtime claims must not exceed proven package symbols.

## Suggested Agent Prompt

Map each MAF 1.6 adoption claim to concrete source or a documented deferral.
