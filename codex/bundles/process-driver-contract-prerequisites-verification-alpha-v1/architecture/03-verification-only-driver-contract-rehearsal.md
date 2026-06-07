# Verification-Only Driver Contract Rehearsal Shape

## Purpose

Define a test-only shape for future driver contracts without production runtime.

## Candidate Request Shape

- Mode
- Lane
- Process facts
- Core evidence descriptors
- Artifact references
- Proof transcript references
- Requested operation
- Caller context

## Candidate Response Shape

- Accepted or denied
- Denial reason
- Diagnostics
- Evidence references
- Redaction status
- No mutation performed flag

## Explicitly Denied In This Bundle

- Production C# driver interfaces.
- Runtime registry.
- DI registration.
- Manager command.
- Runtime selector.
- Shell execution.
- Office/Graph calls.
- Workspace/storage writes.
- Process transitions.
