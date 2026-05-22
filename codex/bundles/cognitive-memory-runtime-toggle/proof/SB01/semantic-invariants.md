# SB01 Semantic Invariants

- Invariant ID: `CM-SB01-001`
- Source raw note: `N003`, `N004`, and `N007`.
- Expected behavior: Cognitive Memory usage is a persisted runtime boolean setting exposed through settings service, API, and UI, with default `true`.
- Disallowed shallow implementation: UI-only or startup-only flags that do not persist or require an app restart.
- Failing-first test: N/A process because the raw runtime log is the failure artifact and SB01 provides infrastructure used by SB02.
- Passing test: `AutomationSettingsService_PersistsScheduleAndSourceOptions` saves `IsEnabled: false` and reloads it.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Settings/CognitiveMemorySettingsContracts.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Settings/CognitiveMemorySettingsServices.cs`, and `repo://src/CanDoItAll.Web/Api/CognitiveMemoryApi.cs`.
- Production assertions: new and existing settings rows default to enabled, explicit API/UI saves persist disabled state, and omitted API input preserves current state.
- Red-team negative case: a client omits `IsEnabled` during a settings update; the builder keeps the current setting instead of resetting to enabled.
- Downstream dependency check: SB02 guards read `ICognitiveMemoryAutomationSettingsService.GetAsync`, so the persisted setting is the runtime source of truth.
