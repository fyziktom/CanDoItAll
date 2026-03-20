# ADR-001 — Use stdio transport for the MCP server

## Status
Accepted

## Context
The target use case is a local development orchestration server for the CanDoItAll solution, primarily consumed by Codex or another local MCP client.

## Decision
Use a **stdio MCP server** as the MVP transport.

## Rationale
- Best fit for local tooling integration
- Lower operational overhead than HTTP transport
- Simpler local setup
- Matches the intended client environment
- Avoids exposing a listening network service unnecessarily

## Consequences
Positive:
- easier bootstrap
- safer default local boundary
- fewer moving parts

Negative:
- stdout discipline becomes critical
- host logging must be routed away from stdout
- remote multi-client use is not the target

## Follow-up
Future HTTP/streamable transport is possible, but not required for MVP.
