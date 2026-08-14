# Findings

## F-001 — Legacy process-plan classifier depends on creation time

**Severity:** merge blocker  
**Priority:** P0 compatibility

### Current behavior

When `PlanHashAlgorithmVersion` is absent, the mapper rejects a plan if either:

- `CreatedAtUtc` is at or after the hard-coded migration boundary; or
- the payload contains one of the host-capability seal property names.

The PostgreSQL migration uses the same cut-off to mark rows as `LegacyV1`.

### Failure scenario

A database created from `development` can contain a valid V1 plan created after
the chosen cut-off. The code that created it did not know the future V2
algorithm, but the migration classifies it as `Unknown`, and the mapper refuses
to load it.

### Required behavior

Classification must be based on a structured examination of the persisted
payload:

- no V2 host-capability shape: verified V1, `NeedsRecompile`;
- complete valid V2 shape: V2, executable only when metadata agrees;
- partial, malformed, or conflicting shape: `Unknown`, fail closed.

The timestamp may remain audit information but must not authorize or reject an
algorithm version.

## F-002 — Attachment failure is outside process cleanup

**Severity:** merge blocker  
**Priority:** P0 lifecycle

### Current behavior

`Process.Start` and `ownershipStart.Attach(process)` execute inside the first
try/catch. If `Attach` throws, the exception is converted to
`WorkspaceProcessStartException`. The cleanup block that kills and disposes the
process is located later and is never reached.

### Required behavior

Any failure after `Process.Start` must perform non-cancellable best-effort
cleanup before returning failure:

- terminate an already established ownership boundary;
- abort a partially established boundary;
- terminate the root process/tree if no boundary can be recovered;
- dispose process and native handles;
- return no session and no process identity.

## F-003 — Legacy Manager registry has no safe boundary migration

**Severity:** merge blocker  
**Priority:** P0 recovery compatibility

### Current behavior

The registry schema is still 1. `WorkspaceOwnedProcessIdentity` now contains a
required `Boundary`, but registry validation checks PID, timestamps and hashes
without checking that boundary. A legacy JSON record can therefore enter the
current model with missing boundary data.

### Required behavior

- Current writes use a new schema.
- Schema-1 records are read through a dedicated legacy DTO.
- A record without boundary is converted to `OwnershipUnverified` with a
  stable diagnostic such as `legacy-process-boundary-missing`.
- It never reaches `TerminateOwnedProcessAsync`.
- No automatic PID-only or root-only kill is introduced.
- The converted state is durably rewritten in the current schema.

## F-004 — Container dependency proof is implicit

**Severity:** hardening  
**Priority:** P1 operations

Linux process ownership resolves `/usr/bin/setsid` or `setsid`, while the
Dockerfile does not explicitly declare `util-linux`. The base image may already
contain it, but the application image contract and Docker validator do not
prove that fact.

Recommended closure: explicitly install `util-linux` and add a disposable
container probe for `setsid`. A direct libc `setsid` bootstrap can be a later
optimization.

## F-005 — Final evidence predates final source

**Severity:** merge gate  
**Priority:** P0 evidence

The integrated M08 report is anchored before the final committed source,
generic-agent authority fix, and MAF 1.17 changes. The current HEAD has no
hosted check run.

Required closure: one bounded exact-head local gate after F01–F04 and the
existing MAF changes. A complete broad-suite rerun is not automatically
required.
