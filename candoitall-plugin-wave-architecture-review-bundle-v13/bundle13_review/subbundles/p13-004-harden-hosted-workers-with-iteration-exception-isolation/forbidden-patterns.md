# Forbidden patterns

- do not leave worker loops without iteration-level exception handling,
- do not swallow exceptions silently,
- do not exit the runtime loop permanently after a single transient failure.
