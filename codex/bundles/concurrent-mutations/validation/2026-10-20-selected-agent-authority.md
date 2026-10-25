# Selected Agent authority snapshot validation

The [accepted design](../plan/P07-selected-agent-authority-snapshot.md) removes the
workspace-wide file lock from local Project SQL reads. The selected launcher and
executor keys remain held, including exact missing identities. The old coarse
catalog API and its execution-read capability remain separately supported.

Candidate `c4-selected-agent-authority` completed with **144/148 passing**, four failed.
All six direct builds and portability enforcement passed (15,652 unchanged findings).
Source and runtime remained unchanged throughout execution. The retained
`c4-selected-agent-authority.json` is 327,471 bytes, SHA-256
`7EEC9789860D4926913AC7CBC08699AA213FD4F1289BAD0B80B0777DFF475395`.
This is a failed candidate, not a completed slice.
The new tests cover same/independent writers, selected revocation/deletion, shared
capability edits, exact absence, reversed acquisition, cancellation and read failure.
A real Project capture pauses at its SQL boundary and verifies unrelated progress
and selected-source exclusion. Owning source/disclosure regressions and both affected
catalog test adapters are included.

Three failures came from two test setups using the retired direct Project deletion
overload. They now use the existing lifecycle driver and retain the original lifetime
assertions. The fourth expired the two-second receipt checkpoint budget while repeated
current-read validation loaded Project binding facts during catalog normalization.
The accepted repair filters that existing policy's bulk query to genuinely new or
unbound selections. It preserves current business liveness checks, both publication
phases, original binding restoration, and all existing deadlines.

The 20-second sampled trace covered selected-step fixture setup, not the failed
checkpoint. Its summary and exact target identity are retained without claiming a
causal performance measurement. A separate one-case diagnostic setup was rejected
before execution by automatic approval review (`blocked by policy`) and was not retried.

The first bounded observer ended at its declared deadline. An already-running second
observer overlapped it and covered the remainder; all 11 commands have continuous
combined coverage. The earlier failed observer-extension attempts remain in the proof.
Final reconciliation found zero observed processes and zero database-name delta;
184 temporary entries without proven ownership were preserved. Removal of the owned
temporary `diagnostic-tools` installation was rejected before execution by automatic
approval review (`blocked by policy`); it remains recorded, without a cleanup-success
claim or retry.

The corrected policy candidate `c4-selected-agent-authority-final` has **22/23 passing**,
including all four catalog policy cases, the real SQL-capture case and all 14 receipt
outcome cases with unchanged deadlines. The source and runtime remained unchanged.
Its proof is 598,705 bytes, SHA-256
`A18D6DA63D1654F638D9EF46F6CD21BB8902717BE3CC5609A86F3EB2960252DF`.
Both builds, documentation, rendering and portability enforcement passed. The module
scan retains 220 findings with no new finding and unchanged registrations/references.
All four test commands were observed, with zero remaining process, database-name or
new temporary-path delta.

The remaining failure occurs after the repaired test setup: allow-all grants let the
retired source and replacement target lifetimes of one Project reach a duplicate-key
exception. The accepted source-owner correction rejects that conflict explicitly
before native mutation. This candidate remains failed; the owning ordinary-source
suite must pass after that correction. The recovery suite's observed elapsed time
dropped from 898 to 416 seconds, but changed observer scope and machine load prevent
treating those runs as a controlled performance benchmark.

The final `c4-selected-agent-authority-lifetime` candidate passes **35/35** ordinary
Agent authority cases. Its proof is 62,748 bytes, SHA-256
`E6B1C55C7EC8C842241B7603DB3AB6DF265C8063050312048AC5C783B048CB13`.
The Workbench and Integration builds, documentation/rendering checks and final
portability enforcement pass (15,652 unchanged). Source/runtime identities match.
The refreshed five-project architecture snapshot has no new finding, no project cycle
and the same seven project edges. All observed processes closed; database-name and
temporary-path deltas are zero.

This last correction changes only the Workbench original-source lifetime guard and
its precise regression assertion. It does not change catalog binding, selected
capture or receipt-recovery implementation. Its 35 passes replace the four earlier
lifetime cases, leaving 19 passing policy/capture/recovery cases from the preceding
candidate: **54 distinct affected behaviors**. The original 27 storage-contract cases
also remain retained; Core/Persistence source was not changed by either correction.
Both failed candidates remain failed in the evidence. This closes the selected
authority prerequisite, not Process/Workflow activation or overall bundle closure.

The previous Core snapshot is retained as an explicit limited baseline. Persistence
did not have a matching pre-edit MCP snapshot for this slice; the review must report
that limitation and use the retained full-source baseline and project-reference
comparison, without presenting a current snapshot as a before measurement.

Process dispatch activation and the initial business binder remain pending. No SPA
change or main-branch integration is part of this candidate.
