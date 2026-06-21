# .NET implementation slice with atomic validation

**Key:** `dotnet-development-slice`
**Criticality:** High
**Autonomy level:** Guarded

Reusable child process for breaking a large implementation lane into intake, architecture check, optional solution setup subprocess, feature/function implementation subprocess, read-only validation proof, bounded repair subprocesses, repaired recheck, and handoff evidence. Full-app parent scopes are narrowed to one reviewable MVP behavior before the feature/function child subprocess starts. Product mutation is confined to nested implementation subprocesses; slice-level QA validates and routes repair.

## Steps
- Capture implementation slice boundary.
- Check architecture and source-of-truth impact.
- Prepare solution skeleton subprocess.
- Implement bounded code change through feature/function subprocess.
- Validate tests and targeted proof without product mutation.
- Hand off implementation slice.
