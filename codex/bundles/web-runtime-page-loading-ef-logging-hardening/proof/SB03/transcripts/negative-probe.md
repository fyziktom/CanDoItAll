# SB03 Negative Probe Transcript

- Invariant ID: `WEB-SB03-001`

Command:

```powershell
rg -n "await CreateObjectAsync\(definition, request\);\r?\n\s*await ReloadSurfaceAsync" -U src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor
```

ExitCode: 1

Output:

```text
No matches. The old create-then-reload sequence is absent from the normal create flow.
```
