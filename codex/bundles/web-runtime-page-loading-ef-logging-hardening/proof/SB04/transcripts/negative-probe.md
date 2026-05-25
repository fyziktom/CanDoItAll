# SB04 Negative Probe Transcript

- Invariant ID: `WEB-SB04-001`

Command:

```powershell
rg -n "await ExampleCatalogSeedService\.EnsureSeededAsync\(\)" src\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor.cs -S
```

ExitCode: 1

Output:

```text
No matches. Page initialization no longer calls example catalog seeding.
```
