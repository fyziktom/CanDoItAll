# Domain Driver Roadmap

## Phase 1: .NET/Rust Transcript Verifier
Implemented as a verification-only alpha package. It is read-only over supplied transcripts and evidence references, returns diagnostics/audit/redaction/no-mutation proof, and remains disconnected from process runtime.

## Phase 2: Runtime Evidence Consistency Verifier
Read-only over Core execution/finalizer/retry/projection descriptors.

## Phase 3: Business Analysis Evidence Reviewer
Read-only over generated artifacts and business-analysis deliverables.

## Phase 4: Office Evidence Reviewer
Read-only over already ingested/exported email/document/task evidence. No Graph or mutation.

## Phase 5: Execution-Capable Software Driver
Requires sandbox, command allowlist, timeout, output hashing, secret masking, explicit file-system/network policy, and runtime owner.
