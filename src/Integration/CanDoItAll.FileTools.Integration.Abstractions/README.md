# CanDoItAll.FileTools.Integration.Abstractions

Defines typed file-access, browse, known-file, semantic-scope, and project/process file
scope contracts used across the application.

The project contains no storage or desktop implementation. Consumers should depend on
these contracts instead of a concrete FileTools package.

```powershell
dotnet build .\src\Integration\CanDoItAll.FileTools.Integration.Abstractions\CanDoItAll.FileTools.Integration.Abstractions.csproj
```
