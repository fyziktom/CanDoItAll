# Target runtime architecture

## Canonical runtime DB

At startup:
1. Control plane resolves or provisions a PostgreSQL runtime profile.
2. `ICanonicalRuntimeDatabase` stores the immutable runtime profile for this process.
3. `AddPooledDbContextFactory<AppDbContext>` uses only that profile for normal runtime contexts.

Normal runtime modules:
- inject `IDbContextFactory<AppDbContext>`;
- never resolve database profiles;
- never switch profiles;
- never use profile-specific maintenance context creation.

Maintenance modules:
- may use `IProfileAppDbContextFactory` or equivalent explicit profile-specific factory;
- must be named as maintenance/profile-specific;
- must not be used in automation/process runtime loops.

## Activation model

The operator can choose a new profile. This writes pending active profile state but does not affect the canonical runtime DB. The UI must display:

- Running now: canonical runtime profile.
- Pending restart: selected profile, if different.
- Restart required: yes/no.

## Removed hot-switch model

`DatabaseRuntimeState` should no longer expose context drain or switch sessions. At most it should publish profile metadata and activation notifications.
