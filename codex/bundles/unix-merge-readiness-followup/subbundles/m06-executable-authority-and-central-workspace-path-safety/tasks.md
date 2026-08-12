# Tasks

- [ ] Change Unix executable validation from “any execute bit exists” to effective current-identity execute access (`X_OK` equivalent) with typed failures.
- [ ] Validate executable candidates for all control characters and bounded length.
- [ ] Validate `PATHEXT` entries as bounded simple extensions: no separators, drive/URI syntax, controls, duplicates, or excessive count/length.
- [ ] Keep canonical final executable path and fingerprint proof after symlink resolution.
- [ ] Make `WorkspacePathAccessGuard` call safe-path/reparse validation before returning success.
- [ ] Add central tests for workspace and managed-file symlink/reparse traversal to an outside root.
- [ ] Ensure downstream callers do not need to independently remember the basic containment/link invariant.
- [ ] Preserve explicit external-target alias authority.
