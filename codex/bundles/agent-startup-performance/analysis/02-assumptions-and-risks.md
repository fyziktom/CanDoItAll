# Assumptions And Risks

## Assumptions

- The scope is explicitly three improvements; accumulation of startup logs remains deferred.
- Existing contracts are the oracle. A more aggressive optimization must not quietly narrow integrity/recovery semantics.
- Named host ports are stable targets, not proof of current process/image identity.
- Existing tests are relevant source inventories, not executed proof. Runtime discovery must be captured later.

## Critical Path Risks

- Caching case facts across root recreation, same-path case-mode changes or untrusted callbacks can permit incorrect path comparisons. Keep fresh probes at any boundary whose freshness cannot be established.
- Token-only shared revision caches can retain a valid lease after malformed catalog tampering. Retain existing semantic/canonical verification on probes.
- Treating a recovered/deserialized plan as trusted can hide foreign/torn writes. Trust must be unforgeable outside the immediate held-lock path.
- Skipping supposedly unaffected indexes can lose LastUsedAtUtc/revision/session metadata.
- Shared workspace locking can dominate remaining cost; do not split locks or batch logs to satisfy a timing target.

## Validation Risks

- Some Windows symlink tests return early; green totals are insufficient without affirmative execution on a capable Windows/Linux environment.
- Actual provider latency, caches, contention and growing history can distort samples. Use the paired protocol and report every run.
- Browser tool success must be proven from actual trace/output, not the agent saying it used a tool.
- Destructive fault injection belongs to isolated fixtures/test hosts, never either live workspace.
- Inaccessible MCP/host/provider or missing approval fixture blocks the applicable proof; do not substitute API-only happy paths or stubs.

## Reopen Triggers

- A changed boundary, schema, public contract, project reference, path-freshness assumption, provider-availability rule, lock interval or actual source version reopens the owner and dependent evidence. Reopen SB01→SB03 and combined UI/performance; SB02→combined UI/performance; SB03→combined UI/performance. Do not reuse measurements after binary/config/provider/fixture/dependency changes.
