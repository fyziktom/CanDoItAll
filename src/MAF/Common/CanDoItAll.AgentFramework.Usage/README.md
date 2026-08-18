# CanDoItAll.AgentFramework.Usage

## Purpose

Read-only provider-usage and cost reporting contracts shared by Agent Chat and Simple Chats. The
project projects recorded invocation evidence into strongly typed summaries without owning provider
execution, pricing capture, or persistence mutations.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework: `net10.0`
- Validation command:

```powershell
dotnet build src/MAF/Common/CanDoItAll.AgentFramework.Usage/CanDoItAll.AgentFramework.Usage.csproj
```

## Boundaries

The project owns usage query contracts, scope selection, grouping, pagination, and aggregate response
models. Its only project dependency is `CanDoItAll.AgentFramework.Models`, which provides the persisted
invocation evidence read by the query service.

It does not invoke providers, estimate missing prices, mutate execution records, or render UI. Product
modules and API endpoints choose an authorized scope and present the resulting projections.

## Related Docs

- [LLM Chats Product And API](../../../../docs/llm-chats-api.md)
- [Module Architecture](../../../../docs/architecture/modules.md)
