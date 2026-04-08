# Forbidden patterns

- Do not make Quartz tables or ad-hoc Quartz config the domain source of truth.
- Do not hide scheduling semantics only in job code.
- Do not run plugin business logic inline in Quartz job bodies.
