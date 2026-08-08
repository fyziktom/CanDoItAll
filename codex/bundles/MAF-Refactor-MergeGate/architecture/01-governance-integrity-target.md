# Governance integrity target

## Problem

The current metadata reader returns the same `null` for:

- a genuine detached/legacy run with no authority projection;
- a current turn whose authority property was removed;
- malformed authority JSON;
- an invalid enum, identifier, or allow-list shape.

Runtime restoration cannot safely treat these states as equivalent.

## Target read model

Introduce a tri-state result, for example:

```text
Absent
  metadata genuinely contains no authority projection

Valid
  projection parsed and all current-schema invariants hold

Malformed
  authority key exists but cannot be trusted
```

The exact type name is flexible. The semantics are not.

## Fail-closed rules

A run must fail before runtime construction when any of the following is true:

- a turn-context reference exists but authority is absent;
- transient context is required but authority is absent;
- an authority key exists but is malformed;
- current-schema authority lacks agent/profile identity;
- authority agent does not match the run agent;
- authority database profile or generation does not match the workspace execution identity;
- authority scope does not match the trusted run scope;
- policy version or fingerprint is missing for a current schema.

A `null` governance snapshot remains valid only for an explicitly recognized detached or legacy run
that never claimed context-admitted authority.

## No compatibility widening

Legacy compatibility must be identified by positive evidence such as a known schema/version or the
complete absence of both turn-context and authority markers. Malformed current metadata must never be
reclassified as legacy.
