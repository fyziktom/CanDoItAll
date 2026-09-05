# Pattern choices

Read adapter: independent catalog/secret failure semantics need normalization. Rejected method extraction (still coupled to rendering) and interface per method (no coherent contract). One port and adapter replace direct reads and allow suspended fake results.

Session: selection and draft/edit-context have an instance lifetime with target replacement. Rejected DI-scoped mutable state (can leak across panels), extra workspace store (duplicates selection), full state-pattern hierarchy (unnecessary). One top-level owner and typed load enum suffice. Tests exercise cancellation, stale generations, target retention, and first selection directly.

Typed sections: explicit ordered definitions drive the rendered tab loop and enum identity. Reject ordinal casts and two separate markup/definition orders. No routing implementation.
