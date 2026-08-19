# Exit criteria

- [x] Gate C1 is GO after independent architecture/security review.
- [x] Filesystem semantics are deterministic and actual-host tested on Windows and Linux; actual macOS remains mandatory before C4.
- [x] Managed-root link escape and unsafe permission cases fail closed.
- [x] Atomic/cross-process behavior is proven before storage or secrets migration.

- [x] Execution report and session handoff are complete for the review candidate.
- [x] No real secret-bearing content exists in proof artifacts; synthetic test vectors are fingerprint-classified.
