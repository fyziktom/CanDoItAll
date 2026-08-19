# Merge-readiness architecture invariants

1. Logical locators use one host-independent representation; physical paths remain host-owned.
2. Foreign or ambiguous physical syntax fails closed and requires explicit rebind.
3. No managed mutation traverses a symlink/reparse point.
4. A persisted execution contract is immutable, hash-versioned, and tamper evident.
5. A missing legacy capability declaration is unknown, never equivalent to no requirement.
6. One low-level process host owns process creation and control.
7. Higher-level Workbench, Manager, MCP, plugin, and Process services own lifecycle intent, not OS process primitives.
8. Process ownership includes descendants, exact identity, and restart/recovery semantics.
9. Package mode is the authoritative clean-checkout build. Source mode is explicit and anchor verified.
10. Optional host capabilities degrade independently; mandatory capabilities block execution with typed remediation.
11. Strong provider selection never silently falls back to `BasicLocal`.
12. Evidence contains typed states, hashes, counts, and redacted identities—not secrets or full roots.
