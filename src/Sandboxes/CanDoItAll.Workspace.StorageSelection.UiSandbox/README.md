# Storage selection UI sandbox

Run from the repository root:

```powershell
dotnet watch --project src/Sandboxes/CanDoItAll.Workspace.StorageSelection.UiSandbox --no-launch-profile --urls http://127.0.0.1:0
```

Use the loopback address printed by the host. The real production field/dialog renders
inside two independent parent fixtures. Choose representative, empty, large, failure or
partial-failure data, then Reset. Saved references include missing and disabled entries.
The parent controls preserve notes and explicit IDs across AllowAll/Disabled changes.
Hold reads and optionally ignore cancellation to exercise stale completion. The source
caps pending reads at four; release them explicitly. Reset advances the source context.

While a dialog is open, Alt+Shift+R releases one read, P resets the source, A retires the
primary parent, B opens the secondary parent and C recovers the failing source so Retry
can succeed. These are fixture controls, not production persistence or authorization.

The host needs no database, vault or driver. It links production parity CSS as content
and publishes the real neutral component assets without referencing Web. Source and
separately published Production journeys live in `StorageSelectionSandboxBrowserTests`.
See the [boundary record](../../../docs/architecture/workspace-storage-selection-ui-boundary.md).
