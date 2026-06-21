# SB34 Proof Manifest

## Scope

Reviewed the active Process implementation after SB33, repaired stale bundle gate metadata, and applied the smallest runtime hardening changes that reduce stuck-run and hot-path risk without adding scenario logic to the generic runtime:

- Process dispatch queues now use bounded channels with explicit capacity options.
- A canceled or failed enqueue removes its run-id dedupe marker so the run can be queued again.
- Pending run-id dedupe still suppresses duplicate dispatch requests until dequeue.
- Queue and queue options are split out of the dispatch worker file.
- AgentFramework Process adapter regexes use source-generated regex methods.
- A fresh dev-DB TetrisGame run was executed after the user-cleared output/project-structure state.

## Root Cause

`ProcessRuntimeDispatchQueueServices.cs` owned too much responsibility and created unbounded dispatch channels. That left the dispatcher with no backpressure policy under a burst of ready/recovery work.

The queue inserted run IDs into the queued set before writing to the channel. If the write was canceled or failed, the dedupe marker could remain without a corresponding queued item, which could strand a run until another recovery path happened to compensate.

`AgentFrameworkProcessExecutionAdapter` still used static compiled regex fields. That conflicts with the Process performance guardrails adopted in SB30 and keeps extra runtime regex generation/caching pressure in a large integration adapter.

The user-cleared TetrisGame state removed the prior output and Main App launch target, so the final proof had to recreate the target through public project-structure APIs and run the current implementation end to end.

## Proof Files

- `changed-file-hashes.txt`
- `semantic-invariants.md`
- `validation.md`
- `performance-analysis.md`
- `e2e/README.md`
- `api-tetris-process-start-execute.json`
- `api-tetris-final-run-hierarchy-summary.txt`
- `api-tetris-structure-after-e2e.json`
- `tetris-output-folder-tree.txt`
- `tetris-processes-after-e2e.txt`
- `transcripts/test-focused-runtime-hardening.txt`
- `transcripts/build-processes-module.txt`
- `transcripts/static-performance-and-genericity-scan.txt`
- `transcripts/tetris-output-build.txt`
- `transcripts/tetris-output-test.txt`
- `transcripts/validate-bundle-prepared.txt`
- `transcripts/web-release-runtime-ready.json`

## Validation Result

Prepared-stage bundle validation passed after SB32/SB33 metadata repair. Focused runtime hardening tests passed 68/68. The Process module Release build passed with 0 warnings and 0 errors. Static scans reported zero async-void, zero sync-over-async markers in modified scope, zero `RegexOptions.Compiled`, zero per-call `new Regex`, zero unbounded Process dispatch channels, zero per-call `HttpClient`, zero per-call `JsonSerializerOptions`, and zero scenario vocabulary in generic runtime/application production files.

The fresh TetrisGame process run `cb18af52-506f-4677-bfb2-088514aa4f16` completed on the restarted Release instance at `http://localhost:5032`. The hierarchy contains seven completed runs and one inactive `NeedsAttention` screenshot attempt that was recovered by a later completed screenshot run. The generated TetrisGame output recorded in `tetris-output-folder-tree.txt` builds with 0 warnings and 0 errors, its tests pass 8/8, the project lease was released, and no TetrisGame process remained after runtime cleanup.
