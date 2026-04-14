# Risk register and stop rules

## High risks

| Risk | Why it matters | Primary owner | Stop rule |
| --- | --- | --- | --- |
| Canonical dependency repair goes halfway | Downstream save/runtime/query work becomes untrustworthy | Subbundles 02-04 | Stop and reopen before any persistence refactor |
| Concurrency token design is provider-specific or incomplete | One provider will drift or conflicts will stay invisible | Subbundles 05-07 | Stop before migrations land broadly |
| Differential persistence changes semantics unexpectedly | Existing editor/runtime tests may silently become invalid | Subbundles 05-07 | Stop and add characterization coverage |
| Publish/version hardening leaves clone path coupled to legacy fields | Draft generation stays fragile | Subbundles 08-11 | Stop before runtime/query refactor continues |
| Runtime extraction becomes a new orchestration monolith | Testability does not improve | Subbundles 09-11 | Stop and revise split plan |
| Shared helper extraction becomes a dumping ground | Cross-module coupling worsens | Subbundles 12-15 | Stop and narrow the extraction |
| Workspace decomposition moves domain logic into UI | Architecture gets worse while files get smaller | Subbundles 13-15 | Stop and re-center logic in services/state |
| Final closure skips real browser proof | UI claims remain unverified | Subbundle 16 | Keep bundle open |

## Mandatory stop conditions

Stop immediately and create a corrective subbundle if any of the following are true:

- two dependency sources of truth still exist after subbundle 02,
- validation still mutates state after subbundle 03,
- save/publish/transition can still lose updates after subbundle 05,
- no-op save still changes unaffected child IDs after subbundle 06,
- publish/version uniqueness is still race-prone after subbundle 08,
- runtime transition logic is still effectively centralized in one large method after subbundle 09,
- list/analytics queries still load broad graphs without necessity after subbundle 10,
- duplicate helpers remain in active use after subbundle 12,
- `ProcessWorkspace` is still effectively one large state monolith after subbundle 13,
- migrations/snapshots are not coherent across both providers after subbundle 14,
- build/tests/browser proof are missing at closure.

## Risk treatment philosophy

Prefer a small, explicit corrective subbundle over undocumented compromise.
