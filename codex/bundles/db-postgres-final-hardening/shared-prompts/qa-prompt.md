# QA prompt

Review the implementation from three angles:

1. Canonicality:
   - Can any stale worker write final status after losing a lease?
   - Can UI/API confuse pending restart activation with runtime active profile?
   - Can profile-specific contexts leak into runtime hot paths?

2. Throughput:
   - Is parallelism actually enabled by default where safe?
   - Are partition keys conservative enough?
   - Is there numeric benchmark evidence?

3. Validation:
   - Are broad-suite caveats closed or honestly quarantined?
   - Are tests adversarial, not only happy path?
   - Are audit/proof artifacts current and not copied from older bundles?
