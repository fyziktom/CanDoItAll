# SB04: Business-analysis process execution E2E

## Status
- Completed

## Objective
Business-analysis process execution E2E.

## Covered Inputs
- inputs/00-original-request.md
- requirements/01-normalized-requirements.md
- analysis/01-real-code-review.md
- analysis/04-gap-analysis.md

## Prerequisites
- SB03 closure gate passes.
- Business-analysis template key is inventoried by SB02.
- Software-specific artifact proof cannot be reused as generic business proof.

## Exact Source References
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationPayloadBuilder.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationBatchOrchestrator.cs
- repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Unit/ProcessDriverBusinessAnalysisAlphaTests.cs

## Scope

Execute a non-software business-analysis template and prove genericity.

Deliverables:
- run start through process services;
- non-software roles/artifact expectations;
- artifacts: analysis/deliverable/evidence/decision as appropriate;
- manager readback and artifact ledger;
- negative test proving software/.NET artifacts cannot satisfy this scenario.


## Dependency Impact
- This subbundle gates SB05 because operator readback must work for both software and non-software run contexts.
- If validation fails, downstream work is not trustworthy.

## Validation Depth
- Critical. Requires focused tests and source assertions.
- Semantic adequacy proof must reject software/.NET artifacts as satisfying the business-analysis scenario.
- Browser proof is required only for UI-visible changes or route proof.

## Implementation Steps
- Execute a non-software business-analysis template through process services.
- Prove non-software roles, artifact expectations, manager readback, and artifact ledger.
- Add a negative test proving software/.NET artifacts cannot satisfy business-analysis evidence.
- Keep Process Core free of business-analysis driver leakage.

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
- `proof/SB04/manifest.md` with changed-file hashes, transcript paths, source assertions, and anti-stub audit.
- `proof/SB04/semantic-invariants.md` tying `REQ-004` to non-software execution proof.
- Short execution-report row.
- For critical new production records/events, include a production behavior artifact matrix.

## Browser Validation Logging
- N/A unless UI routes/components are touched or route proof is required. If needed, use large desktop viewport only and record route, viewport, assertions, screenshot paths, and result.

## Progression Gate
- Proceed only after business-analysis execution and negative genericity proof pass.
- Reopen if proof is report-only, bundle-heavy, or source/test changes are too small.

## Suggested Agent Prompt
Implement SB04 as a coherent code-first slice. Prefer larger source/test changes over proof scaffolding. Keep runtime-host execution future-gated and preserve generic Process Core boundaries.
