# Domain Driver Roadmap

## Phase 1: .NET/Rust Transcript Verifier
Read-only over supplied transcripts and evidence references.

## Phase 2: Runtime Evidence Consistency Verifier
Read-only over Core execution/finalizer/retry/projection descriptors.

## Phase 3: Business Analysis Evidence Reviewer
Read-only over generated artifacts and business-analysis deliverables.

## Phase 4: Office Evidence Reviewer
Read-only over already ingested/exported email/document/task evidence. No Graph or mutation.

## Phase 5: Execution-Capable Software Driver
Requires sandbox, command allowlist, timeout, output hashing, secret masking, explicit file-system/network policy, and runtime owner.
