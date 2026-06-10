# Target Architecture

## Layers

1. **Process Core**: deterministic pure rules/read models. No drivers, UI, EF, workspace, storage, MAF, OpenAI, or module references.
2. **Process Module Runtime**: owns lifecycle, definitions, run start, outbox, dispatch, finalizer, artifacts, recovery, manager diagnostics, API/UI, scheduler/workflow-origin starts.
3. **Process Driver Verification Host**: read-only diagnostic host inside the process module. It selects explicit verification lanes, applies options/limits, records audit, returns structured results/denials, and never mutates process state.
4. **Domain Driver Packages**: verification-only packages over supplied evidence. They never self-register, discover, execute, call external systems, or write state.

## Current allowed host
The current allowed host is **verification-only**. It may inspect supplied facts and produce diagnostics/audit/readback. It may not execute domain actions.

## Future execution-capable host
A future execution-capable host requires a separate approval bundle with sandbox, allowlist, authorization, audit persistence, lifecycle owner, failure handoff, cancellation, timeout, emergency stop, and red-team proof.
