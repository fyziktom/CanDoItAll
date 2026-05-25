# SB05 Negative Probe Transcript

- Invariant ID: `WEB-SB05-001`

Command:

```powershell
rg -n 'EnableSensitiveDataLogging\(|LogTo\(|Microsoft.EntityFrameworkCore.Database.Command.*Information' src\CanDoItAll.Web src\CanDoItAll.Infrastructure -S
```

ExitCode: 1

Output:

```text
No matches. The web and infrastructure startup area does not directly enable EF sensitive logging, LogTo console output, or Information-level EF command logging.
```
