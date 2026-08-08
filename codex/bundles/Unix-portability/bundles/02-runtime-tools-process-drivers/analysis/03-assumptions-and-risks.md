# Runtime assumptions and risks

## Working assumptions

- Core Gate C4 supplies stable path, filesystem, storage, secret, composition, and headless-host contracts.
- Direct headless execution is the primary runtime capability; terminal windows are optional.
- Runtime features may be disabled without blocking the Web host.
- `LocalWorkspaceProcessHost` is the default candidate for the authoritative low-level primitive.
- Processes and MAF ownership from the latest refactor is preserved.

## Critical path risks

1. Workbench refactor creates a second process host instead of reusing Core/MAF execution primitives.
2. terminal presentation remains the command source.
3. automatic sudo/pkexec/osascript is introduced to mimic Windows runas.
4. Manager terminates a foreign process after PID reuse or name-only match.
5. Unix environment names are collapsed or secret values enter receipts.
6. MCP allows one command name but resolves a different executable.
7. global npx cache selects a stale or untrusted Playwright MCP package.
8. Docker/plugin code bypasses the owned registry and cancellation.
9. FileTools support is inferred from package name/metadata.
10. ProcessDriverLayer.Platform becomes a dumping ground for generic OS services.
11. host capability availability accidentally grants authority or bypasses approvals.
12. actual-host tests are replaced by mocked OS enums.

## Reopen triggers

- Core C4 changes execution/path/secret/capability contracts.
- A new direct Process/ProcessStartInfo or shell path is found.
- process-tree tests leave residual children.
- a runtime aggregate owns more than one process host/registry.
- MAF references process-domain policy or Processes references native implementation details.
- an external package/native dependency cannot prove the claimed OS/profile.
