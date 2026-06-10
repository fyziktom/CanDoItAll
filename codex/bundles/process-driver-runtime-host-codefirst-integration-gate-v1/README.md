# process-driver-runtime-host-codefirst-integration-gate-v1

## Status
Prepared for Codex implementation.

## Purpose
This bundle is a code-first follow-up after `process-driver-runtime-host-code-first-dryrun-execution-readiness-v1` and `process-driver-runtime-host-governance-sandbox-readiness-v1` showed that the roadmap is correct but the implementation ratio is still wrong.

The next implementation must make a significant source-level move toward a generic process-driver runtime host while still preventing premature execution-capable drivers.

## Main outcome
Move from a mostly module-local verification/dry-run host toward a stable generic runtime-host boundary with:

- durable EF audit and retention/query lifecycle proven in production DI,
- public/internal host status and manager readback surfaced through real process APIs/UI where applicable,
- scheduler/workflow read-only verification jobs executed through normal process services,
- dry-run execution contracts promoted out of ad-hoc module-local models into a stable contract boundary,
- static domain driver capability descriptors that do not self-register or reflectively discover anything,
- explicit sandbox/authorization/emergency-stop gates prepared for a future execution-capable host,
- real process-run/live OpenAI proof retained as a regression safety net.

## Code-first rule
This bundle must invert the current failure mode. Implementation must not spend most of the diff on `codex/bundles` files.

Required final ratio gate:

- `src/ + tests/ + docs/` changed lines must be at least **2x** `codex/bundles/` changed lines for this bundle execution.
- At least **6 production source files** and **4 test files** must receive meaningful changes.
- New bundle/proof artifacts must be concise: critical proof manifests only, no duplicated 79-line README boilerplate per subbundle, no report-only closure.
- If the ratio is not met, the bundle is **not complete**, even when tests pass.

## Non-goals
- No effectful command execution.
- No package restore through drivers.
- No Office/Graph/CRM calls through drivers.
- No workspace/storage/process mutation through drivers.
- No claim/transition/finalizer/retry mutation through drivers.
- No reflection discovery or fallback selector.
- No Process Core dependency on drivers, modules, infrastructure, UI, EF, MAF, OpenAI, workspace, or storage.
