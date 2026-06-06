# Test Impact Map

Required test categories:
- Build: `dotnet build CanDoItAll.slnx --no-restore`
- Focused unit projection architecture tests
- Focused integration projection tests
- Source scan no-core/no-driver/no-UI/no-stub
- Source-family order scan
- No single all-facet implementation scan
- No coordinator depends on broad host or `ProcessRunAutomationDispatchService`

Browser validation:
- N/A unless UI files unexpectedly change.
- Do not run small/medium/mobile/browser screenshot proof for this runtime refactor.
