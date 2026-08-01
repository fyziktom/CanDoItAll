# CanDoItAll.FileTools.Integration

Implements authorized file content, storage browsing, download leases, known-file
sessions, save targets, and optional desktop launching.

Path scope, authorization, and file-size checks belong in this adapter. Product modules
consume the abstraction project and do not call external FileTools services directly.

```powershell
dotnet build .\src\Integration\CanDoItAll.FileTools.Integration\CanDoItAll.FileTools.Integration.csproj
```
