# SB04: Host status and operator API

## Status
- Completed

## Objective
Expose runtime host status through manager facade and a stable API/readback DTO with correlation, readiness, lane status, audit store kind, no-mutation flags.

## Covered Inputs
- inputs/00-original-request.md
- requirements/01-normalized-requirements.md
- traceability/01-requirement-traceability.md

## Prerequisites
- SB03 closure gate is completed or explicitly blocked before this subbundle starts.

## Exact Source References
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHost.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHostModels.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHostOptions.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHostStatus.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationAuditStore.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagerReadOnlyVerificationCommandService.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationJobRunner.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDryRunExecutionHost.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExecutionCapableDriverFutureGate.cs
- repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs
- repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs

## Scope
- If UI is not changed, API/component proof must still show operator-visible surface; otherwise add large-screen UI proof.

## Dependency Impact
- This subbundle gates the next dependency-map successor and must record closure proof before downstream work starts.

## Validation Depth
- Critical: require source-backed tests, source assertions, anti-stub scan, boundary scan, proof manifest, and semantic invariant contract.

## Implementation Steps
- Verify exact source references and nearby tests.
- Implement the smallest code-first production/test change for this subbundle.
- Run focused validation and record artifact-backed proof before closure.

## Required implementation style
This is a large coherent implementation slice. Do not split it into tiny proof-only edits. Prefer real source/test changes over more bundle files.

## Do Not Do
- Do not create a generic effectful driver host.
- Do not add reflection discovery or fallback selector.
- Do not add driver self-registration.
- Do not mutate process state through drivers.
- Do not put domain-specific driver terms into Process Core.
- Do not add large proof boilerplate.

## Acceptance Checklist
- Real production/test code changed for this subbundle.
- No execution-capable side effects added.
- Existing process runtime tests remain green.
- New behavior is covered by source-backed tests.
- Critical proof manifest includes changed-file hashes and command transcripts.

## Proof Required
- Source assertions.
- Focused tests for the changed behavior.
- Anti-stub scan.
- Boundary scan for Core dependency drift, fallback selector, reflection discovery, mutation APIs, and bundle-path coupling.

## Browser Validation Logging
- N/A unless this subbundle changes Razor/UI routes/components. If it changes UI, use large desktop only and record route, viewport, assertions and screenshot paths.

## Progression Gate
- Downstream subbundles cannot proceed until this subbundle has real source/test changes and passing focused validation. If this subbundle produces more bundle/proof changes than source/test changes, reopen it.

## Suggested Agent Prompt
Implement SB04 as a code-first production/test change. Keep proof concise. Preserve Process Core genericity and keep execution-capable drivers blocked.
