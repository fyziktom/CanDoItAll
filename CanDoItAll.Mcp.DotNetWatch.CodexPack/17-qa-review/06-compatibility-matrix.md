# Compatibility matrix

| Platform / Environment | Status target | Key risks | Required validation |
|---|---|---|---|
| Windows 11 + local .NET 10 SDK | Primary supported | process tree kill semantics, port reuse timing | full P0 suite |
| Ubuntu 24.04 + local .NET 10 SDK | Primary supported | Unix process group behavior, file watcher semantics | full P0 suite |
| macOS 14/15 + local .NET 10 SDK | Strongly desired | process signals, dev cert differences | P0 + manual runtime pass |
| WSL2 | Supported with caveats | file watcher delay, path mapping, polling fallback | app/runtime/manual wait tests |
| Dev container / Docker bind mount | Supported with caveats | file watcher reliability, polling fallback, HTTPS assumptions | watch/manual tests |
| Network share / remote FS | Best effort only | file watcher reliability, latency | require polling fallback verification |

## Notes by area

### Process tree termination
- Windows: verify full subtree cleanup explicitly.
- Unix-like: verify process group signaling explicitly.

### Health probe
- Localhost HTTPS behavior may vary with trusted development certificates.
- Loopback-only default remains valid across platforms.

### File watching
- Containers, WSL, and network filesystems may require `DOTNET_USE_POLLING_FILE_WATCHER=1`.
- Watch exclusions should be verified in each environment where noisy folders are generated.

### SDK/tooling
- `.NET 10` is the baseline.
- `dotnet test` runner detection must handle repo-specific setup.

## Minimum official support statement for MVP
Recommend documenting MVP support as:
- Windows: supported
- Linux: supported
- macOS: supported after local validation
- WSL/container: supported with caveats
