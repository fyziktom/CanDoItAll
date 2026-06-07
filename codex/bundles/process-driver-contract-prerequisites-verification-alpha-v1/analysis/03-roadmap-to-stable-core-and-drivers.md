# Roadmap To Stable Process Core With Domain Drivers

## Milestone 1: Stable Core Pure Rules
Status: In progress, mostly done.
- Route rules: done.
- Subprocess lifecycle/mapping: done.
- Artifact snapshots/matching: done.
- Execution/finalizer/diagnostic/projection descriptors: done.
- Remaining: API versioning, compatibility docs, descriptor governance.

## Milestone 2: Driver Prerequisite Enforcement
This bundle.
- Permission modes.
- Capability scopes.
- Audit facts and redaction.
- Sandbox/command denial policy.
- Negative tests.
- First verification-only alpha lane selection.

## Milestone 3: Production Driver Contracts
Future bundle only after this bundle passes.
- Create contract-only abstractions.
- No runtime dispatch initially.
- No manager commands initially.
- Include audit/permission denial models.

## Milestone 4: First Verification-Only Domain Driver
Likely `.NET/Rust transcript verifier`.
- Inspects existing build/test/proof artifacts.
- Produces diagnostics only.
- No command execution.
- No workspace/storage writes.
- No process mutation.

## Milestone 5: Office And Business Analysis Read-Only Drivers
- Office lane: no Graph calls, no email mutation, no task creation, no document mutation.
- Business-analysis lane: no CRM/business-record mutation.

## Milestone 6: Execution-Capable Driver Gate
Much later.
- Requires sandbox, allowlist, timeout, output hashing, audit, secret masking, and explicit runtime ownership.
