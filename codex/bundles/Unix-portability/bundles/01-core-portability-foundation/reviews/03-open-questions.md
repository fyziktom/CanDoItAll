# Core open questions to resolve during execution

These are decision gates, not reasons to delay A00.

1. Resolved at C0: SharedKernel owns only the pure logical-path value contract because Infrastructure and MAF Core already depend inward on it. It must not gain I/O, host probing, or provider selection.
2. How will root/volume case semantics be determined on macOS and read-only roots?
3. Which headless secure provider best fits local-first deployment: certificate, remote vault, or externally supplied wrapping key?
4. How will ASP.NET Data Protection key-ring at-rest protection integrate with each profile without circular bootstrap?
5. Do real existing users have DataProtectionFileVault data requiring migration, or can that mode be explicitly discarded as alpha/development data?
6. Resolved by A03/C2a: Windows uses LocalApplicationData purpose children; Linux uses XDG data/config/state/runtime with documented service overrides; macOS uses Application Support plus Library Logs. Installer/service owners may override each typed root without changing logical locators.
7. Gate C2 input: the Keychain adapter and injected-native contracts are complete, but no genuine macOS host is available in this execution environment. The independent reviewer must decide whether A05 remains blocked or whether actual macOS proof may remain mandatory at final Gate C4.
7. Is PostgreSQL actual-host CI on macOS installed locally, provided remotely, or separated into a database-independent startup plus scheduled integration?
