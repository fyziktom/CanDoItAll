# Subbundle result — M06

## Anchor

- Repository commit before: `386d8beb6038035f89a9a6961ec017d8213879a5` with accepted M00-M05/C1 working-tree changes
- Dependency mode: package
- Windows host: Windows x64; SDK `10.0.303`; runtime `10.0.11`
- Linux host: Docker Linux amd64; SDK `10.0.302`; non-root proof UID `1654`

## Implemented behavior

Executable candidates are bounded and reject every control character. `PATHEXT` is parsed as a bounded set of unique simple alphanumeric extensions; path, drive, URI, separator, control, duplicate, empty, and excessive inputs fail with the typed invalid-candidate result.

Unix executable acceptance now calls the current-identity `access(path, X_OK)` contract rather than accepting any execute bit. Unix paths are canonicalized with `realpath` after leaf-link resolution, and the process host starts the exact canonical identity that was validated and fingerprinted. The direct non-root proof rejects a file executable only by a different identity class.

`WorkspacePathAccessGuard` now applies the shared physical safe-path/reparse policy before returning workspace or managed-path success. Workspace-root and managed-file links escaping authority are rejected centrally, while missing leaves and explicit external-target aliases retain their intended contracts.

## Commands and results

| Scope | Result |
|---|---|
| Final Windows affected Unit slice | PASS, 91/91 |
| Final Windows affected Integration slice | PASS, 7/7 |
| Linux C2 focused Unit slice containing final M06 source | PASS, 217/217 |
| Linux C2 focused Integration slice containing final M06 source | PASS, 35/35 |
| Linux non-root executable-locator class | PASS, 22/22 |
| CodeAnalytics scoped refresh | PASS, `snap-20260812144653-9eb271a6`; no blocking errors |

## Validation reuse/invalidation

- Invalidated keys: executable candidate/`PATHEXT` parsing, Unix execute authority, canonical executable identity, workspace and managed-path reparse traversal, and M08 integrated authority proof.
- Reused evidence: M01 persistence compatibility, M02 dependency provenance, M04 MCP framing, and M05 Docker recipe/secret contracts.
- Reason reuse is valid: M06 changes only executable and workspace-path authority plus the owned-process integration fixture needed to keep M03 recovery proof semantically valid.

## Security and redaction

Native errors are converted to typed, bounded failure kinds. Workspace guard errors expose only the existing generic physical-path policy messages. No resolved physical root, connection string, or secret is written to bundle evidence.

## Residuals

The architecture snapshot reports only informational member-count findings and pre-existing unrelated module/type cycles. M06 adds no project reference, public interface, or dependency edge.

## Decision

`GO`

## Next eligible subbundle

C2
