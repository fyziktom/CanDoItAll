# ADR-0003: Use Typed Project-Node References Across Module Boundaries

## Status

Accepted

## Context

The Workbench to CRM/HR bridge used raw `NodeKey` strings for node-scoped assignment operations. That contract leaked low-signal primitives across the module boundary and made it too easy to pass unrelated strings without expressing intent.

## Decision

- Cross-module node-scoped assignment operations must use `ProjectNodeReference` instead of raw `string` parameters.
- The typed reference remains a thin value object in this wave; it does not force a database migration or a full Workbench model split.
- String-based node ids may still exist internally inside Workbench persistence, but the external boundary should state node-reference intent explicitly.

## Consequences

- New bridge methods should accept `ProjectNodeReference` whenever they target a specific structure node.
- Additional node-reference semantics can evolve behind the typed wrapper without rewriting every call site again.
- Raw string node identifiers should be treated as an internal representation detail, not the boundary contract.
