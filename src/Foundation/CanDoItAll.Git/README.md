# CanDoItAll.Git

Provides validated Git repository paths, command construction, command execution
contracts, and a repository client for application features that need Git operations.

This project owns process invocation and Git-specific validation. Product workflow and UI
decisions remain in their owning modules.

Build from the repository root:

```powershell
dotnet build .\src\Foundation\CanDoItAll.Git\CanDoItAll.Git.csproj
```

The project file is the authoritative dependency contract.
