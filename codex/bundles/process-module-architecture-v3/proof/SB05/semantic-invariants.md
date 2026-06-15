# SB05 Semantic Invariants

- Driver abstraction contracts do not reference UI, persistence, Git, module, or infrastructure implementation namespaces.
- Core, runtime, and builder contracts do not reference concrete driver names.
- Capability values remain opaque strongly typed tags.
- Driver dependency ordering is deterministic and dependency-first.
- Capability conflicts are explicit match-result facts, not hidden fallback behavior.
- Strategy factories create strategy instances from binding snapshots instead of mutating runtime state directly.
- Strategy results return envelopes with artifact refs, diagnostics, manager signals, and result hashes rather than direct runtime side effects.
- Concrete driver implementation remains out of scope for this subbundle.
