# PostgreSQL 18 — revised Codex handoff

**Revision 2 · reviewed 2026-09-23 · single long-running engineering assignment**

This package supersedes the earlier PostgreSQL 18 handoff. The owner will create a new branch from `development` before execution. Work on **that already checked-out branch**, whatever its name and current commit are. No historical commit, exact branch name, or snapshot-ancestry check is an execution prerequisite. Do not switch to `development`, reset, rebase, merge, or recreate the owner's branch to match this review.

## Prompt to give the coding agent

```text
Execute CODEX_TASK.md from this package as one complete engineering assignment.

Work in the CanDoItAll feature branch I have already created from development and
currently checked out. Its current local source is the implementation authority.
Do not switch branches, reset to a review snapshot, require a specific starting
commit, or reject newer work because it differs from the handoff. Read the current
repository instructions, discover the current code and test/dependency contracts,
and adapt the implementation to changes made since this package was prepared.
Record the actual starting branch/HEAD only as provenance, not as a required pin.

Implement PostgreSQL 18 adoption throughout installation, the generated launcher,
Compose, development tools, CI and relevant tests. Publish the agent-operated
migration guide in tools/dev and link it prominently before the root README's
installation actions. Keep this simple: no automatic major-upgrade framework and
no workflow bundles. Preserve existing safety checks and all newer product fixes.

On this workstation also preserve and migrate the existing database actually used
by our development web application on port 5032. Discover its real database and
configuration; 5032 is the web port, not the PostgreSQL port. Take a coherent,
restorable database/files/keys backup, prefer a complete dump/restore into a
separate PostgreSQL 18 cluster, and prove the effective runtime target and existing
data after restart. Do not discard this instance's data. If direct migration is
blocked, investigate and attempt a genuinely supported CanDoItAll API route,
without inventing endpoints, weakening authorization, or calling a partial copy a
complete migration. Retain the source and report precise remaining blockers.

Use your own long-task plan and finish implementation, validation and the actual
5032 migration. Preserve unrelated changes, sibling-source contracts and Git
signing. Keep all repository text and comments in English. Do not push, merge or
publish as a side effect. Report code, validation and 5032 migration separately.
```

## Package contents

| File | Use |
|---|---|
| [CODEX_TASK.md](CODEX_TASK.md) | Authoritative scope, implementation requirements and workstation preservation outcome. |
| [REVISION_NOTES.md](REVISION_NOTES.md) | What changed since the previous handoff and why. |
| [references/RECHECK_AND_SOURCES.md](references/RECHECK_AND_SOURCES.md) | Current repository observations, official technical sources and uncertainty boundaries. |
| [references/VALIDATION_MAP.md](references/VALIDATION_MAP.md) | Current test topics and evidence expectations; discover actual names/counts at execution. |
| [templates/Migrate-PostgreSql16To18.md](templates/Migrate-PostgreSql16To18.md) | Draft canonical agent runbook to adapt and exercise before publishing under tools/dev. |
| [templates/README_NOTICE.md](templates/README_NOTICE.md) | README/tools/operations notice wording. |
| [templates/MIGRATION_REPORT.md](templates/MIGRATION_REPORT.md) | Unfilled evidence template; not a workflow or claimed test result. |
| [references/REVIEW_METADATA.json](references/REVIEW_METADATA.json) | Informational audit provenance only; never use it as a checkout or acceptance constraint. |
| [SHA256SUMS.txt](SHA256SUMS.txt) | Integrity hashes for every other package file. |

The reviewed stable release is PostgreSQL **18.6**. Recheck the current stable 18.x patch and obtain real distribution hashes at execution. This package contains no verified replacement binary hashes, database backups, secrets or executable migration script.

The current checkout, running 5032 deployment, installed launcher and database are separate things to identify. A branch switch does not migrate any of them. New code may already implement part of this task: inspect and retain it rather than duplicating it or reconstructing an older design.

The main-repository branch freedom does not remove legitimate distribution integrity pins or CI's once-per-run resolution of dependency source commits. Preserve those contracts without hardcoding this review's application commit.
