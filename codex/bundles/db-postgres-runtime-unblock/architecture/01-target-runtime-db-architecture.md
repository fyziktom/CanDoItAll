# Target runtime DB architecture

## Current problem

The app still pays for runtime database switchability on the hot path:

```text
normal service -> IDbContextFactory<AppDbContext>
              -> SwitchableAppDbContextFactory
              -> runtime lease
              -> resolve current profile
              -> create DbContextOptions
              -> AppDbContext
```

That made sense while SQLite profiles and live profile switching were part of the development workflow. After PostgreSQL-only conversion, it is unnecessary overhead and an architectural hazard.

## Target shape

```text
startup
  -> resolve canonical runtime profile
  -> validate provider is PostgreSQL
  -> build NpgsqlDataSource / DbContextOptions once
  -> register pooled canonical AppDbContext factory

normal services
  -> ICanonicalAppDbContextFactory / IDbContextFactory<AppDbContext>
  -> pooled PostgreSQL context
  -> no profile resolution per context
  -> no switch lease per context

admin/profile tools
  -> IProfileDbContextFactory
  -> explicit ResolvedDatabaseProfile
  -> used only for Data Sources test/create/transfer/maintenance
```

## Runtime profile activation

Default behavior:

```text
Save profile -> profile exists
Activate profile -> writes active profile to control plane
UI shows "restart required"
next app start uses new canonical profile
```

Development-only optional behavior:

```text
Feature flag: Database:EnableHotSwitching = true
Switch action enters maintenance mode
drain active operations
complete generation change
resume operations
```

## Canonicality outcome

- Normal runtime cannot accidentally use two DBs.
- DbContext creation becomes much cheaper.
- PostgreSQL pooling can be used properly.
- Data Sources admin workflows can still create/test/transfer profiles.
- Hot switching becomes explicit, rare, and testable.
