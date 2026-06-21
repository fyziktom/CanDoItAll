# Phase 0 Reference Archive And Removal

## Design Intent

Phase 0 is split into SB01 and SB02. It is destructive only after archive proof exists, so it must be mechanical, auditable, and gated.

This phase is not executed by v2.

This v3 bundle also does not execute Phase 0. It prepares the future implementation instructions.

## SB01 Archive-Only Scope

SB01 copies and inventories all legacy evidence. It does not delete active source.

SB01 owns:

- reference archive path creation,
- source/test/template/integration snippet copy,
- SHA-256 manifest,
- line counts,
- file categories,
- reuse decisions,
- old test inventory,
- template pack inventory,
- integration reference inventory.

No active product source removal happens in SB01.

## SB02 Active Removal And Skeleton Scope

SB02 starts only after SB01 archive proof is complete.

SB02 owns:

- removing active old Process implementation from `src/`,
- removing or quarantining old Process tests,
- updating solution references,
- cleaning DI/routes/navigation/EF/scheduler/workflow/workbench references,
- adding skeleton projects in corrected dependency order,
- restoring build through target skeletons,
- adding architecture dependency tests,
- adding vocabulary leak tests,
- running old-symbol search proof.

If repository policy requires every commit to build, do not merge an active-removal commit without the skeleton/build-restoration commit.

## Non-Goals

- Do not implement new runtime behavior.
- Do not port old dispatcher logic.
- Do not wrap the old dispatcher.
- Do not migrate templates into the new canonical schema.
- Do not blindly delete `Templates/Processes`.
- Do not keep old tests compiling by reintroducing old runtime contracts.

## Preflight

1. Create or verify rewrite branch, using the repository branch naming convention.
2. Verify clean working tree.
3. Verify latest architecture bundle is committed.
4. Capture `git rev-parse HEAD`.
5. Capture `dotnet --info`.
6. Capture solution/project inventory for Process-related projects.
7. Search references to `CanDoItAll.Modules.Processes`, `CanDoItAll.Processes.Core`, `CanDoItAll.Processes.Contracts`, and `CanDoItAll.Processes.Drivers.*`.
8. Inventory Process-related tests.
9. Inventory `Templates/Processes`.
10. Inventory DI registrations, navigation/menu entries, routes, EF configuration, migrations, module registrations, scheduler/workflow integrations, and project-structure integrations.

## Reference Archive Path

Use a separate reference bundle:

```text
codex/bundles/process-module-rewrite-reference-v1/legacy/
```

Reason: the architecture bundle should remain a proposal. The reference archive can become large and should have its own manifest, proof, and lifecycle.

## Archive Scope

Archive at least:

- `repo://src/CanDoItAll.Modules.Processes`
- `repo://src/CanDoItAll.Processes.Core`
- `repo://src/CanDoItAll.Processes.Contracts`
- `repo://src/CanDoItAll.Processes.Drivers.Abstractions`
- `repo://src/CanDoItAll.Processes.Drivers.ArtifactEvidence`
- `repo://src/CanDoItAll.Processes.Drivers.BusinessAnalysis`
- `repo://src/CanDoItAll.Processes.Drivers.ObservationAggregation`
- `repo://src/CanDoItAll.Processes.Drivers.OfficeEvidence`
- `repo://src/CanDoItAll.Processes.Drivers.RuntimeEvidence`
- `repo://src/CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence`
- `repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification`
- `repo://src/CanDoItAll.Processes.Drivers.VerificationGateway`
- `repo://Templates/Processes`
- process-related tests under `repo://tests`
- process-related snippets from Web, composition, module registration, EF configuration, migrations, and scheduler/workflow integration points.

## Archive Manifest

Create:

- `codex/bundles/process-module-rewrite-reference-v1/manifest.json`
- `codex/bundles/process-module-rewrite-reference-v1/manifest.md`

Required JSON fields per file:

```json
{
  "sourcePath": "src/CanDoItAll.Modules.Processes/Runtime/ProcessRecoveryRouter.cs",
  "archivePath": "legacy/src/CanDoItAll.Modules.Processes/Runtime/ProcessRecoveryRouter.cs",
  "sha256": "...",
  "fileSizeBytes": 12345,
  "lineCount": 227,
  "category": "runtime-recovery",
  "decision": "adapt-concepts",
  "reason": "Contains useful no-progress fingerprinting, but is coupled to current agent-centric recovery options.",
  "relatedRequirements": ["REQ-018", "REQ-020", "REQ-021", "REQ-022", "REQ-023"],
  "relatedFutureTests": ["manager-recovery-budget-tests", "incident-preprocessing-tests"]
}
```

Allowed decisions:

- `keep-as-reference`
- `adapt-concepts`
- `port-after-redesign`
- `drop-after-archive`
- `migrate-template-input`
- `replace-with-new-architecture`

## Commit Plan

| Commit | Scope | Gate |
| --- | --- | --- |
| A | Reference archive only. Copy sources, tests, templates, snippets, manifest, and hashes. | Hash validation passes. |
| B | Active removal and solution cleanup. Remove old Process projects from active `src/`, remove/quarantine old tests, update solution, DI, routes, navigation, EF registrations, and integrations. | Search proof shows old active references gone. |
| C | New skeleton projects. Add empty/skeleton projects in corrected target dependency order and restore solution build. | Build passes without old dispatcher. |
| D | Architecture tests. Add forbidden dependency and vocabulary leak tests before behavior rebuild starts. | Architecture tests pass. |

If every pushed commit must build, combine B and C into one commit. Do not merge B without C under build-required repository policy.

## Deletion Categories

Active implementation to remove after archive:

- `src/CanDoItAll.Modules.Processes`
- `src/CanDoItAll.Processes.Core`
- `src/CanDoItAll.Processes.Contracts`
- `src/CanDoItAll.Processes.Drivers.Abstractions`
- `src/CanDoItAll.Processes.Drivers.*`

Tests to remove or quarantine:

- tests matching Process, Processes, ProcessRun, ProcessStepRun, ProcessWorkspace, LiveProcesses, ProcessObservation, ProcessTemplate, ProcessRuntime, or ProcessDriver that compile against old contracts.

Configuration to update:

- `CanDoItAll.slnx`
- DI registrations
- Web navigation and routes
- EF DbSet/entity configuration
- migrations referencing old entities
- template import/projection services
- scheduler/workflow process launch integrations
- workbench/project-structure references to process runs or assignments.

Do not blindly delete:

- `Templates/Processes`, because it is migration input.

## Search Proof

After active removal, run searches for:

```text
ProcessRunAutomationDispatchService
ProcessesService.StartRunAsync
ProcessBranchOutcomeRouting
ProcessObservationService
ProcessObservationCache
ProcessRecoveryRouter
ProcessStepRun
ProcessArtifactRecord
ProcessJournalEntry
ProcessDriverVerificationGateway
current-module.import-envelope
current-module.compatibility-report
```

Remaining matches must be only in the reference archive, architecture bundle, tests intentionally quarantined as reference, or approved migration input.

## Skeleton Boundary Order

1. `CanDoItAll.Processes.Contracts`
2. `CanDoItAll.Processes.Abstractions`
3. `CanDoItAll.Processes.Core`
4. `CanDoItAll.Processes.Drivers.Abstractions`
5. `CanDoItAll.Processes.Projections`
6. `CanDoItAll.Git`
7. `CanDoItAll.Processes.Templates`
8. `CanDoItAll.Processes.Builder`
9. `CanDoItAll.Processes.Runtime`
10. `CanDoItAll.Processes.Persistence`
11. `CanDoItAll.Processes.Application`
12. `CanDoItAll.Processes.Drivers.*`
13. `CanDoItAll.Components.Git`
14. `CanDoItAll.Modules.Processes`

## Gates

| Gate | Required proof |
| --- | --- |
| P0-G01 | Reference archive manifest exists and hashes are reproducible. |
| P0-G02 | Old active projects removed or quarantined. |
| P0-G03 | Old process-specific tests removed, quarantined, or rewritten. |
| P0-G04 | Solution/project references updated. |
| P0-G05 | No old implementation references remain outside reference/migration input. |
| P0-G06 | New skeleton project boundaries restore build. |
| P0-G07 | Architecture dependency tests exist before behavior rebuild starts. |

## Failure Handling

If removal reveals broad hidden dependencies:

1. Stop and record a removal blocker.
2. Add blocker details to the implementation execution report.
3. Do not patch around it by reintroducing old runtime behavior.
4. Add a compatibility adapter only when it is domain-neutral and belongs to target architecture.
5. Reopen architecture if the target boundary is genuinely wrong.
