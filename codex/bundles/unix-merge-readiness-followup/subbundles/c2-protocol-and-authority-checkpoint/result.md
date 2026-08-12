# Subbundle result — C2

## Anchor

- Repository commit: `386d8beb6038035f89a9a6961ec017d8213879a5` with accepted M00-M06/C1 working-tree changes
- Dependency mode: package
- Windows: x64, SDK `10.0.303`
- Linux: Docker Linux amd64, SDK `10.0.302`

## Implemented behavior

C2 grouped the final M04-M06 source into Release candidates on Windows and Linux. Focused protocol, Docker, executable, workspace-path, and ownership tests then ran without rebuilding those candidates. No stable full-suite run was attempted.

The runtime catalog guard was reconciled from 33 to 45 integration cases after the expanded explicit class set passed but the stale literal failed closed. Its paired source-contract assertion was updated to the same exact count. Windows then passed the guarded PowerShell catalog; Linux passed the same 422 Unit and 45 Integration cases plus the one real Chromium case in a pinned Playwright 1.55/.NET 10.0.302 runner.

The isolated final-source Docker stack passed positive and negative validation, rebuilt successfully, reached app/database health, returned HTTP 200 on loopback, ran the application as UID 1654 on a read-only root filesystem, and did not publish the database. All exact C2 containers, networks, volumes, custom images, NuGet cache, and disposable secret were removed. The existing user stack remained untouched.

## Commands and results

| Scope | Result |
|---|---|
| Windows Release focused Unit / Integration | PASS, 217/217 and 35/35 |
| Windows guarded runtime catalog | PASS, Unit 422; Integration 45; Browser 1 |
| Linux package Release solution build | PASS, 0 warnings/errors |
| Linux Release focused Unit / Integration | PASS, 217/217 and 35/35 |
| Linux runtime catalog | PASS, Unit 422; Integration 45; Browser 1 |
| Linux non-root executable authority | PASS, 22/22 |
| Docker validator positive/negative fixtures | PASS |
| Isolated final-source Docker stack | PASS, HTTP 200 and both services healthy |

## Validation reuse/invalidation

- Invalidated keys: the runtime catalog integration count and its source-contract assertion; M08 must consume the corrected count.
- Reused evidence: governed M04-M06 architecture and focused behavior proofs.
- Reason reuse is valid: C2 built and exercised the aggregate final source on both required local hosts without changing product behavior after the builds.

## Residuals

The first plain Linux SDK browser attempt failed because that image intentionally contains neither Docker CLI nor Chromium. The required browser case subsequently passed in the exact Playwright image with the pinned .NET SDK and isolated PostgreSQL; the environmental attempt is retained in the transcript rather than hidden.

## Decision

`GO`

## Next eligible subbundle

M07
