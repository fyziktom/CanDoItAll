# PC3 signing and incremental commits

This assignment authorizes local signed commits throughout implementation, not a push,
merge, release, force-push or rewrite of existing history. Use current repository signing
rules and the installed shared signing protocol when present. The following task-local
requirements apply without installing any newer shared bundle.

## Entry and environment continuity

Ask at the beginning:

> Please unlock the repository's configured PGP signing key through your local pinentry or approved secure mechanism. Do not send a passphrase or private key in chat. I will preserve the signing environment and create verified signed commits at tested checkpoints.

Inspect only relevant public/configuration metadata: the effective Git identity, signing
format/key, configured signing executable/wrapper and hook policy. Do not dump credentials
or the complete environment. Do not change the operator identity, signing format or key.
A non-OpenPGP setup needs an explicit resolution, not a false PGP claim.

Keep one designated committer and the same user context, GPG installation/agent and explicit
GNUPGHOME, if configured. Reuse a persistent PowerShell/terminal environment when available.
Do not alternate signing implementations or let parallel workers compete for the index or
pinentry. Validate signing with a non-secret owned challenge, or the first meaningful
checkpoint when the approved wrapper supports only Git signing; no empty probe commits.

Keep one local unlock request observable within the tool's supported lifetime. Do not
collect a passphrase through chat, files, environment variables or tool arguments. A
persistent shell does not guarantee indefinite key-cache availability. Do not kill/reload
the agent as a keep-alive, use dummy signing loops, or silently relax cache policy. A bounded
policy change requires operator approval. On expiry request local re-unlock, never unsigned
commits or disabled hooks. Read-only inspection may continue during an unlock delay, but do
not accumulate the entire implementation as one unsigned pending batch.

## Tested checkpoints

Create coherent English commits as the staged work becomes testable. Commit regressions
with their fixes and affected consumers; do not intentionally leave normal test lanes red.
There is no numeric quota, but one undifferentiated final commit is not the intended workflow.

Before each commit, recheck HEAD, branch and index/worktree ownership. Stage only explicit
owned paths/hunks and inspect the staged diff, including `git diff --cached --check`.
Preserve other people's staged and unstaged work; no blanket staging, destructive cleanup,
secret/raw evidence inclusion or opportunistic stash/reset. Refresh the relevant build,
test discovery and targeted tests after changes, then use `git commit -S` with normal hooks.
A sign-off trailer alone is not a cryptographic signature.

After success, run `git verify-commit HEAD` and check the expected public signing identity,
parent and new commit. Record the exact commit and valid test evidence. Never use
`--no-gpg-sign`, `--no-verify`, a replacement key or a false success status to bypass a
blocker. Correct an already reviewed checkpoint in a new commit rather than rewriting it.
Apply the same care to any authorized minimal shared correction.

On resume, inspect the real ledger, Git/index and committer state. An earlier unlock is not
proof that the next signature will succeed. The final report lists actual signed checkpoints
and verification results; its own report-only commit can reference the tested source commit
without requiring a circular self-hash. Do not imply automatic push or deployment.

## Existing primary references

These references are retained from the preceding handoff, not newly verified by this
packaging-only revision. Follow current repository tooling and verify relevant behavior
when implementing:

- GnuPG agent options: https://www.gnupg.org/documentation/manuals/gnupg/Agent-Options.html
- Git commit: https://git-scm.com/docs/git-commit
- Git verify-commit: https://git-scm.com/docs/git-verify-commit
