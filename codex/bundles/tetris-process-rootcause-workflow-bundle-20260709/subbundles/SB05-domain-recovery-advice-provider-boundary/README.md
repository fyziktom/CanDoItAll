# SB05 Domain Recovery Advice Provider Boundary

## Status

- `Completed`

## Objective

Move .NET/software-delivery recovery advice out of generic process application code and behind provider/template metadata.

## Covered Inputs

- GPTPro RC5.
- Domain boundary rules.
- Requirement R06.

## Prerequisites

- SB04 route mechanics exist or provider API can be designed against stable issue/trace contracts.
- Architecture checkpoint after SB04 passes.
- Forbidden-token baseline is captured.

## Exact Source References

- `bundle://07-domain-boundary-rules.md`
- `bundle://codex-tasks/06-domain-recovery-advice-provider.md`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessStepRecoveryInstructionBuilder.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeOperatorApplicationService.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/Services/WorkbenchModuleServiceCollectionExtensions.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessStepRecoveryInstructionBuilderTests.cs`

## Deliverables

- `IProcessRecoveryAdviceProvider` or equivalent provider strategy.
- Generic recovery provider for common retry/blocker guidance.
- DotNet/software-delivery provider registered from the appropriate module.
- Generic builder delegates provider-specific advice.
- Tests proving generic builder has no hardcoded `.NET`, Blazor, QA, or software-delivery branch constants.
- Existing recovery guidance preserved or improved when domain provider is available.

## Dependency Impact

- SB07 prompt/template hardening depends on provider boundary so prompts do not mask missing runtime behavior.
- SB11 architecture closure depends on forbidden-token tests.

## Validation Depth

- Critical foundation.
- Requires architecture tests and provider behavior tests.

## Implementation Steps

1. Define recovery advice context with diagnostics, assignment metadata, launch variables, branch output, applicable rules, and gate trace.
2. Add provider interface in generic process application/abstractions without Workbench dependency.
3. Move generic advice into generic provider.
4. Move .NET/software-delivery guidance into Workbench or domain-specific module.
5. Register provider implementations explicitly.
6. Rewrite tests to assert provider selection and output.
7. Add forbidden-token architecture test for generic process application/runtime files.

## C# Architecture Impact

This phase repairs the domain leak called out by the user and GPTPro.

## Boundary Ownership

- Generic builder orchestrates providers.
- Workbench provider owns .NET tool names and software-delivery branch names.

## Dependency Direction

- Generic application can reference provider interface.
- Workbench implementation references generic interface; generic process code must not reference Workbench.

## Pattern Decision

- Provider strategy.
- Rejected: constants moved to another generic helper.

## Testability Contract

- Generic builder tests run without Workbench provider.
- Workbench provider tests use software-delivery fixture data.

## Partial Class Policy

- No new partial classes.

## Architecture Proof Required

- Forbidden-token scan for generic process application/runtime.
- Provider selection tests.
- Source assertion that old hardcoded constants were removed from `ProcessStepRecoveryInstructionBuilder`.

## Do Not Do

- Do not weaken recovery instructions by removing actionable state.
- Do not use service location inside core behavior.
- Do not hardcode branch names in generic provider.

## Acceptance Checklist

- Generic builder has no `workspace_dotnet_*`, `qa-validation`, `quality-accepted`, `repair-required`, or `repair-escalation` literals.
- DotNet provider emits the needed guidance when domain context applies.
- Generic provider handles non-domain diagnostics.
- Tests prove both paths.

## Proof Required

- `bundle://proof/SB05/manifest.md` after execution.
- `bundle://proof/SB05/semantic-invariants.md` after execution.
- Forbidden-token failing-first and passing transcripts.
- Provider test transcripts.
- Source assertions and anti-stub audit.

## Browser Validation Logging

- N/A for SB05.

## Progression Gate

- SB11 architecture closure is blocked until SB05 passes provider and forbidden-token proof.

## Suggested Agent Prompt

Implement SB05 by extracting domain recovery advice into providers. Keep generic recovery orchestration, remove .NET/software-delivery constants from generic application code, and prove the boundary with tests.
