# References

Tento seznam není normativní implementační závislost, ale slouží jako orientační zdroj pro implementaci a validaci.

## MCP / C# SDK
- Official MCP C# SDK repository: https://github.com/modelcontextprotocol/csharp-sdk
- MCP C# server docs: https://modelcontextprotocol.io/sdk/csharp/mcp-server
- Blog: Announcing ModelContextProtocol C# SDK v1.0: https://devblogs.microsoft.com/dotnet/announcing-modelcontextprotocol-csharp-sdk-v1-0/

## .NET tooling
- `dotnet watch` docs: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-watch
- ASP.NET Core hot reload docs: https://learn.microsoft.com/en-us/aspnet/core/test/hot-reload
- `dotnet test` docs: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test
- .NET 10 overview: https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview

## Existing related MCP project
- `jongalloway/dotnet-mcp`: https://github.com/jongalloway/dotnet-mcp

## Relevant issues and known caveats
- `dotnet/sdk` issue about build/test and active watch lock behavior: https://github.com/dotnet/sdk/issues/53092
- `dotnet/sdk` issue about `dotnet watch test` on .NET 10: https://github.com/dotnet/sdk/issues/52528

## What these references informed
- stdio host bootstrap and stdout discipline
- .NET 10 target choice
- `dotnet watch` non-interactive and environment flags
- `dotnet test` runner considerations in .NET 10
- why MVP intentionally avoids `dotnet watch test`
- why build/test need conflict-aware orchestration around running watch sessions
