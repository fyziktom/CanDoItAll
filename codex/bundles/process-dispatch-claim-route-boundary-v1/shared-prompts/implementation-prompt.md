# Implementation Prompt

You are implementing `process-dispatch-claim-route-boundary-v1`.

Follow subbundles in order. Do not skip gates. Do not introduce Process Core or driver APIs. Keep all new production helper code under `src/CanDoItAll.Modules.Processes/Automation/Dispatch`.

Preserve existing behavior. Prefer wrapper methods that delegate to new helpers instead of changing callers broadly.

Browser validation is N/A unless UI files unexpectedly change. Do not produce small, medium, mobile, phone, tablet, responsive, Android, or iPhone proof artifacts.

At each subbundle closure, record:
- source assertions,
- focused tests,
- build or scoped build,
- anti-stub scan,
- no-core/no-driver scan,
- no viewport proof drift.
