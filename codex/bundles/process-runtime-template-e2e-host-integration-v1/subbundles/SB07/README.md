# SB07: Runtime-host contracts and capability hardening

## Status
- Completed

## Objective
Runtime-host contracts and capability hardening.

## Covered Inputs
- inputs/00-original-request.md
- requirements/01-normalized-requirements.md
- analysis/01-real-code-review.md
- analysis/04-gap-analysis.md

## Prerequisites
- SB06 closure gate passes.
- Runtime-host verification job lifecycle shape is known.
- No execution-capable driver approval exists in this bundle.

## Exact Source References
- repo://src/CanDoItAll.Processes.Contracts/Runtime/ProcessRuntimeHostContractModels.cs
- repo://src/CanDoItAll.Processes.Contracts/Runtime/ProcessRuntimeEnums.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationHostCapabilityCatalog.cs
- repo://tests/CanDoItAll.Tests.Unit/ProcessContractDriftScannerTests.cs
- repo://tests/CanDoItAll.Tests.Unit/AgentRuntimeHardeningStaticRegressionTests.cs

## Scope

Harden generic runtime-host contracts and capability descriptors.

Deliverables:
- split oversized files if needed;
- ensure contracts remain generic and stable;
- add capability provider boundary without reflection/self-registration;
- add contract compatibility/snapshot tests;
- forbid domain leakage into Process Core.


## Dependency Impact
- This subbundle gates SB08 because final release closure depends on stable contracts, capability descriptors, and boundary scans.
- If validation fails, downstream work is not trustworthy.

## Validation Depth
- Critical. Requires focused tests and source assertions.
- Semantic adequacy proof must reject reflection discovery, fallback selectors, driver self-registration, or domain leakage into Process Core.
- Browser proof is required only for UI-visible changes or route proof.

## Implementation Steps
- Split oversized runtime-host or capability files only if needed.
- Harden generic contract DTOs and compatibility/snapshot tests.
- Preserve explicit static capability provider boundaries.
- Add boundary tests for Process Core leakage, reflection discovery, fallback selector, and driver self-registration.

## Do Not Do
- Do not create execution-capable drivers.
- Do not add reflection discovery, fallback selector, driver self-registration, or generic effectful runtime host.
- Do not mutate process state through drivers.
- Do not add domain-specific concepts into Process Core.
- Do not create large proof scaffolding or repeated boilerplate during execution.

## Acceptance Checklist
- Real source/test code changed unless this is an explicit inventory blocker.
- No effectful driver execution added.
- Process Core remains generic.
- Focused tests prove behavior.
- Source scans pass.
- Code-first ratio is not weakened.

## Proof Required
- Focused test transcript.
- Source scan transcript.
- `proof/SB07/manifest.md` with changed-file hashes, transcript paths, source assertions, and anti-stub audit.
- `proof/SB07/semantic-invariants.md` tying `REQ-007` to contract and capability-boundary proof.
- Short execution-report row.
- For critical new production records/events, include a production behavior artifact matrix.

## Browser Validation Logging
- N/A unless UI routes/components are touched or route proof is required. If needed, use large desktop viewport only and record route, viewport, assertions, screenshot paths, and result.

## Progression Gate
- Proceed only after contract snapshot and boundary tests pass and no forbidden discovery/registration path is present.
- Reopen if proof is report-only, bundle-heavy, or source/test changes are too small.

## Suggested Agent Prompt
Implement SB07 as a coherent code-first slice. Prefer larger source/test changes over proof scaffolding. Keep runtime-host execution future-gated and preserve generic Process Core boundaries.
