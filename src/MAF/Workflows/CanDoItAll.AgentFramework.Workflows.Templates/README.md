# CanDoItAll.AgentFramework.Workflows.Templates

Loads, validates, serializes, and materializes workflow template packs from the
repository-owned `Templates/Workflows` inputs.

Template files are application seed inputs. This project translates them into validated
domain definitions and does not maintain generated documentation sidecars.

```powershell
dotnet build .\src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.Templates\CanDoItAll.AgentFramework.Workflows.Templates.csproj
```
