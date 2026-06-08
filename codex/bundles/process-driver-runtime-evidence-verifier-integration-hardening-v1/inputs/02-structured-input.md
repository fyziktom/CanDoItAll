# Structured Input

## User Objective
Inspect the latest pushed `maf-processes-refactor` work after a Codex crash, verify actual production code and proof, then prepare the next implementation-ready bundle toward a stable Process Core with domain drivers.

## Current Verified State
- Previous bundle introduced a `.NET/Rust` transcript verification alpha package.
- Process module has a read-only adapter for supplied process evidence payloads.
- No generic driver runtime, registry, DI selector, manager command, scheduler/workflow hook, shell execution, Graph call, storage/workspace write, or process mutation is approved.
- Next work should be broader and more coherent than micro-subbundles, while preserving strict safety gates.

## Normalized Direction
Proceed with a multi-area driver hardening and second verification-alpha preparation bundle:
1. split monolithic verifier responsibilities,
2. harden evidence/audit/redaction/no-mutation behavior,
3. harden process read-only adapter,
4. implement/rehearse runtime evidence consistency verification,
5. prepare domain lanes and runtime-host roadmap without wiring runtime infrastructure.
