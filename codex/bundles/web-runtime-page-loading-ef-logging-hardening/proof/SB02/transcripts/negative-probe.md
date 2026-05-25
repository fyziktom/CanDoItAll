# SB02 Negative Probe Transcript

- Invariant ID: `WEB-SB02-001`

Command:

```powershell
rg -n "await LoadExecutorOptionsAsync\(\);\r?\n\s*await LoadWorkflowOptionsAsync\(\);\r?\n\s*await LoadManagerAgentOptionsAsync\(\);\r?\n\s*await LoadPartyOptionsAsync\(\);\r?\n\s*await LoadProcessAnalyticsAsync\(\);\r?\n\s*await LoadImprovementSuggestionsAsync\(\);" -U src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Loading.cs
```

ExitCode: 1

Output:

```text
No matches. The old initial-load eager sequence is absent.
```
