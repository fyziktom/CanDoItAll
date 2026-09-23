# CanDoItAll.Modules.Prompts.Contracts

## Purpose

Feature-owned public contracts of the Prompt Gallery: search queries and pages, item
details, drafts, save receipts, version snapshots, compatibility value types and the
pure compatibility evaluator, the Gallery enums, the `IPromptGalleryService` and
`IPromptGalleryImportService` ports, and the optional Curator launcher port. The types
keep the `CanDoItAll.Modules.Prompts` namespace, so existing consumers compile unchanged
and JSON payloads are identical; only the declaring assembly moved out of the module.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Modules/CanDoItAll.Modules.Prompts.Contracts/CanDoItAll.Modules.Prompts.Contracts.csproj
```

## Dependencies

The authoritative dependency list is in
[CanDoItAll.Modules.Prompts.Contracts.csproj](CanDoItAll.Modules.Prompts.Contracts.csproj).
It references only `CanDoItAll.SharedKernel`. It must not reference Infrastructure,
persistence, Web, or any module implementation.

## Architecture Notes

`CanDoItAll.Modules.Prompts` implements these contracts and owns validation, optimistic
concurrency, persistence, projections and activity effects. The lightweight
[Prompt Gallery rendering UI](../../UI/CanDoItAll.Prompts.UI/README.md) references this
project instead of the module so that its compile graph excludes persistence and runtime
dependencies. See [Prompt Gallery UI boundary](../../../docs/architecture/prompt-gallery-ui-boundary.md).

## Related Docs

- Module README: [CanDoItAll.Modules.Prompts](../CanDoItAll.Modules.Prompts/README.md)
- Current architecture: `docs/architecture/overview.md`
