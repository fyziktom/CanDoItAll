# Builds and static checks

- Run label: `SB04-BUILD-001`
- Working directory: `repo://`
- Commands:
  - `dotnet build src/Modules/CanDoItAll.Modules.LlmChats/CanDoItAll.Modules.LlmChats.csproj --no-restore -nologo -v:minimal`
  - `dotnet build src/Modules/CanDoItAll.Modules.LlmChats.Persistence/CanDoItAll.Modules.LlmChats.Persistence.csproj --no-restore -nologo -v:minimal`
  - `dotnet build src/App/CanDoItAll.Web/CanDoItAll.Web.csproj --no-restore -nologo -v:minimal`
  - `git diff --check`
- Exit codes: `0`, `0`, `0`, `0`

```text
CanDoItAll.Modules.LlmChats: build succeeded, 0 warnings, 0 errors.
CanDoItAll.Modules.LlmChats.Persistence: build succeeded, 0 warnings, 0 errors.
CanDoItAll.Web: build succeeded, 0 warnings, 0 errors.
git diff --check: no output.
```

Invariant IDs: `SB04-INV-01`, `SB04-INV-05`.
