# Shared Implementation Prompt

```text
Execute only the named Simple-Llm-Chats-Backend-Completion subbundle.

Before editing:
1. Read the root README, current execution report, the active subbundle, normalized requirements, architecture decisions, phase plan, and all prerequisite proof.
2. Record Git/application/sibling commits and dirty files. Stop on unclassified drift or a failed prerequisite.
3. Reserve exact changed-file ownership; implementation subbundles are serialized because their contracts and files overlap.
4. Run the active filter with --list-tests and record expected versus actual discovery before the failing-first execution.

During implementation:
- Make the smallest change inside the existing owner and preserve strong typing, explicit stable errors, bounded work, safe logging, PostgreSQL authority, exact scopes, server-owned origin, and ordinary-chat isolation.
- Use deterministic barriers/fake time and real Web/PostgreSQL boundaries where required.
- Build every changed production project directly in Release.
- Do not add UI, partial classes, new projects/interfaces, silent retry/fallback, inline HTTP provider execution, in-memory shadow queues, or raw exception logging.
- If a change requires a new dependency/project/public abstraction or contradicts a locked decision, stop and reopen the architecture checkpoint.

Before closure:
- Re-list and run the exact focused tests; zero or unexpected discovery fails.
- Capture positive and meaningful negative behavior plus durable/API/log state, not only counts.
- Apply invalidation/reopen rules, update reviews/01-execution-report.md and traceability, and create the declared proof artifacts.
- For Governed work, include portable paths, hashes, transcripts, semantic invariants, anti-stub evidence, and independent review.
- Do not run the broad Stable gate before SB10.
```
