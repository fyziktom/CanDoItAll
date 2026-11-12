# Storage Recovery sandbox

Run `dotnet run --project src/Sandboxes/CanDoItAll.Workspace.StorageRecovery.UiSandbox` from the repository root.
Build the existing parity theme with `npm run tailwind:build` first when it is missing.
Choose a scenario and open Recovery. Both feeds, exact detail, guarded actions and returned results use the production renderer and session.

The source is a bounded simulation. It registers no database, driver, credentials, model or persistent recovery owner.
An external-termination checkbox here is a demonstration, never evidence that a real transfer stopped.
Loading exposes a controlled delayed read; closing retires its session. Refresh failure retains the simulated acknowledged result.
Close the modal to change the scenario or permit observation retry, then reopen it explicitly.

Publish with `dotnet publish src/Sandboxes/CanDoItAll.Workspace.StorageRecovery.UiSandbox -c Release`.
The published application runs independently in Production with the same bundled component assets and parity CSS.
