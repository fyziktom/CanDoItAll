# Acceptance

- No production feature should schedule new work through the legacy in-memory background queue seam.
- Migrate remaining production call sites to the durable automation scheduler, or implement a true durable bridge.
- If the legacy queue remains temporarily, mark it legacy/deprecated and prevent new plugin-facing usage.
