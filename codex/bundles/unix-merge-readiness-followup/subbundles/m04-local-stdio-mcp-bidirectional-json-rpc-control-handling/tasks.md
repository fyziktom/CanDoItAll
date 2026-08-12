# Tasks

- [ ] Teach the response loop to recognize and answer peer `ping` requests before the awaited response.
- [ ] Return method-not-found or a typed protocol failure for unsupported peer requests according to the selected MCP contract.
- [ ] Keep a single serialized writer boundary for requests and peer responses.
- [ ] Bound line/message size, unmatched message count, JSON nesting/document size, stderr tail, and total operation timeout.
- [ ] Handle EOF/process exit/cancellation with typed redacted transport failures.
- [ ] Do not advertise callbacks/capabilities that are not implemented.
- [ ] Consider `notifications/cancelled` only if it can be added without broadening scope; it is not a merge blocker.
