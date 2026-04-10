# Normalized Requirements

1. The global processes workspace at `/processes` must load definitions on the first render when there are no route or query parameters.
2. The fix must preserve the current managed database profile flow and must not introduce token-based configuration for the Processes MCP server.
3. Definition list summaries must count roles and steps from one authoritative version per definition instead of aggregating across every version row in the database.
4. The repair must be validated with executable proof, not only source inspection. Required proof includes build or targeted tests plus real MCP and browser checks against the workspace.
5. The implementation must stay minimal and local to the UI/counting/database query defects already identified.
