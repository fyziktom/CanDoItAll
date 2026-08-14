# Subbundle result — M02

## Anchor

- Repository commit before: `386d8beb6038035f89a9a6961ec017d8213879a5` with accepted M00/M01 working-tree changes
- Components source anchor: clean `8372c1d55f21b349f8e859470b02eeb4421e96ca`
- FileTools source base: `f31e20d054003348c7557b9634e0838fc5996ae0`
- Recorded FileTools patch SHA-256: `029F0C87ED366C40661B76D25B6E2AF3CD47FDD68762DCBF8E721E1A0BB01749`
- Isolated patched FileTools validation commit: `514db471d703bc603594731dc8977946e9f6a98b`
- Package versions: Components `0.1.18`; FileTools `0.1.18`
- Host: Windows x64; SDK `10.0.303`; runtime `10.0.11`

## Changed files

- `Directory.Build.targets`
- `ConfiguredDesktopFileLauncher.cs`
- `FileToolsDownloadLeaseTests.cs`
- `Test-RuntimePortability.ps1`
- `Test-CorePortabilityHeadless.ps1`
- `tools/README.md`
- recorded FileTools patch and M02 proof records

## Implemented behavior

Package mode is now the unconditional default; sibling directory presence has no effect. Explicit source mode requires `UseLocalCanDoItAllLibraries=true`, exact expected commits for both sibling repositories, existing Git checkouts, matching `HEAD` values, and clean tracked worktrees. The checks run before restore/build work.

The FileTools desktop implementation is compile-time validated only when the explicitly selected source exposes `DesktopFileLaunchContract.Version == 2`. Package mode remains unavailable for alpha. Missing marker, mismatched anchor, or dirty checkout cannot produce a validated capability claim.

## Failing-first proof

Before M02, a build with automatically selected sibling sources reached compilation with duplicate Components package/project assemblies. After the change, package mode is standalone, and source mode is reachable only with explicit clean provenance.

## Commands and results

| Command | Exit | Duration | Evidence |
|---|---:|---:|---|
| evaluated default Integration graph with sibling repositories present | 0 | 1.1 s | package mode false; FileTools packages `0.1.18`; no sibling project refs |
| standalone package restore with nonexistent sibling roots | 0 | 1.3 s | package graph restored |
| standalone package Integration build | 0 | 1.4 s | 0 warnings/errors |
| isolated explicit source restore at exact commits | 0 | 3.2 s | clean source graph restored |
| isolated explicit source Integration build with contract v2 | 0 | 2.6 s | 0 warnings/errors |
| package-mode focused unit build/test | 0 | 36.4/2.5 s | 6 passed |
| isolated patched FileTools Desktop tests | 0 | 4.3 s | 20 passed |
| mismatched Components expected commit | 1 expected | 0.7 s | explicit anchor mismatch error |
| clean FileTools source without marker | 1 expected | 2.7 s | `DesktopFileLaunchContract` compile error |
| CodeAnalytics scoped refresh | 0 | 21.5 s | `snap-20260812114257-f381a8dc`; package graph; no blocking errors/cycles |

## Validation reuse/invalidation

- Invalidated keys: dependency graph selection, FileTools desktop capability claim, portability scripts, M08 integrated package candidate.
- Reused evidence: M00 sibling anchors and M01 product behavior.
- Reason reuse is valid: the M02 change does not alter M01 hashing/persistence semantics.

## Security and redaction

The build never prints sibling diffs or source content. Failure messages contain repository type and commit identifiers only. Package mode denies the desktop capability rather than silently falling back.

## Residuals

The actual FileTools sibling remains deliberately uncommitted with its original three modified files. Apply and review `proof/M02/patches/filetools-desktop-contract-v2.patch` in that repository, then commit it there before using source mode outside the isolated validation fixture. No actual sibling commit or push was performed.

## Decision

`GO`

## Next eligible subbundle

M03
