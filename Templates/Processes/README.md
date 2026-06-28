# CanDoItAll process template pack

This pack is the current-architecture-aligned replacement for the original execution bundle.

## Goals
- Keep process templates file-driven and outside compiled C# code.
- Preserve shared and local authoring resources for roles, artifacts, checklists, validations, prompts, and step documents.
- Project current-module import envelopes with first-class dependencies, artifact inputs, decision roles, and branch outcomes.
- Generate process catalog Markdown, Mermaid, canonical JSON, hashes, and structure from `definition.json` at runtime; checked-in generated preview sidecars are intentionally not part of the current pack.

## Current architecture adjustments
- Added explicit process-level role usages.
- Added first-class step dependencies and artifact-input definitions.
- Added a new branching-code-review template aligned to the current baseline scenarios.
- Realigned the baseline scenario catalog to current repository expectations.
- Added live-run profiles for fresh UI-driven runs that must not pre-seed completed transitions or artifacts.
- Added corrective guidance for the remaining hardcoded authoring chrome in ProcessCanvasSurfaceFactory.
- Added typed operation contracts, target scopes, workflow/subprocess artifact mappings, and block/recovery health guidance for governed process runs.

## Governance requirements
- Every manifest template step must declare `AllowedOperations` and `OperationTargetScope`. Do not rely on prose-only phrases such as "may edit the app" to define mutation rights.
- `AllowedOperations` values must stay source-aligned with `ProcessStepOperation`: `ReadProcessContext`, `ReadProjectStructure`, `ReadUpstreamArtifacts`, `WriteManagedProcessArtifacts`, `WriteExternalArtifactDestination`, `MutateProductTarget`, `RunValidation`, `LaunchRuntime`, `CaptureRuntimeProof`, `ExecuteExternalAction`, `RecoverArtifactsOnly`, and `EscalateOrDecide`.
- Use the narrowest target scope that matches the step. Validation, review, screenshot, writeback, and escalation steps should normally be read-only or external-action controlled; product mutation belongs in implementation or repair steps.
- `OperationTargetScope` values must stay source-aligned with `ProcessStepTargetScope`: `ManagedProcessArtifactsOnly`, `ManagedOutputProduct`, `ExternalArtifactDestination`, `ExternalProductTargetReadOnly`, `ExternalProductTargetMutable`, and `ExternalActionControlled`.
- `ContractMode` should be `Strict` for templates that are ready to enforce typed operation contracts. Use compatibility mode only for explicitly transitional templates.
- Required workflow-backed artifacts must include explicit workflow output mapping fields: `WorkflowOutputId`, `WorkflowOutputName`, and `WorkflowOutputKind`.
- Required subprocess-backed artifacts must include `SubprocessChildArtifactExpectationId` so parent and child process artifacts are not matched by title/kind heuristics.
- Project-structure writeback is an external action. Mutation tools such as project-structure node or asset creation must require `ExecuteExternalAction`; read-only project-structure discovery should remain read-only.
- Workflows are role executors under Processes. A workflow assignment can execute a process step and produce mapped artifacts, but the process definition still owns dependencies, approvals, artifact expectations, recovery, and closure.
- Block/recovery behavior is typed runtime state. New transitions should supply `BlockCause` instead of depending on blocked-reason text; text inference exists only for legacy state.
- `BlockCause` values must stay source-aligned with `ProcessStepBlockCause`: `OwnOutput`, `UpstreamInput`, `RuntimeEvidence`, and `PolicyDenied`.
- `RecoveryOptions` values must stay source-aligned with `ProcessStepRecoveryOption`: `None`, `WaitForArtifactMaterialization`, `RecoverArtifactsOnly`, `RetryAgent`, `FreshAgentSession`, `ReworkContinuation`, `HumanEscalation`, `RepairImplementation`, and `RerunValidation`.
- Artifact expectation satisfaction status values must stay source-aligned with `ProcessArtifactExpectationSatisfactionStatus`: `Expected`, `Satisfied`, `AutoProjected`, `Missing`, `ProjectionFailed`, `ContentUnavailable`, `NotApplicable`, `InvalidFormat`, `InsufficientEvidence`, `StaleOrWrongRun`, `WrongProducerMode`, `PlaceholderOnly`, and `ContentHashMismatch`.
- Required artifact expectations are not satisfied by `Missing`, `ProjectionFailed`, `ContentUnavailable`, `InvalidFormat`, `InsufficientEvidence`, `StaleOrWrongRun`, `WrongProducerMode`, `PlaceholderOnly`, or `ContentHashMismatch`.
- Baseline scenarios are fixture data for governance and regression proof. Live-run profiles must declare `FreshRunPolicy`, require a fresh run, and keep `AllowsSeededTransitions` and `AllowsSeededArtifacts` false unless a future migration profile explicitly documents why replaying seeded state is safe.
- Live-run profile guidance must require current-run evidence checks before validation and project-structure writeback. Seeded artifacts, seeded transition receipts, and baseline scenario outputs are not live delivery evidence.

## Source-aligned authoring checklist

- Keep templates generic. A software-delivery template may mention product targets, but business, incident, governance, training, and review processes should use the same typed contracts without pretending every step mutates code.
- Model output ownership with required artifact expectations and, when applicable, workflow output or subprocess child mappings. Do not match parent/child evidence by title or artifact kind alone.
- Describe final delivery through managed output roots or grounded project-structure targets. External target aliases are grounding metadata until a current-run artifact records the delivery evidence.
- Use baseline scenarios only for regression and governance fixtures. Use live-run profiles for real operator runs and expose their policy through `/api/processes/templates/live-run-profiles` or `processes_template_live_run_profiles_list`.
- Keep manager, reviewer, and writeback roles explicit. Manager chat should resolve through configured or selected-run assignments before fallback scoring.
- Update `codex/skills/candoitall-api-processes/SKILL.md` and source assertions when template fields, API summaries, or runtime tool names change.

## Blazor WASM PWA live-run readiness checklist
- Start fresh UI-driven runs from `generic-blazor-wasm-pwa-app` in `seed-catalog/live-run-profiles.json` or import `blazor-app-delivery` and supply the concrete app topic in the run request.
- Keep planning and validation steps read-only. Only implementation and repair steps should use product-target mutation.
- Require artifacts for implementation summary, build/test output, browser screenshot or Playwright proof, PWA/offline validation notes, and final handoff.
- For missing current-step output, block with `OwnOutput`; for missing upstream materialization, block with `UpstreamInput`.
- Before treating a run as ready for UI validation, read run detail and confirm required artifact expectations are satisfied, projection lineage is current-run evidence, and runtime invariant diagnostics are clear.
- Before writeback, confirm the live run did not import any baseline scenario transition or artifact records.

## Folder layout
- `shared/` contains reusable roles, artifacts, checklists, validations, and prompts.
- `processes/<key>/` contains the canonical template definition, local authoring resources, and step docs.
- `toolbox/` contains role/step seeds and the proposed chrome-action catalog.
- `seed-catalog/` contains baseline seeded runtime scenarios and live-run profiles for fresh UI-driven runs.

## Validation
Use `ProcessTemplateGovernanceTests` to validate JSON references, dependency graphs, artifact inputs, live-run profiles, and current baseline expectations:

```powershell
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessTemplateGovernanceTests"
```
