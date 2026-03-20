# Self-review prompt

Review the implementation like a strict senior engineer and QA lead.

## Audit questions
1. Does anything write to stdout that should not?
2. Can the client still bypass the intended lifecycle in normal documented usage?
3. Are mutation operations serialized correctly?
4. Can any path escape the workspace boundary?
5. Could a stale cleanup kill the wrong process?
6. Is log redaction sufficient for common secret patterns?
7. Are waits deterministic and evidence-rich on timeout?
8. Does build/test orchestration actually resume the app when policy says so?
9. Is `dotnet watch test` absent from MVP runtime code paths?
10. Are comments in source code all in English?
11. Are the docs and tool contracts still aligned with the code?
12. Which remaining risks are real blockers and which are acceptable follow-ups?

## Output format
Produce:
- blockers
- important non-blockers
- minor cleanups
- exact file-level remediation suggestions
