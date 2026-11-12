# CanDoItAll.Components.Git

Retains the public `GitComponentsAssemblyMarker` assembly identity over `CanDoItAll.Git`.
The current project contains no Razor or C# renderer and has no product visual activation.
Its backend reference is compatibility scaffolding, not a reusable rendering dependency.

Reusable general-purpose Git UI belongs in the shared Components repository; product
orchestration belongs in an owning module.

```powershell
dotnet build .\src\UI\CanDoItAll.Components.Git\CanDoItAll.Components.Git.csproj
```
