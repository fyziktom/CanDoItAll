# Shared QA Prompt

Validate the current subbundle against the bundle, repo state, and proof.

Required checks:

1. Confirm the subbundle prerequisites are satisfied.
2. Confirm changed source files match the subbundle scope.
3. Confirm all process tools in `inventories/01-process-tool-parity-inventory.md` are still present after migration.
4. Confirm process read tools remain approval-free.
5. Confirm process mutation tools remain approval-wrapped unless governed automation explicitly suppresses approvals.
6. Confirm `CanDoItAll.AgentFramework.Maf` does not reference `CanDoItAll.Modules.Processes` after SB05.
7. Confirm MAF works without Processes registered.
8. Confirm process tools attach when Processes module is registered.
9. Confirm no dispatcher split or DotNet driver extraction was smuggled into this bundle.
10. Confirm critical proof manifests include changed-file hashes, failing-first/passing transcripts when behavior changes, anti-stub audit, source assertions, and semantic positive/negative proof.

Fail the gate if the proof only checks file existence, count-only parity, or non-empty output.
