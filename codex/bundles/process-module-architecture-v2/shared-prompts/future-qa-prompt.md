# Future QA Prompt

Review future Process rewrite implementation against `codex/bundles/process-module-architecture-v2`.

Prioritize:

- forbidden dependency violations,
- domain vocabulary leaks in core/runtime/builder,
- old dispatcher coupling,
- missing strategy binding snapshots,
- runtime transition shortcuts,
- dispatcher lease/idempotency holes,
- artifact ledger gaps,
- raw diagnostic exposure,
- template migration gaps,
- event/projection time-window mistakes,
- UI reads of runtime internals.

Require proof from tests, search output, architecture dependency checks, and targeted negative cases. Build success alone is not enough.
