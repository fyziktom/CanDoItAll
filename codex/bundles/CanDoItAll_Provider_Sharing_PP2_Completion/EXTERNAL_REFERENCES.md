# Official tooling references checked for this handoff

Access date: 2026-10-02. These support signing guidance only; they are not evidence about
CanDoItAll runtime behavior. No OpenAI service documentation or pricing assumptions are required:
native-default model tests use the accepted catalog of the current application source.

- Git commit signing: https://git-scm.com/docs/git-commit (the `-S` option).
- Signature verification: https://git-scm.com/docs/git-verify-commit.
- GnuPG agent cache policy: https://www.gnupg.org/documentation/manuals/gnupg/Agent-Options.html.

Use the installed tool version and current operator policy. Keeping one session does not disable
the agent's maximum cache lifetime. A signing-key unlock is not a GitHub/network login and is
never a reason to export the private key into a test environment.
