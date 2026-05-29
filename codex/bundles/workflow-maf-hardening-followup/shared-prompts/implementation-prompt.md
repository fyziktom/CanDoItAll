Implement the currently selected follow-up subbundle only.

Before editing:
1. Confirm branch and commit.
2. Read this subbundle README and the normalized requirements.
3. Capture targeted failing-first tests where the subbundle expects a behavior change.
4. Keep changes minimal and aligned with the architecture.

During implementation:
- Keep the CanDoItAll canonical workflow model as the source of truth.
- Use MAF APIs through explicit adapter boundaries.
- Keep external effects disabled by default in tests.
- Redact secrets in all logs, events, exceptions, proof files, and UI messages.
- Do not introduce service locator execution patterns inside per-node runtime.

After implementation:
1. Run targeted tests.
2. Run the relevant build.
3. Add concise proof transcripts.
4. Update `reviews/01-execution-report.md`.
5. Stop if the progression gate cannot pass honestly.
