# Signed implementation checkpoints

Commit the work in coherent stages: initial origin fixes; canvas/composer boundary;
structural dialogs/operations; final integrations and validation. Combine strongly
related changes when helpful. Do not create a microcommit per component or leave
all product changes uncommitted at the end.

Check the existing signing setup before the first checkpoint. Request native
PGP/pinentry unlock when necessary; never request the passphrase or private key in
chat. Retain the same host user, GnuPG home, persistent PowerShell/shell and running
gpg-agent. A persistent shell alone does not guarantee that a cache has not expired;
use native pinentry again when necessary. Do not make indefinite cache or security
policy changes without separate approval.

Use the configured signing identity and verify each new commit with
`git verify-commit <sha>`. Preserve the signature verification outcome and actual
source-pair refs in the final report. No unsigned fallback, unrelated commits,
force push, merge, release or rebasing of existing user work.

Historical `codex/bundles` packages remain as history throughout this work. Do not
modify their sealed source or erase their original failures. Pre-merge archival
cleanup is a separate task. The user performs remote push; report any newly needed
sibling commit as local-only until actually published.
