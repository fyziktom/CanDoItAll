# CanDoItAll.Modules.CognitiveMemory

## Purpose

Retained legacy native Cognitive Memory implementation. It remains for compatibility
and regression work while native-service ownership is completed; it is not part of the
active web-host composition.

## Prerequisites

This retained project restores its RAG and Semantic Completion driver dependencies from
NuGet.org; their sibling source repositories are not required.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Modules/CanDoItAll.Modules.CognitiveMemory/CanDoItAll.Modules.CognitiveMemory.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Modules.CognitiveMemory.csproj](CanDoItAll.Modules.CognitiveMemory.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

Do not add new base-host behavior here. The active application uses the provider-neutral
projects under `src/Memory`, the `CanDoItAll.Modules.Memory` UI module, and the MAF
Memory adapter. Native Cognitive Memory behavior belongs in the separately maintained
service repository and integrates through an explicitly enabled remote provider.

This project is intentionally excluded from both repository solution files and active
module discovery. Its presence must not be used as proof that the base host exposes the
former native API or depends on Qdrant.

## Related Docs

- Cognitive Memory docs: `docs/cognitive-memory/README.md`
- Development runtime: `docs/development-runtime.md`
- Current architecture: `docs/architecture-beta.md`
