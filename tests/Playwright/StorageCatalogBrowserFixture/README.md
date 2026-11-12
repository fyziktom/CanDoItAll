# Storage catalog browser fixture

Runs the real Web entry point with task-owned PostgreSQL, file roots and loopback port.
Only the API administration access checker is controlled by standard-input commands for
current-denial and authorized-retry proof. API owners, rendering and storage remain real;
secured HTTP authorization is tested separately. No production fault endpoint is added.

Build through `tests/Playwright/CanDoItAll.Tests.Playwright` and run its focused
`WorkspaceApiDenialBrowserTests` or Storage catalog browser selections after discovery.
Set the isolated PostgreSQL test connection described in [Testing](../../../docs/testing.md).
The fixture host owns and removes its child process, database profile and private files.
