# Real Code Review

## Current branch state observed
- Current branch: `maf-processes-refactor`.
- Current head during review: `2ace4287975ffadc447eb72df083404c6cb1bbbe` (`phase57`).
- Previous checked head before the last implementation: `09d155bc696d15e3bd8d25824f1c321951f4a55a`.

## What landed in real code
The latest implementation did add meaningful code:

- `src/CanDoItAll.Processes.Contracts/Runtime/ProcessRuntimeHostContractModels.cs`
  - Adds generic runtime-host surfaces, effect surfaces, operation categories, sandbox decisions, denials, request identity, audit reference, capability reference, and contract snapshot.
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDryRunExecutionPipeline.cs`
  - Splits dry-run processing into request normalization, capability resolution, sandbox evaluation, authorization evaluation, plan building, and contract/audit mapping.
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDryRunExecutionHost.cs`
  - Uses the pipeline and returns dry-run host results with contract snapshots.
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationHostCapabilityCatalog.cs`
  - Adds static descriptors and provider boundary for verification and dry-run capabilities without reflection discovery or self-registration.
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationJobRunner.cs`
  - Still thin, but now connected as a DI service and tested as a read-only job runner.
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagerReadOnlyVerificationReadback.cs`
  - Splits manager readback DTOs from the command service.

## Remaining code concerns
1. **Template execution confidence remains under-proven.** The runtime-host work is useful, but it does not yet prove real template families such as multi-team development execute end-to-end after the refactor.
2. **The dry-run host is still mostly diagnostic.** It has a pipeline now, but there is not yet a higher-level runtime-host service that owns lifecycle, audit, readback, and future execution gating as a coherent module boundary.
3. **Scheduler/workflow read-only jobs are not yet a durable job lifecycle.** The current runner is a thin wrapper over manager readback; it lacks persisted job status, retries, correlation, and operator readback.
4. **Manager/operator UI proof remains weak.** Existing proof is mostly API/service-level. If users must operate processes from UI, run detail should expose manager verification/dry-run status or at least have route-level readback proof.
5. **Live provider proof was not active in the latest bundle.** The report says the live OpenAI process-run smoke was classified as not opted in. This is acceptable, but cannot be counted as new live proof.
6. **Process Core must remain generic.** Runtime-host contracts belong in `CanDoItAll.Processes.Contracts`, not Core, and domain-specific semantics must stay out of Core.

## Code-vs-bundle ratio observation
The latest bundle improved source/test changes, but still added many bundle files. The next implementation should avoid creating new proof-heavy scaffolding and should keep proof focused on critical gates only.
