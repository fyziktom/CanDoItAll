# Subbundle result — M09

## Anchor

- Repository commit: `386d8beb6038035f89a9a6961ec017d8213879a5`
- Candidate source-manifest SHA-256: `a6fe597d186252e913e88b3896faf571e9ce474ef15a2bb8e6f311a7b817461e`
- M08 artifact-manifest SHA-256: `8b164654cb1b9e08db96260847468a33fa8fcd000e24b7db5ace8ed2d9db2c4b`
- Dependency mode: package; Components `0.1.18`; FileTools `0.1.18`

## Implemented behavior

The actual-host handoff now contains the immutable candidate and evidence hashes, one package-mode command sequence independent of adjacent dirty repositories, exact runtime/migration/focused test selections, two-cycle external startup, launchd lint, teardown, failure classification, and complete redaction instructions.

Keychain actual-session CRUD remains separately deferred. The alpha claim is headless `LocalUserFile` only and remains `ActualHostUnverified` until a colleague records actual macOS evidence.

## Validation reuse/invalidation

- Reused evidence: M08 Windows/Linux candidate manifest and artifact hashes.
- Invalidation rule: any production or dependency-anchor change on macOS invalidates M08; classify the failure before changing code.
- No macOS behavior is inferred from cross-publish, Linux, Docker, or static analysis.

## Residuals

No actual macOS arm64 colleague host was available in this execution environment. The handoff is ready, but required actual-host commands have not run and the first exit criterion remains open.

## Decision

`MACOS NO-GO — environment: actual macOS arm64 colleague execution has not occurred`

## Next eligible subbundle

M10 bookkeeping may record a bounded final NO-GO. `MERGE READY` remains prohibited until the M09 colleague result is replaced by actual-host evidence.
