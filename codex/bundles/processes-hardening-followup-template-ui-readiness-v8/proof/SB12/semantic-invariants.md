# SB12 Semantic Invariants

- Invariant ID: SB12-INV-001
- Expected behavior: new runtime block paths carry typed `BlockCause` into block reason code, recovery options, next recovery action, recovery routing, run detail, and HTTP health output; text inference remains a legacy fallback only when no typed cause is supplied.
- Disallowed shallow implementation: inferring own-output and upstream-input failures from prose when a typed cause exists, leaving legacy-inferred failure ownership empty in recovery routing, or exposing block reason without corresponding recovery options and health.
- Required proof: adversarial classifier/router proof, passing production-path transition and HTTP API tests, source assertions, anti-stub audit, and changed-file hashes.
- Positive proof: `bundle://proof/SB12/transcripts/passing.txt` covers typed own-output and upstream-input transitions, run-detail health, HTTP run detail, and existing recovery-router regressions.
- Negative/adversarial proof: `bundle://proof/SB12/transcripts/failing-first.txt` rejects a shallow implementation that lets prose override typed `BlockCause` or drops legacy-inferred ownership before recovery routing.
