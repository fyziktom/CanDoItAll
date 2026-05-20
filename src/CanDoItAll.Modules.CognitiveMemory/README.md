# CanDoItAll.Modules.CognitiveMemory

## Purpose

Product module for Cognitive Memory: evidence-backed memory records, workspace attention, source ingestion, projection to vector storage, recall traces, review queues, dream/quality workflows, procedural skill memory, curator conversation, probe flows, and self-regulation services.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.Modules.CognitiveMemory/CanDoItAll.Modules.CognitiveMemory.csproj
```

## References

Project references:

- `../CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj`
- `../CanDoItAll.AgentFramework.Core/CanDoItAll.AgentFramework.Core.csproj`
- `../CanDoItAll.AgentFramework.Voice/CanDoItAll.AgentFramework.Voice.csproj`
- `../CanDoItAll.Components.BaseLib/CanDoItAll.Components.BaseLib.csproj`
- `../CanDoItAll.Components.Common/CanDoItAll.Components.Common.csproj`
- `../../../CanDoItAll.AgentFramework.Rag/src/CanDoItAll.AgentFramework.Rag.Driver/CanDoItAll.AgentFramework.Rag.Driver.csproj`
- `../../../CanDoItAll.AgentFramework.SemanticCompletion/src/CanDoItAll.AgentFramework.SemanticCompletion.Driver/CanDoItAll.AgentFramework.SemanticCompletion.Driver.csproj`
- `../CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj`
- `../CanDoItAll.SharedKernel/CanDoItAll.SharedKernel.csproj`

Framework references:

- None

Direct package references:

- `Microsoft.AspNetCore.Components.Web (10.0.5)`
- `PdfPig (0.1.14)`

## Architecture Notes

This module owns Cognitive Memory product semantics. Keep durable records, recall scoring, source evidence, review decisions, projection state, and policy checks inside the module. Qdrant is a projection target through the RAG driver; PostgreSQL remains the durable AppDbContext profile.

Do not let Cognitive Memory mutate source systems directly. Workbench, process, workflow, and external source inputs should be ingested through explicit source snapshot and ingestion services, then linked back with evidence anchors and policy metadata.

## Related Docs

- Cognitive Memory docs: `docs/cognitive-memory/README.md`
- Development runtime: `docs/development-runtime.md`
- Current architecture: `docs/architecture-beta.md`
