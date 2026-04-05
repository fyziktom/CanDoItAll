# Hard gates

## HG-01 — Node core is sealed and binding/reference truth is externalized

- **Type:** Automated + manual
- **Current status:** FAIL
- **Related finding(s):** P8-001
- **Pass condition:** ProjectObjectRecord mapping no longer persists binding/media/artifact columns, foreign-owner IDs are not writable metadata, and no direct writes to node binding fields remain outside the binding boundary.


## HG-02 — Editable-node hierarchy has one canonical owner

- **Type:** Automated + manual
- **Current status:** FAIL
- **Related finding(s):** P8-002
- **Pass condition:** Editable nodes use one hierarchy owner only, and no editable-node Contains/BelongsTo link rows are persisted.


## HG-03 — Registry owns node-scoped capability and assignment policy

- **Type:** Automated + manual
- **Current status:** FAIL
- **Related finding(s):** P8-003
- **Pass condition:** The node-kind registry/capability service resolves assignment roles and canonical-node scope. Hardcoded role/type switches are gone.


## HG-04 — Connector platform is plugin-first, not enum-first

- **Type:** Automated + manual
- **Current status:** FAIL
- **Related finding(s):** P8-005
- **Pass condition:** Provider/resource editor and resolution flows are driven by connector manifests and plugin keys. Adding a synthetic plugin does not require enum expansion or switch-page edits.


## HG-05 — Write-side connectors have a durable operation boundary

- **Type:** Manual + runtime
- **Current status:** FAIL
- **Related finding(s):** P8-006
- **Pass condition:** Write-side connector actions commit intent durably and execute through a worker/outbox/idempotent operation boundary instead of inline side effects + compensation.
