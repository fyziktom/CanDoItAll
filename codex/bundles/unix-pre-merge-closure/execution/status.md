# Execution status

## Source anchor

- Branch: `unix-adoption`
- Prepared-source commit: `af9206caf3c09dc25088e388727fda0e1b404833`
- Bundle commit and execution start: `79730d6a4e41db6ed4c1da260a37fd0297c7d98c`
- Target branch: `development`
- Target commit and merge base: `acc1ee4a5484dd98bd1df77f8e060a2a5a3b4c59`
- SDK: `10.0.303`
- Dependency mode: package (`UseLocalCanDoItAllLibraries=false`)
- Prepared bundle checksums: passed before execution
- Prepared-source delta: the single post-review commit adds only this bundle

## Proof tiers and progression

| Subbundle | Proof tier | Status | Prerequisite | Progression decision |
|---|---|---|---|---|
| F00 | Standard | Completed | Readiness gate | F01 unlocked |
| F01 | Governed | Completed | F00 | F02 unlocked |
| F02 | Governed | Completed | F01 | F03 unlocked |
| F03 | Governed | Completed | F02 | C1 unlocked; F04 waits for C1 |
| F04 | Behavioral | Completed | C1 | F05 unlocked |
| F05 | Standard | Completed | F04 | F06 unlocked |
| F06 | Governed | Completed | F01-F05 | Merge-ready decision recorded; macOS actual-host validation deferred |

## Reopen triggers

- Reopen F01 if later migration or runtime evidence changes a V1/V2 hash or authorizes an ambiguous payload.
- Reopen F02 if later process tests show a residual root, child, ownership handle, or observable identity after failed start.
- Reopen F03 if later recovery evidence reaches termination with missing or invalid boundary identity.
- Reopen F04 if the final rebuilt image lacks `setsid` or the application/database smoke is unhealthy.
- Reopen F05 if the exact-source build or package baseline differs from the tested snapshot.

## Current blockers

None. All local Windows/Linux gates passed. The rebuilt developer app and its
database are healthy; macOS actual-host validation remains the declared
post-merge boundary.
