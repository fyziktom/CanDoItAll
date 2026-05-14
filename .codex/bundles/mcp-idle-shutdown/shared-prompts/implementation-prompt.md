# Implementation Prompt

Implement only `subbundles/01-shared-idle-shutdown`.

Add a shared idle shutdown service in `CanDoItAll.Mcp.Core`, configure it from the Components and SshOps settings models, and mark activity through the centralized tool wrappers. Keep the change small and strongly typed. Do not add a fallback mechanism that hides lifecycle failures.

Required proof: targeted unit tests for the shared idle service, existing Components MCP tests, and MCP project builds.
