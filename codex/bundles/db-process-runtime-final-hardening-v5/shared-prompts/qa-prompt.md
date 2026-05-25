# QA prompt

Review the implementation as an adversarial QA engineer.

Focus on:
- lease ownership,
- stale worker finalization,
- recovery stealing active leases,
- duplicate side effects,
- process dispatch after claim loss,
- PostgreSQL query-plan quality,
- actual throughput numbers,
- broad test-suite caveats.

Reject the work if a stale worker can mutate canonical DB state after losing a lease.
