# Validation sequencing and proof requirements

## Before code changes

Read current AGENTS, engineering instructions, `docs/testing.md`, CI, canonical UI seams and applicable SharedInfo guidance. Inspect all current source contracts. Use CodeAnalytics MCP for affected production/test callers when available, Components MCP for component contracts and dotnetwatch MCP for host/watch information. If unavailable, explicitly use local search, build-backed discovery, evaluated graphs, actual component source and CLI/Playwright; lack of a connector is not evidence no tests exist. [WS25, WS26]

Record a clean/dirty entry inventory and the actual sibling revisions, including CI pins. Do not overwrite unrelated work. Keep the prior package as historical input. Capture a baseline for each surface before moving it: actual UI screenshot, source/dependency/asset graph, owner semantics and narrow tests. Existing code assertions that intentionally protect old owner behavior must be understood before replacement.

## Stage gates

| Gate | Must run |
|---|---|
| S0 current slice | Same-row catalog real inputs; selection lifetime suite; actual Agent Apply/Cancel/Save; baseline owner restrictions |
| W1 | Recovery standalone state/renderer tests; actual owner and HTTP authority; Workflow/native continuation; source + published sandbox; production dialog |
| W2 | Data Sources standalone state/forms/dialog tests; real private PostgreSQL/profile/credential/transfer owners; canonicality and restart; MainLayout + Settings browser |
| W3 | Configuration/UI and trusted registry tests; actual registered renderer; affected Workflow/Plugins consumers and browser; no reverse dependency |
| W4 closure map | Every remaining Workspace renderer/host classified; real boundaries and all existing Settings entries preserved |
| Final application | All campaign groups, full Stable, non-live browser inventory, bounded live lane, protected graph/watch comparison and final gates |

Select exact method/class topics for iterative tests. Before every new/changed filter, state expected cases, list tests against the freshly built assembly, reconcile actual discovery and only then run the same filter. Include data-driven cases; when descriptors expand at execution, retain a method-by-method mapping. Zero discovered/executed tests cannot prove coverage. Do not run stale binaries with `--no-build` after an edit.

Use awaited event dispatch and the repository's bUnit lifecycle helpers. Assert completion of the actual operation (target ID/generation/result), not merely a disabled button, a pre-existing Ready label or the presence of unhydrated markup. Browser assertions should retry against exact locators and application state instead of fixed sleeps. [WS24, EX03]

## Full checkpoint gate

The broad gate is explicitly authorised by this assignment. Read its current command at execution. At review the Stable filter is:

```text
Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined&Category!=UnixRuntimePortability&RequiresHostDocker!=true
```

Build and restore the product solution and the separate Stable solution in an isolated final configuration. Keep live environment gates off in this lane. Use the same filter for discovery and execution; retain TRX, process exit and source checkpoint. LiveAgent tests that return without effect under closed gates are not counted as live coverage even when their runner status says Passed.

Run the non-quarantined browser inventory separately. Partition out opt-in LiveAgent tests for the real live lane; account for every discovered method rather than silently excluding hard cases. Examine other external/process-spawning browser scenarios for actual fixture isolation before running. Existing quarantines remain visible as excluded coverage, not silently repaired by removing their trait or asserted as pass.

The ordinary static gate is not a substitute for actual host portability. Run feasible documented host/headless/LiveProcess lanes relevant to changed profile/transfer/file code. PostgreSQL logical restore and external client tests require verified isolated clients. Report unavailable OS/runtime lanes as untested; a local Windows result is not macOS/Linux proof. Do not change CI shard policy or timeout budgets just to fit this campaign.

## Final source and revalidation

Record the exact repository HEAD plus a SHA-256 manifest of all uncommitted product/build/test changes, sibling SHAs/tree hashes and configuration before the frozen build. Treat every test attempt as bound to that checkpoint. If code changes afterwards, either rerun the invalidated gate or explain exact unchanged owner/graph scope and run fresh dependent proof. Never present the earlier failing broad run as all-green after a targeted repair.

Prefer a final clean broad result after bounded fixes when execution resources permit. When a complex mapped blocker or repeated external failure prevents it, retain the mixed run and state release confidence honestly. A finite test run must finish with observable bounded cleanup; do not leave test processes or a daemon waiting until morning.

## Static and documentation closure

Run current portability scanner/tool tests over the complete proposed tree, including new files. Review added/stale results, repair genuine defects, refresh only intentional reviewed deltas, inspect the diff and require final enforcement without `--write-baseline`. Preserve scanner policy. Run documentation/evidence tests, host classification checks and package/tool tests as applicable.

Run the existing artifact/source secret scanner on all generated text, logs, TRX and traces; inspect binary screenshots separately. Do not declare binary files inspected by a text scanner. Synthetic sentinel findings can be explained individually, not blanket-excluded. Maintain a safe evidence manifest and root cleanup record.
