# Stop rules

Stop immediately and open a corrective subbundle if any of the following is true:

1. A core Process type still carries both legacy scalar dependency meaning and canonical dependency collection meaning.
2. A gate is answered with anything weaker than an explicit “yes” to all review questions.
3. Process schema work leaves representative child/runtime rows insertable without valid parents.
4. Dependency uniqueness still relies only on the nullable triple unique index.
5. Draft/published singularity still depends on ordering logic rather than a DB-backed invariant.
6. `ActivePublishedVersionId` can still reference an invalid or foreign version without DB rejection or explicit guard.
7. Version allocation still uses `MAX + 1`.
8. A Process command still performs activity/search work as a fragile direct post-commit call.
9. The execution report claims a suite ran but the `.trx` artifacts do not show it.
10. Structural cleanup begins to reopen already-closed invariants.

When a stop rule fires:
- create the corrective subbundle;
- update `codex/MASTER_TASKS.json`, `codex/TASKS.json`, and the gate memo log;
- complete the corrective work and rerun the failed gate before any downstream task resumes.
