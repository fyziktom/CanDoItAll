# CanDoItAll.Security.Abstractions

## Purpose

Provider-neutral contracts and typed identities for resolving runtime secret values.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Foundation/CanDoItAll.Security.Abstractions/CanDoItAll.Security.Abstractions.csproj
```

## Dependencies

The authoritative project dependency list is in [CanDoItAll.Security.Abstractions.csproj](CanDoItAll.Security.Abstractions.csproj).

## Architecture Notes

Consumers depend on these contracts without knowing the selected vault provider. Provider selection, protection, storage, and migration remain in the security module and composition root.

## Related Docs

- Repository overview: `README.md` at the repo root
- Security policy: `SECURITY.md` at the repo root
