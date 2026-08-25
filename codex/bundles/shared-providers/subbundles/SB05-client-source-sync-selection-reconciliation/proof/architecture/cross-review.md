# SB05 independent cross-review

State: `PASS`.

Three read-only reviewers examined the final Http, Workspace, reconciliation, integration-test, and
dependency surfaces. Initial review found two correctness/security blockers and two proof gaps:

- conditional GET could retain an ETag after a transient/import failure and accept a later 304,
  preventing recovery;
- framework `HttpClientFactory` logging and request stringification could disclose a private source
  URI;
- special-purpose address coverage was too narrow;
- enable/disable, remote-owned refresh, and actual named-client pipeline behavior needed stronger
  realistic proof.

The repair gates conditional requests on authoritative source/import availability, rechecks that
state before accepting 304, and falls back to an unconditional fetch when concurrent state changed.
All shared-provider named clients remove framework loggers, request/token stringification is redacted,
and the public-only classifier rejects the relevant IANA special-purpose ranges. Existing Facts were
extended without changing the frozen 18/22/16 counts.

Final re-review found no remaining production or DI defect. The last two proof-strength notes were
also closed: the integration fact now synchronizes successfully after re-enable, and the real
named-client logging test covers both catalog and relay clients.
