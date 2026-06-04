# SB06 Semantic Invariants

## Invariant SB06-CONTROL-HELPERS-BOUNDED

- Source raw note: RN02 and R7 require waits and approvals with clear runtime semantics.
- Expected behavior: delay is bounded; approval uses explicit external request semantics; host command execution remains planned until a hardened design exists.
- Disallowed shallow implementation: adding catalog entries that appear runnable but either sleep unboundedly or run arbitrary commands.
- Positive proof: `DelayAndApprovalExecutorsUseBoundedRuntimeSemantics` and `ValidatorRejectsPlannedExecutorNode` in `bundle://proof/SB10/transcripts/dotnet-test-unit-workflow-executor-catalog.txt`
- Source proof: `bundle://proof/SB10/transcripts/source-assertions-executor-implementations.txt`
