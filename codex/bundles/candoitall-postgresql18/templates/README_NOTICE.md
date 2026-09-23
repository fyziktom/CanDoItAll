# Proposed documentation changes

Adapt this wording to the final implementation. Do not claim the migration has been exercised until it actually has. Publish the runbook before adding its links.

## Root README notice

Place a visible notice before the installation and quick-start commands, not only deep in the operations documentation. Keep the existing unrelated alpha portability notice.

Suggested root README text:

```markdown
> **PostgreSQL 18 update — existing developer installations:** If you installed
> CanDoItAll before this PostgreSQL 18 change and want to keep your existing data,
> give [the developer migration guide](tools/dev/Migrate-PostgreSql16To18.md) to
> your coding agent and have it complete the migration **before** rerunning the
> new database installer or replacing the database container. The installer does
> not automatically upgrade older PostgreSQL data. The guide covers identifying
> your real database, preserving its data and matching application files/keys,
> restoring into PostgreSQL 18, verification, and the supported API fallback.
> Starting with an empty database is an explicit choice for disposable data,
> not the default migration path. Never point PostgreSQL 18 at an older major's
> data directory or remove your volumes just to make startup succeed.
```

Change the active PostgreSQL requirement to describe PostgreSQL 18 as the provisioned and tested baseline. Do not claim broader server-version support unless it is actually validated, and do not add an unnecessary runtime version lock just to match the documentation.

Do not require a historical checkout, exact starting commit or specific feature-branch name. Keep the notice relative to the real PostgreSQL 18 adoption change, not this handoff's review date. “Before this PostgreSQL 18 change” remains valid when the owner branches later or the merge boundary is not yet known. An optional release note can name the eventual actual release after it exists; this must never become a migration prerequisite.

## Tools index

Suggested addition to `tools/README.md`, near the installed-web-app and development database guidance:

```markdown
### Existing developer database migration

Before updating an older installation while preserving data, have your coding
agent follow [PostgreSQL 16 to 18 data preservation](dev/Migrate-PostgreSql16To18.md).
This is a one-time operator procedure, not an automatic installer upgrade. The
procedure distinguishes development Compose from the separately managed Windows
installation and verifies the restored data before switching the application.
```

## Existing operations guides

Link the same canonical document from the installed Windows, development container, and backup/restore upgrade sections where relevant. From a file in `docs/operations`, its relative link is `../../tools/dev/Migrate-PostgreSql16To18.md`. Keep these additions short; do not duplicate the entire procedure. Update stale version/layout examples that are intended to describe current behavior.

State explicitly that an old ignored `.env` can override a new Compose default. Correct any relevant backup wording that dumps the database while application files are still being modified: the final recoverable set requires quiescing writers before its database and file capture.
