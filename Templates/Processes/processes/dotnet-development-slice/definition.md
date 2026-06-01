# .NET implementation slice with atomic validation

**Key:** `dotnet-development-slice`
**Criticality:** High
**Autonomy level:** Guarded

Reusable child process for breaking a large implementation lane into intake, architecture check, optional solution setup subprocess, feature/function implementation subprocess, read-only validation proof, and handoff evidence. Product mutation is confined to nested implementation subprocesses; slice-level QA validates and routes repair.

## Steps
- Capture implementation slice boundary.
- Check architecture and source-of-truth impact.
- Prepare solution skeleton subprocess.
- Implement bounded code change through feature/function subprocess.
- Validate tests and targeted proof without product mutation.
- Hand off implementation slice.
