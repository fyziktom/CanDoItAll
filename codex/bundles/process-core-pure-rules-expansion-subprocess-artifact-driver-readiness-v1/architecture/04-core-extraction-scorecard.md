# Core Extraction Scorecard

| Area | Decision | Evidence |
| --- | --- | --- |
| Route rules | Keep in Core | Existing routing seed preserved and architecture tests passed. |
| Subprocess lifecycle rules | Keep in Core | Parent status/reason facts moved to pure Core rules with module transition-request adapter. |
| Subprocess artifact mapping | Keep in Core | Child expectation mapping and eligibility decisions moved to pure Core resolver with module entity adapters. |
| Artifact expectation snapshots | Keep in Core | Core-owned artifact enum/read-model snapshots added; module entities are translated at adapter edges. |
| Artifact matching/satisfaction descriptors | Keep in Core | Strong-match disambiguation and recorded-satisfaction id checks moved to pure Core rules. |
| Projection persistence | Keep module-local | EF, claim guard, projection writers, gap journals, and storage remain in `CanDoItAll.Modules.Processes`. |
| Validation orchestration | Keep module-local | Dispatcher validation flow still owns execution detail access, projection writes, and finalizer interaction. |
| Process helper drivers | Keep docs/tests-only | No production driver API, registry, DI registration, runtime selector, or manager command was introduced. |

## Decision
Continue narrow Process Core extraction only for pure deterministic read models and rules. Do not start broad runtime/service extraction yet.
