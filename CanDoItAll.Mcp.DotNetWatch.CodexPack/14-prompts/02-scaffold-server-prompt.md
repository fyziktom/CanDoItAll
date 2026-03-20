# Scaffold server prompt

Based on the repo discovery results, scaffold the MCP server project inside the CanDoItAll solution.

## Implement now
- add `src/CanDoItAll.Mcp.DotNetWatch`
- target `net10.0`
- add the official MCP C# SDK package(s)
- set up `Program.cs` with `Host.CreateEmptyApplicationBuilder(settings: null)`
- register stdio transport
- register tool discovery
- add typed options binding and validation
- add stderr/file logging only
- add a minimal `candoitall_workspace_info` tool
- add unit test and integration test projects to the solution

## Constraints
- No non-protocol stdout output.
- Keep comments in English.
- Do not over-implement app lifecycle yet.
- The output should build cleanly.

## Deliver
- changed files
- build result
- a short note about how stdout safety is enforced
- the next implementation slice
