# Subbundle result — M08

## Anchor

- Repository commit: `386d8beb6038035f89a9a6961ec017d8213879a5`
- Branch: `unix-adoption`
- Dependency mode: package (`UseLocalCanDoItAllLibraries=false`)
- Candidate raw-content manifest: `a6fe597d186252e913e88b3896faf571e9ce474ef15a2bb8e6f311a7b817461e` for 3,552 build-input files
- M08 artifact manifest: `8b164654cb1b9e08db96260847468a33fa8fcd000e24b7db5ace8ed2d9db2c4b` for 57 retained files
- Windows stamp: source fingerprint `5c0e24fdbdae3b821465f7828fc6f1d6a73666b08fe263d80aa7ab6dc76eb4cf`, SDK `10.0.303`
- Linux stamp: source fingerprint `b1ca3d15048b4c5034a5dd7048425002397c6b27371c84985693ef2e97c6d373`, SDK `10.0.302`

The durable runner fingerprints are host-specific because Git/PowerShell canonicalization differs across this Windows bind mount. Each stamp validated on its own host. The host-neutral handoff anchor is the raw SHA-256 file manifest, not equality between those two runner values.

## Implemented behavior

The M00-M07 candidate was frozen and exercised in package mode on Windows x64 and Linux x64. Both hosts completed clean restore/build, the exact 468-case runtime portability catalog, the transactional legacy-plan migration/restart/rollback proof, and two package-mode publish/start/restart cycles outside the checkout. The Linux browser case used real Chromium and an isolated PostgreSQL fixture. The Windows headless proof included first-cycle Chromium.

The isolated Compose candidate built and became healthy with Ready database operations. Runtime inspection confirmed a non-root user, read-only root filesystem, and all Linux capabilities dropped. Its containers, networks, volumes, image, and disposable secret were removed after evidence capture.

## Stable-suite evidence and classification

The full stable suite ran exactly once per host, as authorized:

| Host | Raw total | Raw passed | Raw failed | Classification |
|---|---:|---:|---:|---|
| Windows | 7,984 | 7,976 | 8 | Five known P2 residuals; three validation-test defects repaired and passed in exact reruns |
| Linux | 7,986 | 7,975 | 11 | Nine known/environmental P2 residuals; two portability-test defects repaired and passed 2/2 |

The recurring residual set is three pre-existing Component tests and two pre-existing ProjectStructure integration tests already classified outside the changed contract closures. Linux additionally exposed four Component timing/environment failures. None invalidates the 468/468 runtime catalog, migration proof, two-cycle startup proof, or Docker gate. The aggregate suites were not rerun after bounded test-validity repairs.

## Commands and results

| Scope | Result |
|---|---|
| Windows clean package restore/build | PASS; 0 warnings/errors |
| Linux clean package restore/build | PASS; 0 warnings/errors |
| Windows runtime catalog | PASS; Unit 422, Integration 45, Browser 1 |
| Linux runtime catalog | PASS; Unit 422, Integration 45, Browser 1 |
| Windows migration/restart/rollback | PASS; 1/1 |
| Linux migration/restart/rollback | PASS; 1/1 |
| Windows external headless cycles | PASS; 2/2, Chromium in cycle 1 |
| Linux external headless cycles | PASS; 2/2, graceful SIGTERM |
| Docker policy/negative fixtures | PASS |
| Isolated Compose smoke/teardown | PASS; app and database healthy |
| Portability baseline | PASS; 5,039 files, 30,206 findings, 13,562 reviewed executable-source occurrences unchanged |
| Complete artifact redaction | PASS; 56 text files, 0 oversized/unreadable, 0 findings |
| `git diff --check` | PASS; existing generated-model line-ending advisory only |

## Independent review

- Architecture: compiler-clean project boundaries and all catalogued architecture guards passed. An optional whole-solution CodeAnalytics refresh was rejected by the unverified MCP destination, so no source was disclosed; local build, reference, static, and architecture-test evidence is authoritative.
- Security: executable authority, owned process groups, bounded MCP framing, secret-file policy, symlink-safe paths, non-root/read-only Compose, and complete redaction all passed.
- Operations: two-cycle external startup and isolated Compose teardown prove package and container lifecycle behavior without relying on adjacent source repositories.
- Migration: the exact legacy payload survives upgrade, restart, fail-closed load classification, idempotent reapplication, and rollback on both hosts.
- Test validity: five cross-host validation defects were repaired only in test contracts and passed targeted Windows/Linux reruns. No product behavior was weakened to obtain a pass.

## Security and redaction

The first complete scan detected 24 synthetic key-shaped test parameters retained in two Unit TRXs. Those two TRXs were sanitized without changing XML validity or result counters; original and sanitized hashes are recorded in `stable-trx-redaction-manifest.json`. The final 60 MB scan has no size skips and zero findings.

Task-generated browser snapshots and the exact disposable M08 Docker resources were removed after sanitized evidence was retained.

## Residuals

- The stable-suite P2 residuals remain visible and are not reported as passes.
- Hosted CI was not executed, per the accepted deferral.
- Genuine macOS arm64 execution is still required. No macOS support claim is inferred from cross-publish or Linux evidence.

## Decision

`LOCAL MERGE CANDIDATE READY FOR MACOS VALIDATION`

## Next eligible subbundle

M09
