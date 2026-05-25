# QA prompt

Review the implementation as an adversarial QA reviewer.

Reject the work if:

- normal DbContext creation still resolves profile and takes runtime switch lease per context,
- hot profile switch remains enabled by default in production path,
- InMemory appears as a persisted Data Sources profile,
- retired-provider strings are hidden with concatenation instead of allowlisted,
- outbox/process concurrency tests use only single-thread/single-worker fixtures,
- tests assert only counts without proving no duplicate claims,
- validation ignores branch divergence from `development`,
- proof artifacts are committed without an explicit repository policy,
- full build/test failures are hand-waved as unrelated without issue references and quarantine reason.
