# Implementation Prompt

Implement the current subbundle only.

Rules:

- Reuse existing services and boundaries first.
- Keep changes strongly typed and minimal.
- Fail explicitly on policy, lease, or validation problems.
- Add or update tests before calling the subbundle complete.
- Update `reviews/01-execution-report.md` while proof is fresh.
- If implementation reveals a missing foundation or stale bundle assumption, repair the bundle and rerun prepared-stage validation before continuing.
