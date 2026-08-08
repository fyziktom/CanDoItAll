# Path and filesystem model

## Canonical logical path

A logical path is an application identifier, not an OS path.

Recommended invariants:

- UTF-8 text value;
- `/` separator;
- no empty/dot/dot-dot segments;
- no leading `/`, drive, UNC, URI scheme, NUL, or control characters;
- exact/ordinal comparison;
- deterministic Unicode policy recorded before implementation;
- maximum segment and full length limits;
- serialization version.

A legacy reader may map `\` to `/` only for a field known to have historically stored a Windows-style logical path.

## Physical path

A physical path is resolved only after authority and root selection. It:

- uses native separators and `Path` APIs;
- can contain backslash as a filename character on Unix;
- must not be persisted as a portable locator;
- may be stored as host-bound configuration with platform/format/version;
- uses root/volume filesystem semantics for equality;
- is validated against trusted roots and link policy.

## Foreign syntax detection

Before `Path.IsPathRooted` or `Path.GetFullPath`, detect:

- Windows drive absolute and drive-relative forms;
- UNC/device paths;
- Unix absolute paths;
- `~` and configured variable tokens;
- URI schemes.

A Windows path on Unix is not a relative path. A Unix path on Windows is not automatically a valid host path. Store/display it as unresolved host-bound data until explicit rebind.

## Case semantics

- logical paths: `StringComparer.Ordinal`;
- database IDs/capability IDs: contract-specific, usually ordinal;
- environment variable names: host semantics;
- physical filesystem paths: root/volume policy;
- URLs: URI rules.

macOS support must not hard-code ignore-case. Probe/configure uncertainty per root or use a conservative policy that cannot authorize an escape.

## Link policy

For managed writable roots:

- root must be direct and trusted;
- deny symlink/reparse traversal by default;
- validate every existing ancestor immediately before access;
- keep temporary files inside the same verified directory;
- resolve/open only after workspace/tool authority;
- document residual race limitations and add failure injection.

External allowed roots may require a separate explicit policy; do not silently relax managed-root rules.

## Determinism

Every filesystem enumeration that influences output, persistence, fingerprints, plans, receipts, migration, or agent context is ordered by canonical logical key with explicit stable tie-breaking.
