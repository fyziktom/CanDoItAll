# Prior stable-gate evidence

Source artifact:
`codex/bundles/Simple-Llm-Chats-Backend-Api/subbundles/SB11-final-regression-and-release-gate/proof/SB11/transcripts/02-stable-gate-failure.txt`

The original bundle ran exactly one stable Release solution command:

```powershell
dotnet test ./CanDoItAll.slnx --configuration Release --no-build --no-restore --artifacts-path ./artifacts/codex/simple-llm-chats/SB11 --filter "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined" /m:1 --logger "console;verbosity=minimal"
```

Result: exit code 1; 8,121 passed, 19 failed, 0 skipped, 8,140 total.

The companion classification artifact
`03-regression-repair-and-failure-classification.txt` records the four repaired Agent/Workflow API
regressions, the focused passes, and the seven failures that reproduced against baseline-owned sources.
Source inspection confirmed that the run-tracking theory contributed four distinct cases, so the
reconstructed inventory contains exactly 19 cases rather than 16 method names.

No broad test command was rerun in SB00.
