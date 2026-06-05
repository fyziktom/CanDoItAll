# Next Cutline

## Recommended Next Bundle

Target `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs`.

## Why This Cutline

- It remains the largest touched dispatcher partial after this bundle.
- It still owns compatibility wrappers, concurrent execution adoption, provider fallback wrappers, and no-progress wrappers.
- The new helper files created here give the next bundle a stable map for deleting wrapper-only code once reflection-based tests are updated.

## Proposed Constraints

- Keep work module-local under `CanDoItAll.Modules.Processes`.
- Do not introduce `CanDoItAll.Processes.Core`.
- Do not introduce production process-driver APIs or registries.
- Preserve retry counts, provider fallback ordering, no-progress compression, recovery journals, and finalizer recovery behavior.
- Keep browser proof N/A unless UI files change.

## Suggested Gates

- Gate A: inventory remaining `Concurrency.cs` wrapper methods and reflection test dependencies.
- Gate B: move or remove one wrapper family at a time after tests stop depending on private dispatcher names.
- Gate C: prove `Concurrency.cs` line count drops below 750 without behavior change.
- Gate D: run the same retry/provider/no-progress smoke matrix used in `bundle://proof/SB42/transcripts/broad-focused-smoke-matrix.txt`.
