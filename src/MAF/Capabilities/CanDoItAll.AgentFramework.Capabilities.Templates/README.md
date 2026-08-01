# CanDoItAll.AgentFramework.Capabilities.Templates

Loads and validates capability template DTOs and compiles template access policies into
the typed capability model.

Runtime seed assets live under `Templates/Capabilities`; this project owns their schema
translation and validation, not the catalog persistence layer.

```powershell
dotnet build .\src\MAF\Capabilities\CanDoItAll.AgentFramework.Capabilities.Templates\CanDoItAll.AgentFramework.Capabilities.Templates.csproj
```
