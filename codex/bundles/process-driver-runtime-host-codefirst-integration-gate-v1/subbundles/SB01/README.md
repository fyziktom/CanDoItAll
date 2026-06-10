# SB01: Code-first baseline and ratio guard

## Status
Prepared.

## Objective
Measure the real code-vs-bundle ratio, establish base SHA, and fail if execution starts by adding large proof scaffolding instead of code.

## Exact source references
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
This subbundle must create/refresh a small source inventory and code-first guard test. It must not create a large proof tree.

## Required implementation style
This is a large coherent implementation slice. Do not split it into tiny proof-only edits. Prefer real source/test changes over more bundle files.

## Do not do
- Do not create a generic effectful driver host.
- Do not add reflection discovery or fallback selector.
- Do not add driver self-registration.
- Do not mutate process state through drivers.
- Do not put domain-specific driver terms into Process Core.
- Do not add large proof boilerplate.

## Acceptance checklist
- Real production/test code changed for this subbundle.
- No execution-capable side effects added.
- Existing process runtime tests remain green.
- New behavior is covered by source-backed tests.
- Critical proof manifest includes changed-file hashes and command transcripts.

## Proof required
- Source assertions.
- Focused tests for the changed behavior.
- Anti-stub scan.
- Boundary scan for Core dependency drift, fallback selector, reflection discovery, mutation APIs, and bundle-path coupling.

## Browser validation logging
N/A unless this subbundle changes Razor/UI routes/components. If it changes UI, use large desktop only and record route, viewport, assertions and screenshot paths.

## Progression gate
Downstream subbundles cannot proceed until this subbundle has real source/test changes and passing focused validation. If this subbundle produces more bundle/proof changes than source/test changes, reopen it.

## Suggested agent prompt
Implement SB01 as a code-first production/test change. Keep proof concise. Preserve Process Core genericity and keep execution-capable drivers blocked.
