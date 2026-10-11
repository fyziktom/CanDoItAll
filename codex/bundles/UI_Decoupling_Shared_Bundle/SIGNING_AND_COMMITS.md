# PGP signing and incremental commits

This protocol applies when the selected child authorizes commits. PC2 explicitly does. It does not authorize a push, merge, release, force-push, or rewriting existing history.

## Entry: ask before coding, never ask for a secret

Tell the operator at the beginning:

> Please unlock the repository's configured PGP signing key through your local pinentry/approved secure mechanism. I will keep the same signing environment and GPG agent for the run and make signed commits at tested checkpoints. Do not send a passphrase or private key in chat.

Inspect the effective Git identity, `gpg.format`, `user.signingkey`, `gpg.program` / `gpg.openpgp.program`, signing policy and hook policy. Inspect only the relevant keys; do not dump all Git configuration, environment variables, or credential helpers. Respect existing wrappers, hardware-backed keys, and operator identity. Do not silently change the signing format or generate/import another key. A configured non-OpenPGP format must be resolved with the operator, not mislabeled PGP.

Keep one designated committer. Retain the same user context, `GNUPGHOME` (when explicitly set), agent socket/installation, executable, and dedicated persistent PowerShell/terminal environment. On Windows, do not accidentally alternate Git-bundled GPG and another GnuPG installation. Parallel workers may edit/test their assigned files but do not run competing commits or pinentry requests.

Before the first implementation batch, sign and verify one **non-secret** task-owned challenge using the configured signing mechanism, or verify the first meaningful signed checkpoint when an approved wrapper only supports Git signing. Do not create an empty commit just as a probe. Record only public fingerprint, method, time, and success/failure in the private run ledger. A successful listing of a key is not a signing test.

Keep a pending pinentry request observable to the operator within the terminal/tool's supported lifetime. Do not immediately cancel because the operator has not noticed it, and do not create multiple overlapping prompts. On timeout, report the actual state and resume one controlled request; never collect the passphrase through a tool argument.

## Cache lifetime: preserve, do not extract

GnuPG's documented default idle cache lifetime is 600 seconds; use refreshes that idle timer. Its separate maximum lifetime defaults to 7200 seconds and expires even with recent use. Local policy may differ. A persistent shell preserves environment continuity, not the secret cache's maximum lifetime. `ignore-cache-for-signing` can bypass caching. [G1]

At entry, inspect the applicable policy and explain when it is too short for the intended work pattern. A bounded cache-policy adjustment requires explicit operator approval and must be configured **before** the unlock. Never silently extend it indefinitely, weaken trust, or change machine-wide security. Record non-secret approved settings privately. Do not reload/kill/restart the agent as a keep-alive mechanism; a reload may clear cached passphrases. [G4]

Regular legitimate checkpoint signatures may refresh an idle cache but are not guaranteed to outlive the hard limit. Do not run dummy signing loops, extract or preset passphrases, use `--passphrase`, write secrets to an environment variable/file, or bypass pinentry. If the cache expires, a hardware policy requires interaction, or the agent disappears, request a local re-unlock. Never fall back to unsigned commits.

An unlock delay permits independent read-only inspection and already-owned tests. It does not justify accumulating the entire assignment as one uncommitted change. Pause new dependent implementation at the next coherent signing checkpoint until access is restored; report that blocker without discarding work.

## Checkpoint protocol

Commit a coherent tested correction, a contract with all affected consumers, an asset/integration change, or a final documentation/proof closure. A numeric commit quota is not the goal; delaying every change until one final commit is disallowed. Keep failing-first evidence in the owned run area, then commit the corrected behavior with passing targeted tests. Do not deliberately leave normal test lanes red.

For each checkpoint:

1. Recheck branch, HEAD and operator-owned worktree/index changes. Review the owned diff and the exact files to stage. Preserve unrelated staged and unstaged changes. If the index contains others' changes, use a reviewed isolation strategy or request resolution; never silently include, reset, stash, or unstage their work.
2. Build affected production code, refresh the relevant test assembly, confirm expected discovery, and run the owning tests. Review any generated, portability baseline, lockfile, or sibling delta. Source changed after a run invalidates affected proof.
3. Stage only explicit reviewed paths/hunks. Inspect `git diff --cached --check`, the staged names and diff; ensure no secrets or raw browser/database evidence were staged. Do not use blanket `git add .` / `git add -A` in a mixed worktree.
4. Create an English commit with `git commit -S` using the established identity and normal hooks. `-s` only adds a sign-off trailer and is not a cryptographic signature. Do not use `--no-gpg-sign`, `--no-verify`, or disable signing to get past a blocker. [G2]
5. Check command success and run `git verify-commit HEAD`; confirm the expected public fingerprint and the new parent/commit identity, not just a displayed `Signed-off-by` line. A cryptographically valid signature from the wrong key is not the agreed identity. Record the actual hash, public signing identity, tests, and remaining scope. [G3]

Do not amend/rebase/squash an already reviewed checkpoint merely to make the history look tidy. Correct it in a new signed commit. Sibling fixes require their own owned signed checkpoint and explicit consumed revision; no automatic sibling push is authorized.

## Resume and final proof

After context compaction, reconnect to the same committer environment/agent where possible, inspect HEAD/index and the ledger, then verify the latest checkpoint. An old successful unlock is not current proof that a new signature will succeed. Resume the next unfinished stage, not the original extraction.

The final response lists the signed checkpoints and their verified status. The tracked verification report may refer to the last tested production-source commit and be committed separately afterwards. Do not require a report to contain its own future commit hash; check any documentation-only delta and record the final report commit in the operator-facing result. Never advertise GitHub's verification of an earlier commit as verification of the new local series.

## Primary references

- [G1] GnuPG, Agent Options: https://www.gnupg.org/documentation/manuals/gnupg/Agent-Options.html
- [G2] Git, git-commit: https://git-scm.com/docs/git-commit
- [G3] Git, signature-format: https://git-scm.com/docs/signature-format
- [G4] GnuPG, gpg-agent manual (signals/reload): https://www.gnupg.org/documentation/manuals/gnupg26/gpg-agent.1.html

The workflow policy above is this assignment's design; the references support the tool and cache semantics, not an assertion that signing was performed by the reviewer.
