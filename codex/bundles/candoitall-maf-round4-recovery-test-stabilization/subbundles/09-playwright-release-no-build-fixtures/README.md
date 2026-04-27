# 09 — Playwright Release/no-build Fixtures


## Problem

Some Playwright fixtures launch `dotnet run --no-build` without `--configuration Release`, so Release/no-build test runs can try to launch Debug output.

## Tasks

1. Update `PlaywrightAppFixture.cs` and `DatabaseSwitchWorkbenchPlaywrightTests.cs` to pass the active build configuration.
2. Prefer a shared helper for launching the web app in tests.
3. Ensure ports are dynamically allocated or conflict-safe.
4. Ensure child processes are killed/disposed on test teardown.
5. Ensure Playwright tests are categorized as `Playwright` and excluded/included according to test policy.

## Acceptance criteria

- Playwright fixtures work under `dotnet test -c Release --no-build`.
- No fixture assumes Debug output unless Debug is explicitly requested.
- Playwright logs are captured for failures.

