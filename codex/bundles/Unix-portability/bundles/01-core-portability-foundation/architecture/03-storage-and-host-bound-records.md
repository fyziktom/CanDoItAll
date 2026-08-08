# Storage and host-bound records

## Record classes

### Portable records

- logical storage locator;
- artifact relative path;
- route;
- content/revision identity;
- process/template/capability IDs.

These serialize independently of the host.

### Host-bound records

- workspace root;
- local repository path;
- external allowed root;
- preferred application executable;
- service/install path;
- local tool root.

Recommended envelope fields:

```text
formatVersion
platformFamily
pathSyntax
hostId (optional, privacy-reviewed)
path
state: Active | Unresolved | NeedsRebind | Migrating | Disabled
lastValidatedAt
```

Do not expose `hostId` or full paths unnecessarily in diagnostics.

## Migration state machine

```text
Discovered
  -> BackedUp
  -> Staged
  -> Verified
  -> PointerCommitted
  -> RestartVerified
  -> SourceRetainedDuringGrace
  -> SourceCleaned
```

Every step is idempotent and journaled without secret values.

## Storage locator migration

- read old `/` and known logical `\` formats;
- write only versioned canonical `/`;
- never transform physical/opaque values;
- preserve revision/token/content identity;
- create a dry-run diff;
- verify physical content and metadata before commit.

## Root rebind

Foreign or missing roots enter `NeedsRebind`. The operator chooses a new root, and the migration validates:

- ownership/permissions;
- links;
- collisions/case;
- available space;
- copy/checksum;
- database/catalog references;
- rollback source.

No automatic path guess can authorize a move.

## Preferred applications

Preferences are host/platform-bound and optional. A copied preference is disabled until revalidated/rebound. Executable existence is not enough; desktop session, operation support, and external package capability must also be available.
