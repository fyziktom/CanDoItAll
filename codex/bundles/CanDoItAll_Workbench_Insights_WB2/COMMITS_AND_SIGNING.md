# Coherent signed checkpoints

Check repository identity, worktree, existing signing configuration and authorized
key early. Ask the operator to unlock the existing PGP key through native pinentry
when necessary. Never ask for a passphrase/private key in chat or place either in
source, logs, environment variables, command arguments or test containers.

Keep the same authorized OS user, GnuPG home, gpg-agent and persistent host shell
for later commits. A persistent shell does not guarantee unlimited cache lifetime;
use the existing policy and ask for pinentry again on expiry. Any longer bounded
cache policy needs the operator's approval; do not disable key protection or copy
the key into a new environment. Do not kill the user's agent as cleanup.

Suggested commits are S0 Components cleanup, report/activity extraction, selection/
support extraction, and final native validation/docs. Closely coupled changes may
share a commit; do not make dozens of cosmetic microcommits. Sign the actual reviewed
changes and verify each with the repository's existing `git verify-commit` path.
No unsigned fallback and no unrelated user's files in a task commit.

Record both repositories' exact signed heads and which source pair was built/tested.
A new locally committed Components correction needs explicit delivery status; the
already-pushed dc573e2b change does not automatically include it. Preserve configured
remotes and CI branch selection. Do not push, merge, release packages, rebase, amend
historical signed commits or remove historical bundles in this run.
