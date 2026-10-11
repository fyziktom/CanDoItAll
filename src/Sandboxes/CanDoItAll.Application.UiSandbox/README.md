# Application scenarios

Run from the repository root:

```powershell
dotnet watch --project src/Sandboxes/CanDoItAll.Application.UiSandbox run --configuration Release --no-launch-profile --urls http://127.0.0.1:58745
```

The host has no Web, Composition, database or module registration. `/dashboard` includes
loading, empty, populated, partially priced, stale and failed snapshots with real controls.
`/runtime` includes ready, misconfigured, unavailable, unsupported and unverified capability
states with real fact children. Scenario refreshes are synthetic; native owner tests and
browser journeys prove the actual effects separately.

The host consumes the application's generated Tailwind asset and BaseLib's static assets.
`Tailwind/input.css` scans `src`, including the moved renderers. Build the standard application
theme first when that generated asset is absent. The explicit asset check fails when it is
missing instead of silently substituting a different visual baseline.
