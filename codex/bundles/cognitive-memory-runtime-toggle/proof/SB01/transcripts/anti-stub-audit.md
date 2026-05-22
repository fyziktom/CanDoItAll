# SB01 Anti-Stub Audit Transcript

- Invariant ID: `CM-SB01-001`

Command:

```powershell
rg -n "Assert.False\(loaded\.IsEnabled\)|record\.IsEnabled = update\.IsEnabled|data-testid=\"cognitive-memory-usage-enabled\"" tests\CanDoItAll.Tests.Unit src\CanDoItAll.Modules.CognitiveMemory src\CanDoItAll.Web -S
```

ExitCode: 0

Output:

```text
tests\CanDoItAll.Tests.Unit\CognitiveMemoryOperationalSettingsTests.cs:44:        Assert.False(loaded.IsEnabled);
src\CanDoItAll.Modules.CognitiveMemory\Settings\CognitiveMemorySettingsServices.cs:54:        record.IsEnabled = update.IsEnabled;
src\CanDoItAll.Modules.CognitiveMemory\Pages\Components\CognitiveMemorySettingsTab.razor:16:                                                           data-testid="cognitive-memory-usage-enabled" />
```

Audit conclusion: no stub-only proof; the assertions cite persistence code, UI code, and a settings service test.
