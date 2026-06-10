# SB02: Template catalog and multi-team development inventory/repair

## Status
- Completed

## Objective
Template catalog and multi-team development inventory/repair.

## Covered Inputs
- inputs/00-original-request.md
- requirements/01-normalized-requirements.md
- analysis/01-real-code-review.md
- analysis/04-gap-analysis.md

## Prerequisites
- SB01 closure gate passes.
- Baseline template/runtime-host inventory paths are current.
- Code-first ratio guard remains enforceable.

## Exact Source References
- repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplateCatalogService.cs
- repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackScenarios.cs
- repo://tests/CanDoItAll.Tests.Integration/ApplicationStartupIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs

## Scope

Inventory actual process template keys and categories. Confirm whether multi-team development exists, was renamed, or was lost during refactoring.

Deliverables:
- source-backed template catalog inventory test;
- exact keys for software development, Blazor/.NET app, business analysis, and multi-team development if present;
- if multi-team is missing, either restore the template/catalog registration or create a blocking test + explicit follow-up artifact;
- no silent skip.


## Dependency Impact
- This subbundle gates SB03 and SB04 because template execution proof is meaningless if catalog keys are stale or missing.
- If validation fails, downstream work is not trustworthy.

## Validation Depth
- Critical. Requires focused tests and source assertions.
- Semantic adequacy proof must reject a shallow catalog test that only checks non-empty template lists.
- Browser proof is required only for UI-visible changes or route proof.

## Implementation Steps
- Inventory actual template keys exposed by source and API surfaces.
- Confirm software-development, Blazor/.NET, business-analysis, and multi-team development availability.
- Restore, map, or explicitly block missing multi-team development without silent skip.
- Add focused tests proving exact keys and launch-surface availability.

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
- `proof/SB02/manifest.md` with changed-file hashes, transcript paths, source assertions, and anti-stub audit.
- `proof/SB02/semantic-invariants.md` tying `REQ-002` to exact-template inventory proof.
- Short execution-report row.
- For critical new production records/events, include a production behavior artifact matrix.

## Browser Validation Logging
- N/A unless UI routes/components are touched or route proof is required. If needed, use large desktop viewport only and record route, viewport, assertions, screenshot paths, and result.

## Progression Gate
- Proceed only after exact template inventory is source-backed and multi-team development is present, mapped, or explicitly blocked.
- Reopen if proof is report-only, bundle-heavy, or source/test changes are too small.

## Suggested Agent Prompt
Implement SB02 as a coherent code-first slice. Prefer larger source/test changes over proof scaffolding. Keep runtime-host execution future-gated and preserve generic Process Core boundaries.
