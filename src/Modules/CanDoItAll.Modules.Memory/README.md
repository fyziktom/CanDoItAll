# CanDoItAll.Modules.Memory

The `/memory` route composes the shared Memory.UI workspace through Memory.Presentation.
This module retains the actual in-process owner facade, profile/transport mapping and
validation, current database-profile guard, executable capability checks, requester creation,
ledger projection and production provider UI registrations. Drivers, operation handlers,
workers and EF persistence remain in their existing `src/Memory` owners.

All seven tabs remain available. Shipped execution is unchanged: Mock/HTTP/NativeRemote
support synchronous query; configured MCP supports sync/async query and status. Cancellation,
manual ingestion, feedback execution and provider push acknowledgement remain refused, even
for imported manifest claims. No workers or default provider features are enabled here.

See [the boundary and validation record](../../../docs/architecture/memory-ui-boundary.md)
and [the backend-free sandbox](../../Sandboxes/CanDoItAll.Memory.UiSandbox/README.md).

```powershell
dotnet build src/Modules/CanDoItAll.Modules.Memory/CanDoItAll.Modules.Memory.csproj
```
