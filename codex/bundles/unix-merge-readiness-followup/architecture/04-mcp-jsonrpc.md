# Local stdio MCP JSON-RPC hardening

The connection currently serializes client requests and reads until a matching response ID. The receive loop must also process bounded peer control traffic.

## Required behavior

- Matching response → return to caller.
- Notification → ignore or route through an explicitly bounded handler.
- Peer request `ping` with ID → immediately return an empty result and continue waiting.
- Unsupported peer request → return JSON-RPC `-32601 Method not found` or terminate with a typed protocol error according to the selected MCP contract.
- Invalid JSON, duplicate/invalid IDs, excessive unmatched messages, or overlong lines → typed bounded failure.
- EOF or process exit → typed transport failure with redacted stderr tail.

Do not advertise capabilities that are not implemented. Do not introduce concurrent writes without a single serialized writer boundary.
