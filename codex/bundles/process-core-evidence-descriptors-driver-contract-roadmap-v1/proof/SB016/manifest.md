# SB016 Proof Manifest

## Status
- Completed.

## Scope
- Create/update the explicit Core adapter map and adapter ownership list.
- Keep Core consumption exact, file-based, and free of wildcard dispatch exemptions.

## Evidence
- Current bundle adapter map: `bundle://architecture/06-core-adapter-ownership-map.md`.
- Inherited stabilization map update: `repo://codex/bundles/process-core-stabilization-diagnostics-driver-roadmap-v1/architecture/05-core-consumer-allowed-call-site-map.md`.
- Exact consumer scan: `bundle://proof/SB018/transcripts/explicit-core-consumer-list.txt`.
- Global using scan: `bundle://proof/SB018/transcripts/global-using-core-scan.txt`.

## Hashes
- SHA-256 `71E60955C752E4786A195BC022108DCFB13C4DC9105F7A7EDF5F60BEC059DD8B` for `bundle://architecture/06-core-adapter-ownership-map.md`.
- SHA-256 `0C73C7FFD5F15281F04C0C8011AEFCF2AE5D14BEFF6BD04C97EC6CB68C7827EF` for `repo://codex/bundles/process-core-stabilization-diagnostics-driver-roadmap-v1/architecture/05-core-consumer-allowed-call-site-map.md`.

## Result
- SB016 passed. Core consumers are listed explicitly in both the inherited map and the current bundle ownership map.
