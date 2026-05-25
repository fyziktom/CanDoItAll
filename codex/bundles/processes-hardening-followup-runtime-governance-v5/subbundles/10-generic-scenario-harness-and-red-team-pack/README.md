# SB10 - Generic Scenario Harness and Red-Team Pack

## Status


- Completed

## Objective

Build broad validation scenarios across software and non-software processes.

## Covered Inputs

- RQ10
- N001
- N002
- N003
- N004
- N005

## Prerequisites

- SB01-SB09 closure gates passed.
- All runtime governance foundations have artifact-backed proof.

## Exact Source References

- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessMockAgentRuntimeIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs
- repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs

## Scope

- Generic red-team scenario tests for purchasing, HR onboarding, incident response, legal review, manufacturing QA, business planning, architecture-only, implementation, QA, repair, and release gate flows.
- Scenario assertions for process completion, blocking, branch outcomes, and artifacts.
- Final fake-proof resistance audit across all critical subbundles.

## Dependency Impact

- Critical subbundle.
- This is the final closure gate. No downstream subbundle may hide unresolved governance gaps.

## Validation Depth

- Failing-first or red-team test transcript.
- Focused unit/integration tests named in the proof manifest.
- Source assertions against production code paths.
- Anti-stub audit.
- Changed-file SHA-256 hashes.
- Full build and PostgreSQL-only audit before final closure.

## Implementation Steps

- Add red-team fixtures proving software and non-software processes share the same governance model.
- Run focused policy, linter, dispatch, and scenario tests.
- Run full build and PostgreSQL-only audit.
- Update proof artifacts, execution report, raw-note closure, and final validation status.

## Do Not Do

- Do not add SQLite support or SQLite-specific runtime/migration paths.
- Do not confuse workflow executor state with process-owned lifecycle, finalization, or governance.
- Do not replace runtime enforcement with prompt-only wording.
- Do not hard-code software-delivery-only behavior into generic process services.
- Do not mark this subbundle complete without artifact-backed proof under proof/SB10/.

## Acceptance Checklist

- [ ] Architecture-only Blazor step cannot implement.
- [ ] Business plan report can write external artifact destination without product mutation.
- [ ] Legal review can no-go without browser/build evidence.
- [ ] Manufacturing QA can route defect to repair branch.
- [ ] Old shallow behavior fails or is red-team documented.
- [ ] New production behavior passes through runtime code, not prompt text.
- [ ] Proof manifest and semantic invariants cite existing artifacts.
- [ ] No SQLite runtime reintroduction.

## Proof Required

Update:

- proof/SB10/manifest.md
- proof/SB10/semantic-invariants.md
- proof/SB10/transcripts/failing-first.txt
- proof/SB10/transcripts/passing.txt
- proof/SB10/transcripts/source-assertions.txt
- proof/SB10/transcripts/anti-stub-audit.txt
- proof/SB10/transcripts/changed-file-hashes.txt

## Browser Validation Logging

- Required only for browser-visible scenario or process editor/dashboard validation paths.
- Add a row in reviews/01-execution-report.md while validation is fresh.

## Progression Gate

- Entry gate must confirm prerequisites and exact source references still match the repo.
- Closure gate must confirm tests, source assertions, anti-stub audit, changed-file hashes, and proof manifest are complete.
- Downstream subbundles must re-check this gate if later observations weaken the proof.

## Suggested Agent Prompt

Implement SB10 from codex/bundles/processes-hardening-followup-runtime-governance-v5. Preserve generic process semantics, keep Processes above Workflows, and capture artifact-backed proof before moving on.
