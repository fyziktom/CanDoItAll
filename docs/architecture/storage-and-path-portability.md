# Storage, Paths, And Host Portability

CanDoItAll separates portable storage identity from host-owned physical paths. A storage
object is durable because its provider, storage catalog identity, locator kind, and
logical locator are persisted; an absolute local path is never treated as a portable
identity by itself.

## Storage Flow And Ownership

```mermaid
flowchart LR
    Request["Placement or access request"] --> Routing["Typed routing rules"]
    Routing --> Catalog["Storage catalog record"]
    Catalog --> Registry["Driver registry by provider kind"]
    Registry --> Driver["Filesystem, FTP, or IPFS driver"]
    Driver --> Reference["Portable storage object reference"]
    Reference --> Adapter["Authorized FileTools/UI adapter"]
```

- `StorageCatalogService` owns configured providers, health, capabilities, routing rules,
  bootstrap filesystem storage, and explicit root rebind.
- `DefaultStorageRoutingService` selects an enabled storage that satisfies typed scope,
  usage, content, size, preview, publish, and capability requirements.
- `StoragePlacementService` validates the selected catalog record and dispatches through
  `IStorageDriverRegistry`; it does not implement provider protocols.
- A driver returns a `StorageObjectReference`. Product modules persist and exchange that
  reference instead of reconstructing provider-specific paths.
- `CanDoItAll.FileTools.Integration` maps an authorized storage source into browse, open,
  download, and save interactions. FileTools is an adapter boundary, not the owner of the
  storage catalog or routing policy.

## Driver Matrix

| Provider | Locator | Main capabilities | Operating-system behavior |
| --- | --- | --- | --- |
| Filesystem | Storage-relative logical path | Read, write, delete, preview, download, local open, mutable update, batch operations | Uses the current host's native filesystem syntax and a host-bound absolute root |
| FTP | Normalized remote path | Read, write, delete, download, batch operations | Protocol behavior is the same on Windows, Linux, and macOS; credentials come from the secret resolver |
| IPFS | Content address | Read, write, preview, download, direct URL, batch operations | HTTP/API behavior is host-neutral; optional pinning and gateway URLs come from provider configuration |

Catalog capability masks describe the configured contract; each driver separately
declares its implemented capabilities. Access projection intersects both masks, transfer
validates the source and target driver operations, and connection tests return the
driver's current capability result. Missing drivers, disabled storage, unavailable
health, read-only state, missing required catalog capabilities, or unsupported driver
operations fail explicitly. The placement path does not silently select an unrelated
provider. Routing-rule alternatives are deliberate configured candidates and are
returned with a warning when the preferred storage cannot be used.

## Logical And Physical Paths

Logical paths use `/` separators and reject absolute roots, backslashes, drive prefixes,
URI syntax, empty segments, `.` segments, and `..` traversal. They remain comparable with
ordinal semantics on every host.

Physical paths remain host-owned:

- Windows accepts native drive-absolute or UNC roots.
- Linux and macOS accept Unix-absolute roots.
- Foreign or ambiguous physical syntax is rejected instead of translated.
- Containment and comparison use the actual filesystem policy. Case sensitivity is
  probed for an existing managed root and is not inferred solely from the OS name.
- Managed filesystem operations reject symbolic-link and Windows reparse-point traversal.
  Mutation targets are revalidated immediately before commit.

Filesystem storage keys encode individual physical names into a deterministic logical
form. Browse ordering uses the encoded logical key with ordinal comparison, so UI paging
does not depend on platform directory enumeration order.

## Host Binding And Rebind

Each active filesystem catalog root records:

- the platform family;
- physical path syntax;
- an opaque host-binding ID;
- the absolute root path;
- binding state and last validation time.

Interactive runs derive an opaque binding from platform and machine name when
`CANDOITALL_HOST_BINDING_ID` is absent. Containers and services must set a stable value
explicitly because machine identity can change across image or service lifecycle. The
value must contain 8-128 ASCII letters, digits, hyphens, or underscores.

A root resolves only when its record is active and its platform and binding ID match the
current host. Legacy, foreign-host, mismatched, or invalid-syntax roots become unavailable
and require explicit rebind through `IStorageCatalogPathMigrationService`. Rebinding is a
validated administrative decision; no driver guesses that a Windows path corresponds to
a Unix path or that two machines mount the same directory.

The bootstrap filesystem catalog is bound to the current workspace root. The catalog may
contain only one system-default entry, and that entry must be the trusted filesystem
bootstrap record active for the current root. Conflicting defaults are treated as an
integrity error rather than selected by ordering.

## Platform Roots

The workspace root defaults outside the checkout:

| Host | Default workspace root |
| --- | --- |
| Windows | `%LOCALAPPDATA%\CanDoItAll\workspace` |
| Linux | `$XDG_DATA_HOME/candoitall/workspace`, otherwise `~/.local/share/candoitall/workspace` |
| macOS | `~/Library/Application Support/CanDoItAll/workspace` |

Services should set `Storage__WorkspaceRoot` and the control-plane purpose roots
explicitly. The complete default matrix and service profiles are in
[Installing instances](../operations/installing-instances.md#default-user-owned-runtime-roots).

## Portability Rules For Changes

When adding a storage provider or path-bearing feature:

1. Persist a typed logical identity or provider locator, not an unclassified path string.
2. Keep provider selection and capability requirements in routing/application policy.
3. Put protocol and physical I/O in a driver or integration adapter.
4. Classify physical syntax before calling `Path` APIs and reject foreign syntax.
5. Enforce containment and no-link traversal for managed local paths.
6. Bind absolute roots to the current host and require explicit rebind after migration.
7. Test Windows, case-sensitive Unix, foreign-path, traversal, symlink/reparse, and
   host-binding mismatch cases.

The relevant implementation entry points are
[`StorageContracts.cs`](../../src/Foundation/CanDoItAll.Infrastructure/Storage/Abstractions/StorageContracts.cs),
[`StorageCatalogService.cs`](../../src/Foundation/CanDoItAll.Infrastructure/Storage/Persistence/StorageCatalogService.cs),
[`FileSystemStoragePathPolicy.cs`](../../src/Foundation/CanDoItAll.Infrastructure/Storage/Drivers/FileSystemStoragePathPolicy.cs),
and
[`HostBoundPathPolicy.cs`](../../src/Foundation/CanDoItAll.Infrastructure/Common/HostBoundPathPolicy.cs).
