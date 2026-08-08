# Package-shape decision

## Decision

Ship one distributable ZIP containing two sequential implementation bundles:

1. `01-core-portability-foundation`
2. `02-runtime-tools-process-drivers`

The runtime bundle is prepared but not eligible until Core Gate C4 has a GO result and an exact handoff commit.

## Why one undivided implementation bundle was rejected

A single execution graph would combine two materially different risk classes:

- persisted path/storage/control-plane migration and secret/key migration;
- process execution, terminal presentation, Manager ownership, MCP, plugin host tools, and Processes-domain behavior.

The first class can make existing state unreadable and therefore needs backup, dual-read/one-write, restart, rollback, and corruption-injection gates before runtime complexity is added. The second class crosses many recently refactored ownership boundaries and must be rebased against the post-core architecture.

Codex 5.6 Sol xhigh can process a large specification, but executor context size does not eliminate migration ordering, rollback, ownership, or independent-review requirements.

## Why the runtime bundle is still included now

Preparing it now preserves the complete target architecture and prevents the core implementation from making decisions that block later runtime work. Its `B00` subbundle treats every concrete source reference and task estimate as provisional until the exact Core C4 commit is reviewed.

## Further split triggers

`B00` must split the runtime program before implementation when any of these are true:

- more than 60 production files are expected to change;
- more than eight project-ownership boundaries require coordinated changes;
- an external package must be changed at source rather than adapted locally;
- independent validation gates cannot remain isolated;
- a material MAF or Processes architecture change invalidates the prepared ownership model.
