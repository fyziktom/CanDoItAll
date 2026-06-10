# SB046 - Process Core genericity and contract governance: inventory and source changes

## Status
- Status: `Completed`

## Objective
Advance `Process Core genericity and contract governance` without weakening the restored process runtime, generic Process Core, or verification-only host boundary.

## Covered Inputs
- User request to inspect real code and real tests.
- Current live OpenAI process-run proof.
- Current verification host beta and remaining path toward generic runtime host.

## Prerequisites
- Previous subbundle gate has passed.
- Current branch is `maf-processes-refactor`.
- No transient `codex/bundles/<name>` references in `src` or `tests`.

## Exact Source References
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHost.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHostModels.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationAuditStore.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagerReadOnlyVerificationCommandService.cs
- repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs
- repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs

## Deliverables
- Source changes or proof artifacts for `Process Core genericity and contract governance`.
- Focused tests and source scans matching the subbundle objective.
- Updated docs/runbook only when the code behavior is source-backed.

## Dependency Impact
- This subbundle is part of P16. If it is wrong, all downstream subbundles in later phases must be reopened because runtime-host governance and safety assumptions may be invalid.

## Validation Depth
- Build or focused test proof as relevant.
- Source assertions for exact changed files.
- Anti-stub and forbidden-runtime scan.
- If critical: semantic adequacy manifest, negative proof, positive proof, and production behavior artifact matrix.

## Implementation Steps
1. Re-read the exact source references and identify the minimal production/test changes.
2. Add failing-first or source assertion proof before implementation when possible.
3. Implement the behavior without broadening driver authority.
4. Run focused tests and required scans.
5. Record proof under `proof/SB046/`.

## Scope Exceptions
Execution-capable driver runtime remains out of scope. Any needed execution-capable capability must be recorded as future-gated, not implemented.

## Do Not Do
- Do not add execution-capable drivers.
- Do not add shell/package restore/Graph/CRM/workspace/storage/process mutation through drivers.
- Do not add fallback lane selection, reflection discovery, generic `object` dispatch, or implicit driver DI discovery.
- Do not weaken Process Core dependency boundaries.
- Do not claim skipped live tests as live-provider proof.

## Acceptance Checklist
- [ ] Source changes are bounded to the phase objective.
- [ ] Tests prove behavior, not just non-empty output.
- [ ] No process mutation is introduced through verification host/drivers.
- [ ] No Core dependency drift.
- [ ] Proof manifest is artifact-backed.

## Proof Required
- `proof/SB046/manifest.md`
- `proof/SB046/semantic-invariants.md` if this is a critical gate or introduces production behavior.
- Command transcripts under `proof/SB046/transcripts/`.
- Changed-file hashes.

## Browser Validation Logging
- N/A unless this subbundle changes operator UI/readback. If UI changes, use `/processes` or project-scoped run detail at 1900x1200 large desktop only and capture screenshots plus API readback.

## Progression Gate
- Proceed only after focused proof and source scans pass. If the subbundle changes a shared contract, rerun the nearest critical gate.

## Suggested Agent Prompt
Implement SB046 for `Process Core genericity and contract governance`. Use the exact source references above. Preserve generic Process Core and verification-only host constraints. Record source-backed proof before claiming completion.