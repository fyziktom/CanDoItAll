# Signed coherent checkpoints

The user requests commits. Arrange native PGP unlock early through the existing pinentry;
keep the same host user, GnuPG home, gpg-agent and persistent shell for subsequent commits.
An agent cache can expire despite a persistent shell; request native unlock again when needed.
Do not put passphrases/private keys into prompts, environment variables, scripts, logs or
containers. Do not silently change signing policy, use unsigned commits or amend user history.

Suggested meaningful checkpoints: inherited quote regression/continuity; Calendar and narrow
contracts; complete Gantt; both task families and native integration; final evidence/docs.
Combine tightly related changes when appropriate; no microcommit quota. Build and verify
relevant behavior before each checkpoint, then run `git verify-commit` on the exact commit.
Record source pair and dirty input fingerprints for tests that ran before a commit.

No push, merge, release, force operation, removal of historical bundles or destructive fixture
cleanup is authorized. Preserve unrelated user changes. Sibling changes are permitted only
for a reproduced reusable component gap in this family, with its own owner tests, verified
signed commit and exact application source/asset matching. Both old Components fixes are
already published; do not repeat their implementation or their old push request.
