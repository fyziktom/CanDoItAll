# CanDoItAll.AgentFramework.Skills

Implements skill descriptors, loading, registration, and setup diagnostics over the
provider-neutral skill contracts.

Default application skill inputs are maintained under `Templates/Capabilities`. Runtime
loading must fail explicitly for missing or invalid assets.

```powershell
dotnet build .\src\MAF\Skills\CanDoItAll.AgentFramework.Skills\CanDoItAll.AgentFramework.Skills.csproj
```
