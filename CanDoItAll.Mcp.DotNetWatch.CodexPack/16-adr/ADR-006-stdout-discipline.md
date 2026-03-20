# ADR-006 — Enforce stdout discipline strictly

## Status
Accepted

## Context
In a stdio MCP server, stdout is reserved for protocol traffic. Any accidental text logging can corrupt the transport.

## Decision
The server must never write non-protocol data to stdout.

## Rationale
- protocol correctness
- client stability
- predictable debugging

## Consequences
Positive:
- reliable MCP communication

Negative:
- developers must be careful with logging and `Console.WriteLine`

## Enforcement
- logging to stderr and/or file only
- code review rule
- integration test for stdout cleanliness

## Follow-up
Keep this as a release-blocking invariant.
