# Coherent signed commits and operator key lifetime

The operator explicitly requires local commits. The already pushed S0 commit is preserved. This overrides older package text that left all
changes uncommitted; it does NOT authorize push/merge or history cleanup. Use the operator's already
configured identity/key, never invent a new key or account.

## Entry

Ask early: 'Please unlock your existing PGP signing key in the native pinentry dialog so I can
sign the larger verified checkpoints. Keep the signing agent available for this run.' Never ask
for the passphrase or a private-key export in chat. Inspect `git config --show-origin --get
user.signingkey`, `gpg.format`, `gpg.program`, `commit.gpgsign` and the effective GnuPG installation
without dumping secret material. Confirm it is the intended OpenPGP setup; do not replace it with
SSH signing because it is easier. Use the same host user, persistent PowerShell environment,
GnuPG home and gpg-agent. The signing environment is not the test container environment.

The native agent caches an unlocked key subject to policy; merely preserving a PowerShell process
does not guarantee an indefinite unlock. Inspect existing cache policy with the operator. A
bounded task-length default/max cache TTL adjustment may be proposed for their approval before
long tests, retaining and restoring previous settings afterward. No silent system-wide relaxation,
no disabling pinentry, no repeated dummy signing loop to defeat expiry, no secret in environment,
stdin automation, logs, temporary files or shell history. Do not kill/restart the agent between
ordinary commits. Hardware-token and pinentry policies may still require another interaction;
ask again when necessary rather than signing unsigned or claiming success.

## Commit checkpoints (flexible coherent boundaries)

1. PP2C-R1 local-settings baseline repair and its deterministic/native tests; preserve signed S0.
2. PP2 publication/import Sharing renderer and native seam, including owned confirmation tests.
3. PP2 source/discovery/synchronization/refresh family, actual sandbox, consumer wiring and tests.
4. Final-image regression/evidence fixes, maintained documentation and closure.

Combine chunks that cannot reasonably compile independently; split another substantial fix if
it has a separate cause. Avoid a flood of micro commits or a single unreviewable all-night diff.
Never create empty checkpoint commits. Stage only intended paths/hunks after inspecting the
starting dirty state and index; do not absorb user changes via blind `git add -A`.

Representative commands after staging the correct coherent change:

```powershell
# Use the existing configured OpenPGP key; unlock through native pinentry.
git diff --cached --check
git commit -S -m "test(providers): verify multi-instance model parity"
if ($LASTEXITCODE -ne 0) { throw "Signed commit did not complete." }
git verify-commit HEAD
if ($LASTEXITCODE -ne 0) { throw "Commit signature verification failed." }
```

Do not use `--no-gpg-sign`, `--no-verify`, an unsigned API-created commit, or an unrelated private
key. Preserve all hooks and existing identity. If pinentry temporarily blocks, leave the exact
unsigned working change and test evidence intact, ask for unlock, and continue safe read-only work;
report the block without pretending the required commit exists. Do not rewrite pushed history
with amend/rebase to turn a failed signature into a different ancestry.

Verify every new commit between recorded entry and final HEAD with `git verify-commit`, record
its subject/SHA, and distinguish a trusted configured signing key from a merely present signature.
Each test/image receipt records the commit and any uncommitted delta actually compiled. The last
report states signed commits, current dirty files, source/dependency/image hashes and no push.
A final metadata-only commit may reference tests of identical executable inputs; prove the diff.

Official reference context (not product test evidence): Git `-S` signs commit objects and
`git verify-commit` verifies them. `gpg-agent` has separate default and maximum cache TTLs, so
activity can refresh a default timer without overriding the maximum. See the pinned access-date
links in [external references](EXTERNAL_REFERENCES.md).
