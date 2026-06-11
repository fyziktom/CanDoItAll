# Runtime Stability Gaps

## Remaining functional blockers
1. Live OpenAI process-run smoke has not been run in the latest bundle. It must be run with explicit env variables:
   - `CANDOITALL_RUN_LIVE_PROCESS_RUN_VALIDATION=true`
   - `CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE=true`
   - `CANDOITALL_LIVE_PROCESS_RUN_OPENAI_MODEL=<safe configured model>`
   - `CANDOITALL_LIVE_PROCESS_RUN_TIMEOUT_SECONDS=180`
   - `CANDOITALL_LIVE_PROCESS_RUN_MAX_TOTAL_TOKENS=10000`

2. Final release decision currently says `not merge-ready` due to code-first ratio, while deterministic runtime evidence is green. This must be reclassified:
   - Functional runtime blocker
   - Live provider blocker
   - Churn/policy blocker
   - Non-blocking advisory metric

3. Existing manual-transition process tests should remain, but must be clearly named and excluded from automation proof. Automation proof must continue using launch/outbox/dispatch/finalizer path.

4. UI proof now covers completed run path, but final bundle should rerun the browser flow and confirm no regression.

## What must not happen
- Do not try to solve these final stabilization issues by extracting dispatcher or process runtime core into separate packages.
- Do not add domain-specific terms into Process Core.
- Do not introduce execution-capable drivers just because dry-run host contracts now exist.
