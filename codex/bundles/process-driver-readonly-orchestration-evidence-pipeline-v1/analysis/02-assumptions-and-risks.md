# Assumptions And Risks

## Assumptions
- The latest branch is `maf-processes-refactor`.
- Existing driver packages are read-only alpha libraries and must remain runtime-free.
- Process module may use explicit adapters, but it must not gain a generic driver runtime.
- Gateway expansion must be explicit typed methods / typed batch envelopes, not `object`, reflection dispatch, service locators, or registry-like runtime selection.

## Critical Path Risks
- Gateway batch API accidentally becomes a generic runtime selector.
- Process module references all driver packages directly and becomes hard to govern.
- Splitting adapters changes observation behavior.
- Supplied evidence builders accidentally read files or storage instead of receiving resolved content.
- Audit/redaction/no-mutation behavior diverges across lanes.
- Runtime-host approval language sneaks into docs after many read-only proofs pass.

## Validation Risks
- Build/full-unit proof alone would miss runtime-host drift.
- Happy-path diagnostics would not prove side-effect denial.
- Status-only bundle rows can hide crash recovery gaps.
- Prose docs can claim runtime remains denied while package source introduces gateway host/selector/DI tokens.

## Reopen Triggers
- Any Core reference to `CanDoItAll.Processes.Drivers`.
- Any driver or process adapter source containing registry/selector/DI/manager/scheduler/workflow/runtime-host/file/network/persistence/process-mutation tokens.
- Any new public gateway API accepting `object`, untyped lane dispatch, or arbitrary operation names.
- Any failure in full unit, focused driver unit matrix, focused process adapter integration, source scans, prepared validator, or completed validator.
