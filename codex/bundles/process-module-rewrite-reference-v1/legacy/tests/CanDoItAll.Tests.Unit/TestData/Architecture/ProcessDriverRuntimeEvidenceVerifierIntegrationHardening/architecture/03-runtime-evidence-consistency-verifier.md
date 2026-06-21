# Runtime Evidence Consistency Verifier

Goal: add a verification-only alpha that accepts supplied Core descriptor payloads and returns diagnostics about internal consistency.

Allowed checks:
- execution descriptor says success but finalizer descriptor says failed/blocked,
- retry diagnostic says no retry while unresolved critical failures are present,
- projection source order descriptor drift,
- provider repair diagnostic without provider failure,
- finalizer result says apply transition but no result exists,
- no-progress descriptor missing fingerprint when no-progress is signaled.

Denied:
- reading execution runs from runtime,
- provider calls,
- finalizer calls,
- retry persistence,
- process mutation,
- storage/workspace/file reads.
