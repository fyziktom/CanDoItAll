# Current State Analysis

## What is now stable enough

- `CanDoItAll.Processes.Core` exists and is already used for deterministic route, subprocess, artifact, execution, finalizer, diagnostic, projection, and validation descriptors.
- Core dependency hygiene is guarded by architecture tests and source scans.
- Public API surface is inventoried and owner-classified.
- Runtime orchestration remains correctly module-local.

## What is not ready yet

Production domain drivers are not ready because the following prerequisites are not executable yet:

- permission modes and capability scopes,
- audit fact shape and redaction policy,
- sandbox and command denial policy,
- evidence ownership and lane boundaries,
- negative tests proving read-only modes cannot mutate process state,
- first alpha lane decision backed by executable proof.

## Recommended next cutline

Do not add production drivers yet. Build the prerequisite enforcement bundle first.
