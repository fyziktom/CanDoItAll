# SB07 - Recovery Continuation

## Status


- Completed

## Objective

Make recovery continuation work consistently for direct agent, workflow-backed, subprocess-backed, and manager-recovered artifacts.

## Covered Inputs

- RQ07
- VF05
- N001
- N003

## Prerequisites

- SB01-SB06 closure gates passed.
- Explicit output mapping and projection identity are available for recovered artifacts.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ProviderRecovery.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryDirective.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeProgressionPlanner.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeOperatorReadModelTests.cs

## Scope

- Manager recovery support for workflow-backed completed steps when source evidence exists but process artifacts are missing.
- Recovered artifact lineage carried into finalizer context.
- Artifact-only recovery path that avoids broad implementation reruns.
- Typed recovery decision records or metadata.

## Dependency Impact

- Critical subbundle.
- SB08 audits recovered lineage and SB09 promotes typed recovery options.

## Validation Depth

- Failing-first or red-team test transcript.
- Focused unit/integration tests named in the proof manifest.
- Source assertions against production code paths.
- Anti-stub audit.
- Changed-file SHA-256 hashes.
- Full build and PostgreSQL-only audit before final closure.

## Implementation Steps

- Add failing tests for workflow-completed/missing-process-artifact recovery.
- Thread recovery lineage through finalizer and artifact projection.
- Ensure recovery rejects invented source evidence.
- Update proof artifacts after focused recovery tests pass.

## Do Not Do

- Do not add SQLite support or SQLite-specific runtime/migration paths.
- Do not confuse workflow executor state with process-owned lifecycle, finalization, or governance.
- Do not replace runtime enforcement with prompt-only wording.
- Do not hard-code software-delivery-only behavior into generic process services.
- Do not mark this subbundle complete without artifact-backed proof under proof/SB07/.

## Acceptance Checklist

- [ ] Workflow completed but missing mapped process artifact can be recovered by manager.
- [ ] Recovery cannot invent missing source evidence.
- [ ] Recovery artifact validates against recovered-for execution/run.
- [ ] Old shallow behavior fails or is red-team documented.
- [ ] New production behavior passes through runtime code, not prompt text.
- [ ] Proof manifest and semantic invariants cite existing artifacts.
- [ ] No SQLite runtime reintroduction.

## Proof Required

Update:

- proof/SB07/manifest.md
- proof/SB07/semantic-invariants.md
- proof/SB07/transcripts/failing-first.txt
- proof/SB07/transcripts/passing.txt
- proof/SB07/transcripts/source-assertions.txt
- proof/SB07/transcripts/anti-stub-audit.txt
- proof/SB07/transcripts/changed-file-hashes.txt

## Browser Validation Logging

- N/A unless recovery controls become browser-visible.
- Add a row in reviews/01-execution-report.md while validation is fresh.

## Progression Gate

- Entry gate must confirm prerequisites and exact source references still match the repo.
- Closure gate must confirm tests, source assertions, anti-stub audit, changed-file hashes, and proof manifest are complete.
- Downstream subbundles must re-check this gate if later observations weaken the proof.

## Suggested Agent Prompt

Implement SB07 from codex/bundles/processes-hardening-followup-runtime-governance-v5. Preserve generic process semantics, keep Processes above Workflows, and capture artifact-backed proof before moving on.
