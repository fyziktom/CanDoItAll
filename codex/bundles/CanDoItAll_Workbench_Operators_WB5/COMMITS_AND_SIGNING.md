# Coherent signed commits

The user requests real implementation commits. Resolve signing readiness at entry, not after
all tests. Use the existing configured PGP key and native pinentry; ask the operator to unlock
it when required. Never ask for the passphrase/private key in chat or put it into environment,
logs, scripts, test containers or command-line loopback input.

Keep the same host user, GNUPGHOME, gpg-agent and persistent PowerShell environment for later
checkpoints. An open PowerShell is not a guarantee that the key remains unlocked: agent idle
and absolute cache lifetimes differ. Only use an operator-approved bounded cache configuration;
otherwise request native pinentry again on expiry. Do not kill/restart the agent casually. [F02]

Suggested larger commits: S0 original-party correction and sealed WB5 input; W1 parties/meetings;
W2 protected references; W3 runtime/web preview; W4 final proof and maintained docs. Combine
closely related stages if justified; there is no microcommit quota. Keep signed sibling fixes
separate when applicable. Verify each signature with git verify-commit and record exact refs.

Commit the supplied bundle history as input, unmodified, with an appropriate checkpoint after
checking the manifest and index. Do not stage unrelated private evidence, generated secrets or
other user edits. Do not change sealed templates into passing evidence; store completed evidence
outside them. Never use --no-gpg-sign, disable signing, rewrite author identity or silently skip
a requested checkpoint to get around pinentry. A signing failure is reported separately from
implementation progress.

No push, merge, release, history rewrite or deletion of old bundles is authorized. Keep the
working branch supplied by the user. Future pre-merge housekeeping is a different task.
