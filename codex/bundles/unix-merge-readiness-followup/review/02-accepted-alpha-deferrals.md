# Accepted alpha deferrals

These deferrals are deliberate and must not be reported as accidental omissions.

## Enterprise vault adapters

Complete Azure Key Vault and HashiCorp Vault CRUD adapters are not required for the alpha merge. Preserve the provider enum/configuration surface, fail closed when selected but unavailable, and avoid any documentation claim that they are implemented.

Alpha-supported secret paths are:

- Windows `Auto` → current-user DPAPI / `Strong`;
- Unix `Auto` → `LocalUserFile` / `BasicLocal` with explicit same-user warning;
- explicit `ExternalWrappingKeyFile` for a stronger headless deployment boundary;
- interactive Keychain or Secret Service only where their actual dependencies are available.

## macOS proof

Cross-publish is not actual-host proof. Keep macOS profiles `ActualHostUnverified` until `M09` is completed by the colleague. Keychain CRUD may remain a separate deferred validation from the general headless macOS gate.

## Hosted CI

Do not execute hosted CI during implementation. The workflow must be left internally correct for later use, but local Windows/Linux evidence and the macOS colleague handoff are the current gating path.
