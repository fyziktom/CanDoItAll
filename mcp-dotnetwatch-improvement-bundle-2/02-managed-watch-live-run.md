# Managed Watch Live Run

## Session

- logical app: `Bundle2ManagedBench2`
- session id: `app_add8c4bc624345e9b7c3707824e35ca8`
- URL: `http://127.0.0.1:5510/projects`
- file edited: `src/CanDoItAll.Modules.Projects/Pages/ProjectsPage.razor`
- probe text: `MCPBench25`

## Measurements

### Startup

- `lastStartUtc`: `2026-03-25T13:10:55.6905096Z`
- healthy returned at: `2026-03-25T13:11:57.2400071Z`
- startup to healthy: about `61.5s`

### Single simple edit

- baseline cursor: `149`
- file update log: `2026-03-25T13:12:33.7676374Z`
- hot reload succeeded log: `2026-03-25T13:12:50.3841441Z`
- file update to hot reload log: about `16.6s`
- `candoitall_app_wait(condition="RevisionConfirmed")`: satisfied in `13.712s`
- observed state after wait: `Running`
- health after wait: `Pending`
- browser/server-visible result: not visible in fetched `/projects` HTML within another `30s`

## Important Contradiction

The managed wait reported success:

- `RevisionConfirmed = true`
- `watch.pendingChange = false`
- `watch.summary = Hot reload succeeded`

But the changed page content still never appeared in the served HTML.

That means the current MCP wait model can declare success while the actual user-visible result is still stale.

## Why This Matters

If an agent trusts `RevisionConfirmed`, it can stack a second edit on top of an unverified first edit. That creates exactly the kind of overlapping rebuild confusion the user suspected.

## Artifact

The structured result is stored in `artifacts/mcp-managed-projects-page-live-run-2026-03-25.json`.
