# Future QA Prompt

Review future Process rewrite implementation against `codex/bundles/process-module-architecture-v3`.

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
- .NET performance antipatterns in hot paths: sync-over-async, unbounded queues, allocation-heavy LINQ/projectors, uncached JSON options, per-call clients, sync file I/O, load-all UI queries, and unsealed leaf implementation classes where sealing is appropriate.

Require proof from tests, search output, architecture dependency checks, performance scan counts from `validation/05-dotnet-performance-antipattern-checklist.md`, and targeted negative cases. Build success alone is not enough.
