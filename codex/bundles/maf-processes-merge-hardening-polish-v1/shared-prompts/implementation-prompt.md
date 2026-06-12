# Implementation Prompt

```text
You are executing the MAF Processes merge-hardening polish bundle on branch maf-processes-refactor.

Implement only the current subbundle. Before editing, read the root README, inputs, current-state analysis, requirements, architecture, phase plan, traceability, and the current subbundle README. Verify prerequisites and capture baseline scans/tests required by the subbundle.

Hard constraints:
- No broad dispatcher-runtime isolation before merge.
- No MAF -> CanDoItAll.Modules.Processes dependency.
- No runtime driver host, registry, selector, DI discovery, manager command, scheduler hook, workflow hook, shell execution, Graph/connector call, workspace/storage write, process mutation, finalizer mutation, transition mutation, or retry mutation in driver packages.
- Do not delete codex/skills/bundles tooling.
- Do not preserve SB/INV/bundle/subbundle execution IDs in active test names.
- Preserve working multi-team app delivery behavior.

Make the smallest correct change set for the subbundle. Prefer semantic tests and tracked-file scans over report-only proof. Update reviews/01-execution-report.md with commands, results, changed files, and blockers. Stop if a progression gate cannot honestly pass.
```
