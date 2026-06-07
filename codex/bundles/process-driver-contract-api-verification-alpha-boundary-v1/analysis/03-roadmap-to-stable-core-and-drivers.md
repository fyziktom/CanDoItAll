# Roadmap To Stable Process Core With Domain Drivers

## Milestone A — Stable Process Core
Status: mostly complete.
- Routing descriptors: done.
- Subprocess lifecycle/mapping: done.
- Artifact expectations/matching: done.
- Execution/finalizer/retry/projection descriptors: done.
- Remaining: public API governance, compatibility documentation, owner classification, semantic versioning.

## Milestone B — Driver Contract Boundary
This bundle.
- Create a contract-only driver abstractions project.
- Define permission modes, capability scopes, denial reasons, evidence references, audit facts, redaction, and diagnostic response models.
- Add architecture tests proving this is not a runtime system.

## Milestone C — Verification-Only Alpha
Next after this bundle if Gate K approves.
- First candidate: `.NET/Rust transcript verifier`.
- Reads only existing build/test/proof transcripts.
- Produces diagnostics only.
- No shell execution, workspace writes, storage writes, process mutation, claims, transitions, finalizer, or retries.

## Milestone D — Domain Driver Families
- `.NET/Rust`: transcript and artifact verifier first; execution-capable mode much later.
- Office: evidence reviewer over already-ingested mail/docs only; no Graph calls or mail/task/document mutation.
- Business analysis: gap reviewer over produced artifacts only; no CRM/business-record mutation.
- Runtime verification: execution/finalizer/retry descriptor consistency checker.

## Milestone E — Execution-Capable Drivers
Denied until sandbox, command allowlist, timeout, output hashing, secret masking, audit log persistence, and side-effect ownership are production-ready.
