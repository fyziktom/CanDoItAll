# CanDoItAll.Processes.Builder

Compiles validated process definitions into deterministic instance plans, resolves
builders, persists plan contracts, and computes plan hashes.

The builder does not execute steps or choose a provider. Runtime consumes the compiled
plan through typed contracts.

```powershell
dotnet build .\src\Processes\CanDoItAll.Processes.Builder\CanDoItAll.Processes.Builder.csproj
```
