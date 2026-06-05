# Process Dispatch Pre-Execution Guard & Upstream Materialization Boundary v1

## Status

- Status: `Completed`

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Passed`
- Browser validation analytics: `N/A - no UI changes in scope`
## Purpose

This bundle continues the `maf-processes-refactor` line after the candidate factory/cooperation extraction. It deliberately does **not** create `CanDoItAll.Processes.Core`, process driver packs, driver registries, or public driver APIs.

The next safe seam is the pre-execution guard and upstream artifact materialization path currently sitting in `ProcessRunAutomationDispatchService.Dispatch.cs`.

## Why this bundle exists

The latest branch already extracted candidate header selection, candidate hydration readback, candidate construction, technical-agent binding, recovery query helpers, and cooperation metadata. The remaining dispatch path still mixes:

- database runtime requirement blocking,
- missing upstream artifact detection,
- downstream block transition,
- materialization fingerprinting,
- journal duplicate protection,
- upstream rerun request construction,
- `ProcessesService.RerunAgentStepAsync`,
- dispatch route continuation,
- logs and candidate state continuity.

This is still too side-effectful for Process Core and too early for production process drivers.

## Hard constraints

- Do not create `CanDoItAll.Processes.Core`.
- Do not create `IProcessDriverPack`, driver registry, driver packages, or production driver APIs.
- Keep all new production helpers under `src/CanDoItAll.Modules.Processes/Automation/Dispatch/`.
- Preserve all existing public/internal dispatcher wrappers unless explicitly replaced by a focused test.
- Preserve behavior of `TryRequestMissingUpstreamArtifactMaterializationAsync`, `BlockDispatchForDatabaseRequirementAsync`, and `LoadDispatchCandidateAsync`.
- Browser/UI validation is `N/A` unless UI files are changed unexpectedly. Do not run small, medium, mobile, phone, tablet, Android, iPhone, or responsive proof.
- If UI proof becomes unavoidable, use only large desktop/PC viewport proof and document why.

## Expected result

After this bundle, `Dispatch.cs` should delegate database guard and upstream materialization work to module-local helpers. The dispatcher should still own orchestration and side effects, but the decisions, request building, fingerprinting, journal dedup, and rerun request construction should be isolated and tested.

## Next-step policy

The bundle prepares for both future Process Core and future process helper drivers, but only via local vocabulary and documentation. Driver readiness is a semantic map, not a production API.
