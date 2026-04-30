# Codex QA Prompt

Act as an independent reviewer. Verify the implementation without trusting the implementation report.

Check:

1. No tracked provider key pattern remains.
2. `01-execution-report.md` exists and every claimed file/test exists.
3. Structured output is preserved through approval continuations.
4. Required finalizer exact-once behavior is enforced before transcript persistence and run completion.
5. Built-in tool enabled/disabled configuration is respected.
6. Unknown `processes_*` tools are denied.
7. Process mutation tools are mutation-classified and approval-wrapped or denied.
8. Tool-thrown exceptions are not misclassified as policy blocks.
9. Recovery uses typed decisions and rework packets.
10. Proof reuse is fingerprint-based.
11. Retry ledger/backoff/loop control works.
12. Escalations and approvals are first-class and visible/actionable in UI.
13. Process UI allows structured operator rework instructions, approval decisions, and attempt/proof inspection.
14. Tests cover behavior, not just source string presence.

Produce a concise pass/fail report with evidence paths and test command results.
