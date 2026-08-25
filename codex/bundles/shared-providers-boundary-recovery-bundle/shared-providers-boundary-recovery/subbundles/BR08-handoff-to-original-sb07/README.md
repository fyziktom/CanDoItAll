# BR08 — Handoff to the original SB07

## Objective

Record that the old Workspace ownership decisions are superseded and prepare the unfinished Docker-dependent SB07 work to continue against the corrected architecture, without running it.

## Required documentation changes

1. Add one concise supersession note under the original bundle root, for example:

   `codex/bundles/shared-providers/BOUNDARY-RECOVERY-HANDOFF.md`

   It must state:
   - recovery bundle commit range
   - ProviderManagement is canonical
   - Workspace ownership statements in original SB00/SB02 architecture documents are historical and superseded
   - original SB07 must use ProviderManagement ports and the unified MAF runtime
   - physical table names intentionally remain historical for compatibility
2. Append one status entry to the original top-level `STATUS.md` linking to the handoff note.
3. Do not rewrite every original architecture/proof/subbundle document.
4. Update this recovery bundle's `STATUS.md` to `DONE` only when BR07 passed.

## SB07 continuation constraints

The future original SB07 continuation must:

- start from the corrected branch HEAD
- read the handoff note before its own README
- use ProviderManagement services and Web endpoint mapping
- not recreate Workspace provider services or DI registrations
- not bypass the MAF-backed execution port
- retain the Docker retry budget and authorization policy separately agreed by the user

## Deferred validation record

List the exact Docker-dependent commands not run by this bundle and the reason. Do not execute them.

## Acceptance

- One handoff note exists.
- One original status entry exists.
- No historical-document churn occurred.
- Recovery `STATUS.md` accurately reflects BR00-BR08.
- Working tree is clean after the commit.

## Commit

`BR08: hand off corrected boundary to shared provider SB07`
