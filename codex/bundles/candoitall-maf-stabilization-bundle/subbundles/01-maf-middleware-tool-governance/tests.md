# Test Plan: 01 - MAF Middleware and Tool Governance Hardening


Unit tests:
- Disabled built-in tool configuration prevents tool attachment.
- Read-only workspace tools are allowed.
- Write/mutation workspace tools require approval or are denied when approval is unavailable.
- A repeated identical mutation tool call is blocked before tool execution.
- MCP tools outside `AllowedTools` are denied.
- Policy logs contain tool name, decision, agent id/run id, and no secrets.

Integration or component tests:
- A process run with auto-approved tools still executes approved workspace writes.
- A run without approval permission receives a pending approval or policy denial instead of executing a mutation.
- Existing calculator/process mock flow still passes.


## Minimum validation commands

Use the repository's actual test projects. At minimum attempt:

```bash
dotnet build CanDoItAll.slnx --no-restore
dotnet test CanDoItAll.slnx --no-build
```

If full-solution tests are too slow or environment-limited, run focused tests and explain exactly what was not run.
