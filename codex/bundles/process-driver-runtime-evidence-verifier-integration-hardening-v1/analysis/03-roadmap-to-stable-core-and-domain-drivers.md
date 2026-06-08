# Roadmap To Stable Process Core With Domain Drivers

## Stage 1 — Stable Core Pure Rules
Status: substantially complete.
- Route, subprocess, artifact, execution/finalizer evidence, retry diagnostics, projection/validation descriptors are in Core.
- Continue API governance and dependency scans.

## Stage 2 — Verification Driver Foundation
Status: in progress.
- Driver abstractions exist.
- `.NET/Rust transcript verifier` alpha exists.
- Process read-only adapter exists.
- Next: decompose verifier/adapter and add runtime evidence consistency verifier.

## Stage 3 — Verification-Only Driver Family
Next.
- `.NET/Rust transcript verifier`: harden.
- Runtime evidence consistency verifier: implement read-only alpha.
- Office/business analysis lanes: keep read-only and denial-first.

## Stage 4 — Controlled Process Consumption
Later.
- Add an explicit process evidence provider and observation persistence only after separate approval.
- No scheduler/workflow/manager command until audit persistence and permission enforcement are production-ready.

## Stage 5 — Execution-Capable Drivers
Much later.
- Requires sandbox, allowlist, timeout, output hashing, secret masking, runtime ownership, audit persistence and denial-first tests.
