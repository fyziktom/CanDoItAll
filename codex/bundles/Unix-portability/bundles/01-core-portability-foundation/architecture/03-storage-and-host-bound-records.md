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

## A03 execution alignment

A03 implements this design in the existing Infrastructure boundary:

- `HostBoundPathRecord` uses format version 1 with platform family, physical syntax, opaque host binding, path, state, and validation time.
- `ApplicationPurposeRootPolicy` resolves purpose-specific Windows, Linux XDG/service, and macOS roots. It does not persist logical locators as physical paths.
- Database profile and preferred-application catalogs use format version 2 and retain a schema-1 compatibility reader. New writes are version 2 only.
- Storage object references use format version 2 and canonical logical locators. Legacy separator migration is restricted to those typed fields.
- Storage catalog roots persist host-binding metadata in PostgreSQL and use an EF migration rather than an application-side shadow schema.
- Foreign or missing paths never become active through a guessed native path. Only explicit rebind creates a current-host active record.

The migration state machine is realized as durable backup, staged checksum, target/database commit, commit marker, restart repair, and checksum-verified rollback. Database profile password ciphertext is preserved byte-for-byte. Migration reports and errors contain identifiers, counts, states, and hashes rather than secret values or physical roots.

The host identifier is deliberately opaque. By default it is a SHA-256-derived binding over platform and machine name; container/service deployments can provide a stable `CANDOITALL_HOST_BINDING_ID`. It is authorization metadata, not an agent-facing external-target alias.

Workbench project-structure metadata paths remain assigned to B00/B02. A03 exposes the reusable contract but does not rewrite fields owned by the runtime/tools bundle.
