You are implementing the Zyphonote repository versioning feature inside the current PHP 8.2 + MariaDB repo.

Before changing code:

1. Read:
- `README.md`
- `CURRENT_REPO_ALIGNMENT.md`
- `SPEC/01-executive-summary.md`
- `SPEC/02-current-state-findings.md`
- `SPEC/03-target-architecture.md`
- `SPEC/12-migration-plan.md`
- `CHECKLISTS/00-master-checklist.md`

2. Inspect the current repo and produce a file-by-file implementation plan mapped to:
- `src/lib/*`
- `src/api/v1/index.php`
- `src/account-*.php`
- `src/assets/js/*`
- `src/db/migrations/*`
- `tools/*`
- `TESTS/*`

3. Explicitly confirm in your plan:
- scores and playlists are currently not truly content-addressed
- learning packages are already close to the target storage model
- events need repository history from scratch
- legacy entity/version tables will stay as compatibility bridges during migration

Do not start coding until the plan is concrete.
