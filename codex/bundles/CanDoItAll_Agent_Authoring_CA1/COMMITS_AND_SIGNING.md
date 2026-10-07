# Signed coherent commits — early native unlock, no credential export

The user requires local signed commits. Use the existing configured OpenPGP identity/key and hooks.
At entry inspect the effective `user.signingkey`, `gpg.format`, `gpg.program` and `commit.gpgsign`
without dumping secrets. Ask early for native pinentry unlock when the key is locked, not for the
passphrase/private key in chat. Maintain the same host user, persistent PowerShell environment,
GnuPG home and gpg-agent across coherent checkpoints. Signing is outside all test containers.

The current agent's cache may expire even when PowerShell stays open. Do not silently change TTLs,
kill/restart the agent between ordinary commits, disable pinentry, automate a password, or keep
signing dummy objects to defeat expiry. A bounded task-length cache adjustment needs the operator's
explicit consent and must restore previous configuration. Hardware-token policies can still ask
again. No unsigned fallback, `--no-gpg-sign`, hook bypass, invented key or API-created substitute commit.

Suggested coherent checkpoints (combine tightly coupled changes when needed):
1. Bounded documentation/delivery follow-up and capability draft/owner tests.
2. Complete capability authoring rendering and native parent integration.
3. Complete team metadata/icon/membership rendering and owner integration.
4. Final sandbox, application proof, maintained docs and qualified closure.

Do not make empty checkpoint commits or dozens of microcommits. Stage reviewed intended paths/hunks;
no blind `git add -A` over user work or retained artifacts. Preserve all historical bundles and the
original failed test evidence. Do not rebase/amend pushed history to hide a test or signing failure.

```powershell
# Stage only inspected paths before this checkpoint.
git diff --cached --check
if ($LASTEXITCODE -ne 0) { throw "Staged change failed whitespace validation." }
git commit -S -m "refactor(agents): isolate capability authoring and preserve native effects"
if ($LASTEXITCODE -ne 0) { throw "Signed commit did not complete." }
git verify-commit HEAD
if ($LASTEXITCODE -ne 0) { throw "Commit signature verification failed." }
```

Verify every new main and sibling commit from their recorded entry, preserve key identity and record
SHA/subject. A present signature is not proof it used the configured intended key. If signing is
blocked, preserve the change/evidence, ask for native unlock and report the pending exact checkpoint.
Do not claim commits exist when they do not. The final report separates working changes, signed
commits and dependency publication. Push, merge, PR, package release and history cleanup are NOT
part of this authorization. Current checkpoint/signature provenance is in [sources](SOURCES.md).
