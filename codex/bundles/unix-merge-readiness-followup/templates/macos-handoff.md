# macOS actual-host validation handoff

## Immutable candidate

- CanDoItAll SHA:
- Components SHA/package versions:
- FileTools SHA/package versions:
- SDK/runtime:
- Package/source mode:
- Artifact hashes:

## Required focused gates

- package-mode restore/build;
- runtime portability catalog on actual macOS arm64;
- PostgreSQL migration/restart slice;
- two-cycle headless publish/start/restart outside checkout;
- `LocalUserFile` restart and owner permissions;
- process-group parent-exits-first descendant cleanup;
- executable lookup/permission behavior;
- MCP ping-before-response fake server;
- launchd template lint/rendering;
- redaction scan.

## Separate deferred item

- macOS Keychain actual-session CRUD/restart may remain `ActualHostUnverified` if alpha support claims remain headless `LocalUserFile` only.

## Evidence

## Result

- `MACOS GO`
- `MACOS NO-GO`
