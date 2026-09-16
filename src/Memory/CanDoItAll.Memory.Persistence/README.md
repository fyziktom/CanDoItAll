# CanDoItAll.Memory.Persistence

Implements EF Core stores for Memory operations, events, feedback, provider profiles,
source requests, retention, and worker leases.

`MemoryDbContext` maps only the seven Memory record types using the existing canonical
PostgreSQL table mappings. Application composition registers its pooled factory against
the immutable database profile for the current host. Standalone consumers of
`AddGenericMemoryModule` supply an `IDbContextFactory<MemoryDbContext>`.

The complete application migration model continues to include this assembly. Runtime
context isolation does not create another migration authority or change stored records.
Provider-profile GUID stamping retains the application behavior; worker lease tokens
retain their separate ownership semantics.

Persistence does not execute provider operations. Generic background workers remain
disabled unless explicitly enabled.

```powershell
dotnet build .\src\Memory\CanDoItAll.Memory.Persistence\CanDoItAll.Memory.Persistence.csproj
```
