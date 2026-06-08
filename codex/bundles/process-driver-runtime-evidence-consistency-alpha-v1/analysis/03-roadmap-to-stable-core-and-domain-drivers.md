# Roadmap To Complete Stable Process Core With Domain Drivers

## Current milestone
Stable deterministic Process Core exists. Driver abstractions exist. First `.NET/Rust` transcript verification alpha exists. A narrow process-module read-only adapter exists.

## Next milestone covered by this bundle
Create a second read-only verification alpha around runtime evidence consistency and harden the existing transcript alpha by decomposition and shared policies.

## Later milestones after this bundle
1. Process-module controlled adapter for runtime evidence consistency becomes production-read-only if this bundle proves safe.
2. Optional package split for shared verification policy only if duplication becomes measurable and tests prove no runtime ownership.
3. Business-analysis read-only verifier over existing deliverables, not CRM/business-record mutation.
4. Office read-only verifier over supplied evidence snapshots only, not Graph/mail/task/document mutation.
5. Runtime host/registry/DI/manager command design bundle only after at least two verification-only drivers prove stable and after permission/audit/sandbox ownership is complete.
6. Execution-capable driver lane much later with sandbox, allowlist, timeout, output hashing, audit persistence, secret masking, network/filesystem boundaries, and explicit side-effect ownership.

## Stable Core acceptance target
Core may own deterministic descriptors and rules only. It must not own runtime dispatch, execution, claim lifecycle, transition application, finalizer application, storage/workspace IO, provider repair, retry scheduling, or domain-driver runtime.

## Domain driver acceptance target
Domain drivers start as verification-only readers over supplied evidence. They must return diagnostics/audit/redaction/no-mutation proof. Runtime execution is a separate future capability.
