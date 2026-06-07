# Roadmap To Complete Stable Process Core With Domain Drivers

## Milestone A: Stable Core Pure Rules
Status: substantially complete.
Next: keep API governance, descriptor versioning, and adapter allow-lists updated with every Core addition.

## Milestone B: Driver Abstractions
Status: contract-only package exists.
Next: add production verification-only alpha implementation without runtime integration.

## Milestone C: First Verification-Only Alpha
Candidate: `.NET/Rust transcript verifier`.
Allowed: parse supplied transcript/evidence content and return diagnostics.
Denied: command execution, package restore, workspace/storage writes, process mutation.

## Milestone D: Process Module Consumer Adapter
After alpha library proves safe, add a process-module adapter that can call the alpha from controlled proof/evidence workflows, still no manager command or scheduler integration.

## Milestone E: Business Analysis Read-Only Driver
Use existing artifact/evidence snapshots and business-analysis deliverables only. No CRM/business-record mutation.

## Milestone F: Office Read-Only Driver
Use already-exported/ingested Office evidence only. No Graph calls, mail mutation, task creation, or document writes.

## Milestone G: Runtime Host/Registry
Only after several verification-only drivers are safe. Requires registry governance, DI policy, capability gate, audit persistence, denial events, version compatibility, and explicit ownership.

## Milestone H: Execution-Capable Drivers
Much later. Requires sandbox, allowlist, timeouts, output hashing, secret masking, network/filesystem boundaries, explicit mutation ownership, and red-team tests.
