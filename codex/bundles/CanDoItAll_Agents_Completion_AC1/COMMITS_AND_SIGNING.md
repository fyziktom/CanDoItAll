# Commits and PGP

The user authorizes local commits, not push/merge/release. Arrange the existing signing identity and native pinentry unlock early, while other work can proceed. Never request/export a passphrase or private key into chat, logs, files, environment variables or test containers. Keep the same host user, GnuPG home, gpg-agent and persistent shell context for subsequent coherent commits. Do not kill the agent or launch commits under a different account after successful unlock.

A shell session is not a promise that cache validity lasts indefinitely. Respect current cache/max lifetime; re-request native pinentry if needed. Do not weaken signing policy, extend indefinite cache, use a keyless identity, disable hooks or use an unsigned fallback. If the original unlock is delayed, preserve pending work and continue safe tests rather than repeatedly spawning prompts.

Suggested checkpoints: (1) Scheduler and preview repairs with focused proof; (2) shell/Usage renderer completion; (3) runtime/floating adjuncts and bounded integration closure; (4) final native tests, census, docs and reviewed validation baselines. Closely related stages may share a commit; no microcommit quota. Keep unrelated user changes untouched, inspect staged paths, and retain historical execution packages.

Run `git verify-commit` on each new commit and record its exact SHA/signature result. For any necessary new Components/FileTools changes verify the sibling separately and document consumer source equivalence. Unavailable signing is an explicit delivery blocker, not permission to fake a successful signed checkpoint.
