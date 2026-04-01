# Implementation Prompt

```text
You are implementing the CanDoItAll runtime database switching bundle.

Rules:
1. Work one subbundle at a time. Do not skip ahead.
2. Before editing code, read:
   - README.md
   - plan/01-phase-plan.md
   - requirements/01-normalized-requirements.md
   - traceability/01-requirement-traceability.md
   - the current subbundle README
   - reviews/01-execution-report.md
3. Treat subbundles 02–06 as critical foundations. Do not start 07 or 08 until their progression gates are actually satisfied.
4. Never claim “done” without proof.
5. If a command, browser pass, PostgreSQL dependency, or IPFS dependency cannot run, mark the subbundle `Blocked` and record the missing dependency in the execution report.
6. Do not silently narrow scope. If a requested note cannot be closed exactly, record the exception explicitly in the execution report and reopen the owning subbundle if needed.
7. Update `reviews/01-execution-report.md` during execution:
   - Status
   - Commands
   - Browser artifacts
   - Subbundle Gate Results
   - Browser Validation Analytics
   - Raw Note Closure
8. When a subbundle changes UI:
   - run a large-screen browser pass first
   - capture screenshots
   - answer the screenshot review questions from the subbundle README
   - run a narrower-width follow-up when layout can wrap or collapse
9. When a subbundle changes data shape, startup bootstrap, or switching behavior:
   - run unit tests
   - run integration tests
   - rerun any dependent component/browser suites listed in the subbundle README
10. If later proof shows an earlier critical foundation is weak, reopen the earlier subbundle instead of explaining the weakness away.

Execution contract:
- Do the work only for the current subbundle.
- Run the proof listed under “Proof Required”.
- Record evidence paths and command outcomes honestly.
- Then and only then decide whether the current subbundle is `Completed` or `Blocked`.

Required anti-fake checks:
- Do not leave `EnsureCreatedAsync()` as the normal schema path and still claim migrations are done.
- Do not leave `/managed-files` bound to a fixed startup file provider and still claim runtime switching is done.
- Do not leave the workbench local-storage key global and still claim DB isolation is done.
- Do not claim PostgreSQL support without real PostgreSQL automated proof.
- Do not claim runtime switching complete unless a stale artifact route has been proven safe after switching.
- Do not claim clone/snapshot complete unless storage files were included and verified.
- Do not mark blocked proof as success.

Before closing the subbundle, run through templates/02-stop-the-line-checklist.md.
```
