# Implementation Prompt

```text
Execute only the named dashboard subbundle. Read the root README, requirements, architecture gate/checkpoints, this subbundle README, and current execution report. Verify the entry gate before edits.

Deliver the observable outcome with the smallest strongly typed change. Preserve module ownership; do not add project/package references, partial/nested services, IServiceProvider lookup outside the dedicated singleton lifetime runner, silent fallback, mutable/unbounded cached collections, user-specific shared-cache data, or broad overview/list/enrichment calls prohibited by the bundle. The lifetime runner must own one fresh async scope per actual refresh and resolve only the scoped loader there. You are not alone in the repository: preserve unrelated edits and never revert them.

Run the exact commands/tests listed for the subbundle. Record raw note, shipped behavior, source proof, test proof, shallow-pass trap, adversarial negative, semantic positive, anti-stub audit, gate result, and any browser artifacts in reviews/01-execution-report.md. Mark Completed only when Behavioral proof passes. If a prerequisite or semantic test fails, stop, mark In progress, and reopen the owning subbundle instead of weakening proof.
```
