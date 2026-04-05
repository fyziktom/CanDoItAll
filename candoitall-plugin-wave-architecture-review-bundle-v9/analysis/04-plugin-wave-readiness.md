## Plugin-wave readiness
**Verdict: NO-GO**

The next large plugin wave should not start yet for the following reasons:

1. The core node still exposes legacy carrier concerns.
2. The active runtime still hydrates compatibility state back into the node.
3. Marker truth is still split.
4. The “plugin platform” is not yet truly plugin-first because unknown fields still require shared UI/model changes.
5. Custom plugins can still inherit fake legacy identity.
6. Reference semantics still require core-workbench edits for every new relation kind.
7. Read paths still mutate persisted state.
8. There is no generic write-side connector execution boundary yet.

The result would be predictable:
- email / LinkedIn / custom API work would either push more technical state into the node carrier,
- or add more hardcoded shared UI/editor logic,
- or both.
