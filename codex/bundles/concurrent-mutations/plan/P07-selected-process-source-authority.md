# Exact launching Agent within a held publication snapshot

Entry: the inherited task publication codec has 124 passing selected cases. Current
publication authority must retain separate original launching-source and executor
permissions. Core's inherited publication snapshot intentionally keeps the executor as
its primary Agent and can hold the original launching Agent in the same selected key set.

Workbench's existing Process source checks assume `held.Agent` is always the launcher.
That is valid for their current single-source acquisition but incorrect when consuming
the inherited publication snapshot. It rejects legitimate different-executor runs; using
the executor's grants to work around it would cross the authority boundary. Current source
checks must resolve the saved source ID from the already held primary/selected Agents.
Never reacquire the catalog or substitute an executor for a missing/revoked launcher.

Keep this repair within Workbench. Factor exact held-Agent selection into the existing
source authority owner and use it in source capture, mutation/read checks, source project
ceiling calculation and governed receipt source/executor selection. Preserve existing
original scope, profile generation, source Project lifetime, tool/read/write ceilings and
operator checks. The selected collection is already bounded to two Agents by its owner;
selection adds no I/O, allocation, lease or SQL wait. Unsupported legacy selected access
fails explicitly rather than silently refreshing or borrowing another Agent's grants.

Rename the existing held Workflow-source method to an origin-neutral held-source check
and move its implementation into the existing source authority file, deleting its tiny
runtime partial. Existing Workflow callers retain identical behavior. This shared source
check authorizes only the original launcher; inherited task publication must separately
require the executor's original/current tool and task-write ceilings, Project gates,
selected Process transaction fence, original proposal and finite live work.

Use real file-backed catalog leases and the existing isolated application/database fixture.
Prove both primary orders, missing original source, current source tool/read/write/lifetime
revocation while a different executor remains permitted, and separate source read versus
mutation permission. Repeat existing actual Process launch, rollback/replay and governed
definition receipt tests. Build Workbench and Integration in standard Release, discover
the exact selection, enforce full portability and review before/after dependency findings.
Record frozen source/runtime, observer coverage and resource reconciliation; retain failures.
No provider or Process execution guard is enabled by this prerequisite repair.
