# QA Prompt

```text
Validate the assigned subbundle from C:\repositories\CanDoItAll\codex\bundles\plugin-runtime-governance-docker-refactor.

Focus areas:
- Permission grants are not confused with plugin installation or enabled state.
- Undeclared or ungranted capabilities fail predictably.
- Host-tool recipes do not allow arbitrary shell, arbitrary PowerShell, arbitrary Docker flags, or inherited secrets.
- Docker log output is bounded and stored as artifact content when large.
- EF queries use projection, AsNoTracking for reads, indexes, and paging where list sizes can grow.
- Browser-visible permission settings and workflow warnings have actual Playwright evidence.
- Execution report rows are complete and honest.

Return findings first, ordered by severity, with exact file paths and minimal fix guidance.
```
