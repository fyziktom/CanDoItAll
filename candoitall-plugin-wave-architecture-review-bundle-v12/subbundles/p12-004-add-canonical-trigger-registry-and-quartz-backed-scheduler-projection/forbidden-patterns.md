# Forbidden patterns

- Do not make Quartz tables or config the domain source of truth.
- Do not hide scheduling semantics only in job code.
- Do not run heavy plugin logic inline in Quartz jobs.
