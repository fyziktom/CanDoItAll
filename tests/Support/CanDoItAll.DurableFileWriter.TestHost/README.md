# CanDoItAll.DurableFileWriter.TestHost

Provides a dedicated child process for cross-process durable-file commit, coordination,
and interruption tests.

This executable is test infrastructure only. It must not become a production dependency or application entry point.

```powershell
dotnet build tests/Support/CanDoItAll.DurableFileWriter.TestHost/CanDoItAll.DurableFileWriter.TestHost.csproj
```
