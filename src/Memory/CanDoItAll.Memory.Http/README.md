# CanDoItAll.Memory.Http

Implements the HTTP Memory provider driver, configuration, URI and request construction,
header binding validation, bounded response reading, and result mapping.

Raw secrets are not stored in provider configuration. Endpoints, headers, timeouts, and
payloads are validated before invocation.

```powershell
dotnet build .\src\Memory\CanDoItAll.Memory.Http\CanDoItAll.Memory.Http.csproj
```
